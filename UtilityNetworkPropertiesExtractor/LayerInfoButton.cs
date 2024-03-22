/*
   Copyright 2021 Esri
   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at
       http://www.apache.org/licenses/LICENSE-2.0
   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS, 
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace UtilityNetworkPropertiesExtractor
{
    internal class LayerInfoButton : Button
    {
        private static string _fileName = string.Empty;
        private const string _defQueriesMesg = "see LayerInfo_DefinitionQueries";

        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Layer Info to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractLayerInfoAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Layer Info");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractLayerInfoAsync()
        {
            return QueuedTask.Run(() =>
            {
                Common.CreateOutputDirectory();

                string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _fileName = string.Format("{0}_{1}_LayerInfo.csv", dateFormatted, Common.GetActiveMapName());

                List<CSVLayout> csvLayoutList = new List<CSVLayout>();
                List<PopupLayout> popupLayoutList = new List<PopupLayout>();
                List<DisplayFilterLayout> displayFilterLayoutList = new List<DisplayFilterLayout>();
                List<SharedTraceConfigurationLayout> sharedTraceConfigurationLayoutList = new List<SharedTraceConfigurationLayout>();
                List<DefinitionQueryLayout> definitionQueryLayoutList = new List<DefinitionQueryLayout>();
                List<LabelLayout> labelLayoutList = new List<LabelLayout>();

                InterrogateLayers(ref csvLayoutList, ref popupLayoutList, ref displayFilterLayoutList, ref sharedTraceConfigurationLayoutList, ref definitionQueryLayoutList, ref labelLayoutList);

                string layerInfoFile = Path.Combine(Common.ExtractFilePath, _fileName);
                WriteLayerInfoCSV(csvLayoutList, layerInfoFile);

                if (popupLayoutList.Count >= 1)
                {
                    string popupFile = layerInfoFile.Replace("LayerInfo", "LayerInfo_PopupExprs");
                    WritePopupCSV(popupLayoutList, popupFile);
                }

                if (displayFilterLayoutList.Count >= 1)
                {
                    string displayFilterFile = layerInfoFile.Replace("LayerInfo", "LayerInfo_DisplayFilters");
                    WriteDisplayFilterCSV(displayFilterLayoutList, displayFilterFile);
                }

                if (sharedTraceConfigurationLayoutList.Count >= 1)
                {
                    string sharedTraceConfigFile = layerInfoFile.Replace("LayerInfo", "LayerInfo_SharedTraceConfig");
                    WriteSharedTraceConfigurationCSV(sharedTraceConfigurationLayoutList, sharedTraceConfigFile);
                }

                if (definitionQueryLayoutList.Count >= 1)
                {
                    string defQueryFile = layerInfoFile.Replace("LayerInfo", "LayerInfo_DefinitionQueries");
                    WriteDefQueriesCSV(definitionQueryLayoutList, defQueryFile);
                }

                if (labelLayoutList.Count >= 1)
                {
                    string labelFile = layerInfoFile.Replace("LayerInfo", "LayerInfo_Labels");
                    WriteLabelCSV(labelLayoutList, labelFile);
                }
            });
        }

        private static void InterrogateLayers(ref List<CSVLayout> csvLayoutList, ref List<PopupLayout> popupLayoutList, ref List<DisplayFilterLayout> displayFilterLayoutList, ref List<SharedTraceConfigurationLayout> sharedTraceConfigurationLayout, ref List<DefinitionQueryLayout> definitionQueryLayout, ref List<LabelLayout> labelLayoutList)
        {
            int displayFilterCount;
            int layerPos = 1;
            int popupExpressionCount;
            int labelCount;
            string layerContainer;
            string prevGroupLayerName = string.Empty;
            string popupName = string.Empty;
            string popupExpression = string.Empty;
            string displayFilterExpression = string.Empty;
            string displayFilterName = string.Empty;
            string additionalDefQueriesText;

            List<Layer> layerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<Layer>().ToList();
            foreach (Layer layer in layerList)
            {
                CSVLayout csvLayout = new CSVLayout();
                try
                {
                    popupExpressionCount = 0;
                    labelCount = 0;
                    popupName = string.Empty;
                    popupExpression = string.Empty;
                    displayFilterCount = 0;
                    displayFilterExpression = string.Empty;
                    displayFilterName = string.Empty;

                    layerContainer = layer.Parent.ToString();
                    if (layerContainer != MapView.Active.Map.Name) // Group layer
                    {
                        if (layerContainer != prevGroupLayerName)
                            prevGroupLayerName = layerContainer;
                    }
                    else
                        layerContainer = string.Empty;

                    csvLayout.LayerPos = layerPos.ToString();
                    csvLayout.LayerType = Common.GetLayerTypeDescription(layer);
                    csvLayout.LayerName = Common.EncloseStringInDoubleQuotes(layer.Name);
                    csvLayout.GroupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer);
                    csvLayout.IsExpanded = layer.IsExpanded.ToString();
                    csvLayout.IsVisible = layer.IsVisible.ToString();
                    csvLayout.MaxScale = Common.GetScaleValueText(layer.MaxScale);
                    csvLayout.MinScale = Common.GetScaleValueText(layer.MinScale);

                    //BasicFeatureLayer (Layers that inherit from BasicFeatureLayer are FeatureLayer, AnnotationLayer and DimensionLayer)
                    if (layer is BasicFeatureLayer basicFeatureLayer)
                    {
                        csvLayout.ActiveDefinitionQuery = Common.EncloseStringInDoubleQuotes(basicFeatureLayer.DefinitionQuery);
                        csvLayout.ClassName = basicFeatureLayer.GetTable().GetName();
                        csvLayout.GeometryType = basicFeatureLayer.ShapeType.ToString();
                        csvLayout.IsEditable = basicFeatureLayer.IsEditable.ToString();
                        csvLayout.IsSelectable = basicFeatureLayer.IsSelectable.ToString();
                        csvLayout.LayerSource = basicFeatureLayer.GetTable().GetPath().ToString();

                        //Display Filters
                        CIMBasicFeatureLayer cimBasicFeatureLayer = layer.GetDefinition() as CIMBasicFeatureLayer;
                        if (cimBasicFeatureLayer.EnableDisplayFilters)
                        {
                            CIMDisplayFilter[] cimDisplayFilterChoices = cimBasicFeatureLayer.DisplayFilterChoices;
                            CIMDisplayFilter[] cimDisplayFilter = cimBasicFeatureLayer.DisplayFilters;
                            displayFilterCount = AddDisplayFiltersToList(csvLayout, cimDisplayFilterChoices, cimDisplayFilter, ref displayFilterLayoutList);
                            GetDisplayFilterInfoForCSV(displayFilterLayoutList, displayFilterCount, ref displayFilterExpression, ref displayFilterName);
                            
                            csvLayout.DisplayFilterCount = displayFilterCount.ToString();
                            csvLayout.DisplayFilterExpresssion = displayFilterExpression;
                            csvLayout.DisplayFilterName = displayFilterName;
                        }

                        //FeatureLayer
                        if (basicFeatureLayer is FeatureLayer featureLayer)
                        {
                            CIMFeatureLayer cimFeatureLayer = layer.GetDefinition() as CIMFeatureLayer;
                            CIMFeatureTable cimFeatureTable = cimFeatureLayer.FeatureTable;
                            CIMExpressionInfo cimExpressionInfo = cimFeatureTable.DisplayExpressionInfo;

                            //Primary Display Field
                            string displayField = cimFeatureTable.DisplayField;
                            if (cimExpressionInfo != null)
                            {
                                displayField = cimExpressionInfo.Expression.Replace("\"", "'");  //double quotes messes up the delimeters in the CSV
                            }

                            //Labeling
                            string labelExpression = string.Empty;
                            LabelLayout labelRec = null;
                            labelCount = AddLabelInfoToList(csvLayout, cimFeatureLayer, ref labelLayoutList);
                            if (labelCount > 0)
                            {
                                if (labelCount == 1)
                                {
                                    labelRec = labelLayoutList.LastOrDefault(); // this layers label will always be at the bottom of the list
                                    labelExpression = labelRec.LabelExpression;
                                }
                                else if (labelCount >= 2)
                                    labelExpression = "see LayerInfo_Labels.csv";
                            }

                            //symbology
                            DetermineSymbology(cimFeatureLayer, out string primarySymbology, out string field1, out string field2, out string field3);

                            //Subtypes
                            string subtypeValue = string.Empty;
                            if (featureLayer.IsSubtypeLayer)
                                subtypeValue = featureLayer.SubtypeValue.ToString();

                            //Popups
                            string popupUseLayerFields = GetPopupUseLayerFieldsVal(cimFeatureLayer.PopupInfo);
                            popupExpressionCount = AddPopupInfoToList(csvLayout, cimFeatureLayer.PopupInfo, ref popupLayoutList);
                            GetPopupInfoInfoForCSV(popupLayoutList, popupExpressionCount, ref popupName, ref popupExpression);

                            //Definition Queries
                            if (! featureLayer.IsSubtypeLayer)
                                additionalDefQueriesText = AddDefinitionQueriesToList(csvLayout, featureLayer.DefinitionQueries, featureLayer.DefinitionQuery, ref definitionQueryLayout);
                            else
                            {
                                //When the featurelayer is part of a subtype group layer, the definition query can only be set at the SGL level
                                additionalDefQueriesText = string.Empty;
                                csvLayout.ActiveDefinitionQuery = string.Empty;  
                            }

                            //Assign Featurelayer values
                            csvLayout.AdditionalDefinitionQueries = additionalDefQueriesText;
                            csvLayout.DisplayField = Common.EncloseStringInDoubleQuotes(displayField);
                            csvLayout.EditTemplateCount = cimFeatureLayer.FeatureTemplates?.Count().ToString();
                            csvLayout.IsSnappable = featureLayer.IsSnappable.ToString();
                            csvLayout.IsSubtypeLayer = featureLayer.IsSubtypeLayer.ToString();
                            csvLayout.IsLabelVisible = featureLayer.IsLabelVisible.ToString();
                            csvLayout.LabelCount = labelCount.ToString();
                            csvLayout.LabelName = labelRec?.LabelName;
                            csvLayout.LabelExpression = labelExpression;
                            csvLayout.LabelMaxScale = labelRec?.MaxScale;
                            csvLayout.LabelMinScale = labelRec?.MinScale;
                            csvLayout.PopupExpressionArcade = popupExpression;
                            csvLayout.PopupExpressionCount = popupExpressionCount.ToString();
                            csvLayout.PopupExpressionName = popupName;
                            csvLayout.PopupUseLayerFields = popupUseLayerFields.ToString();
                            csvLayout.PrimarySymbology = primarySymbology;
                            csvLayout.SymbologyField1 = field1;
                            csvLayout.SymbologyField2 = field2;
                            csvLayout.SymbologyField3 = field3;
                            csvLayout.RefreshRate = cimFeatureLayer.RefreshRate.ToString();
                            csvLayout.ShowMapTips = cimFeatureLayer.ShowMapTips.ToString();
                            csvLayout.SubtypeValue = subtypeValue;
                        }

                        //Annotation Layer
                        else if (basicFeatureLayer is AnnotationLayer annotationLayer)
                        {
                            //Definition Queries
                            additionalDefQueriesText = AddDefinitionQueriesToList(csvLayout, annotationLayer.DefinitionQueries, annotationLayer.DefinitionQuery, ref definitionQueryLayout);
                            csvLayout.GroupLayerName = csvLayout.LayerName;
                            csvLayout.AdditionalDefinitionQueries = additionalDefQueriesText;
                        }

                        //Dimension Layer
                        else if (basicFeatureLayer is DimensionLayer dimensionLayer)
                        {
                            //Definition Queries
                            additionalDefQueriesText = AddDefinitionQueriesToList(csvLayout, dimensionLayer.DefinitionQueries, dimensionLayer.DefinitionQuery, ref definitionQueryLayout);
                            csvLayout.AdditionalDefinitionQueries = additionalDefQueriesText;
                        }
                    }

                    //Subtype Group Layer
                    else if (layer is SubtypeGroupLayer subtypeGroupLayer)
                    {
                        csvLayout.GroupLayerName = csvLayout.LayerName;
                        csvLayout.LayerName = string.Empty;

                        CIMSubtypeGroupLayer cimSubtypeGroupLayer = layer.GetDefinition() as CIMSubtypeGroupLayer;
                        if (cimSubtypeGroupLayer.EnableDisplayFilters)
                        {
                            CIMDisplayFilter[] cimDisplayFilterChoices = cimSubtypeGroupLayer.DisplayFilterChoices;
                            CIMDisplayFilter[] cimDisplayFilter = cimSubtypeGroupLayer.DisplayFilters;
                            displayFilterCount = AddDisplayFiltersToList(csvLayout, cimDisplayFilterChoices, cimDisplayFilter, ref displayFilterLayoutList);
                            GetDisplayFilterInfoForCSV(displayFilterLayoutList, displayFilterCount, ref displayFilterExpression, ref displayFilterName);
                        }

                        //Definition Queries
                        additionalDefQueriesText = AddDefinitionQueriesToList(csvLayout, subtypeGroupLayer.DefinitionQueries, subtypeGroupLayer.DefinitionQuery, ref definitionQueryLayout);

                        csvLayout.ActiveDefinitionQuery = Common.EncloseStringInDoubleQuotes(subtypeGroupLayer.DefinitionQuery);
                        csvLayout.AdditionalDefinitionQueries = additionalDefQueriesText;
                        csvLayout.DisplayFilterCount = displayFilterCount.ToString();
                        csvLayout.DisplayFilterExpresssion = displayFilterExpression;
                        csvLayout.DisplayFilterName = displayFilterName;
                    }

                    //Group Layer
                    else if (layer is GroupLayer groupLayer)
                    {
                        csvLayout.GroupLayerName = csvLayout.LayerName;
                        csvLayout.LayerName = string.Empty;
                    }

                    //Utiliy Network Layer
                    else if (layer is UtilityNetworkLayer utilityNetworkLayer)
                    {
                        csvLayout.GroupLayerName = csvLayout.LayerName;

                        //Trace Configuration introduced in Utility Network version 5.
                        string sharedTraceConfiguation = "";
                        if (utilityNetworkLayer.UNVersion >= 5)
                        {
                            CIMUtilityNetworkLayer cimUtilityNetworkLayer = layer.GetDefinition() as CIMUtilityNetworkLayer;
                            CIMNetworkTraceConfiguration[] cimNetworkTraceConfigurations = cimUtilityNetworkLayer.ActiveTraceConfigurations;
                            if (cimNetworkTraceConfigurations != null)
                            {
                                for (int j = 0; j < cimNetworkTraceConfigurations.Length; j++)
                                {
                                    SharedTraceConfigurationLayout traceConfig = new SharedTraceConfigurationLayout()
                                    {
                                        LayerPos = csvLayout.LayerPos,
                                        LayerType = csvLayout.LayerType,
                                        LayerName = csvLayout.LayerName,
                                        GroupLayerName = csvLayout.GroupLayerName,
                                        TraceConfiguration = Common.EncloseStringInDoubleQuotes(cimNetworkTraceConfigurations[j].Name)
                                    };
                                    sharedTraceConfigurationLayout.Add(traceConfig);
                                }
                            }
                        }

                        if (sharedTraceConfigurationLayout.Count == 1)
                        {
                            SharedTraceConfigurationLayout shared = sharedTraceConfigurationLayout.LastOrDefault();
                            sharedTraceConfiguation = shared.TraceConfiguration;
                        }
                        else if (sharedTraceConfigurationLayout.Count >= 2)
                            sharedTraceConfiguation = "see LayerInfo_SharedTraceConfig.csv";

                        csvLayout.SharedTraceConfigurationCount = sharedTraceConfigurationLayout.Count.ToString();
                        csvLayout.SharedTraceConfiguration = sharedTraceConfiguation;
                    }

                    //Tile Service Layer
                    else if (layer is TiledServiceLayer tiledServiceLayer)
                    {
                        csvLayout.LayerSource = tiledServiceLayer.URL;
                    }

                    //Image Service Layer
                    else if (layer is ImageServiceLayer imageServiceLayer)
                    {
                        CIMAGSServiceConnection cimAGSServiceConnection = imageServiceLayer.GetDataConnection() as CIMAGSServiceConnection;
                        csvLayout.LayerSource = cimAGSServiceConnection.URL;
                    }

                    //Vector Tile Layer
                    else if (layer is VectorTileLayer vectorTileLayer)
                    {
                        CIMVectorTileDataConnection cimVectorTileDataConn = vectorTileLayer.GetDataConnection() as CIMVectorTileDataConnection;
                        csvLayout.LayerSource = cimVectorTileDataConn.URI;
                    }

                    //Graphics Layer
                    else if (layer is GraphicsLayer graphicsLayer)
                    {
                        CIMGraphicsLayer cimGraphicsLayer = graphicsLayer.GetDefinition() as CIMGraphicsLayer;
                        csvLayout.IsSelectable = cimGraphicsLayer.Selectable.ToString();
                        csvLayout.RefreshRate = cimGraphicsLayer.RefreshRate.ToString();
                    }
                }
                catch (Exception ex)
                {
                    csvLayout.LayerType = "Extract Error";
                    csvLayout.LayerSource = ex.Message;
                }

                //Assign record to the list
                csvLayoutList.Add(csvLayout);

                //increment counter by 1
                layerPos += 1;
            }

            //Standalone Tables
            IReadOnlyList<StandaloneTable> standaloneTableList = MapView.Active.Map.StandaloneTables;
            layerPos = InterrogateStandaloneTables(standaloneTableList, layerPos, string.Empty, ref csvLayoutList, ref popupLayoutList, ref definitionQueryLayout);

            //Tables in Group Layers
            //  Will show up at the bottom of the CSV.  This isn't quite right.
            List<GroupLayer> groupLayerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<GroupLayer>().ToList();
            foreach (GroupLayer groupLayer in groupLayerList)
            {
                if (groupLayer.StandaloneTables.Count > 0)
                    layerPos = InterrogateStandaloneTables(groupLayer.StandaloneTables, layerPos, groupLayer.Name, ref csvLayoutList, ref popupLayoutList, ref definitionQueryLayout);
            }
        }

        private static int InterrogateStandaloneTables(IReadOnlyList<StandaloneTable> standaloneTableList, int layerPos, string groupLayerName, ref List<CSVLayout> csvLayoutList, ref List<PopupLayout> popupLayoutList, ref List<DefinitionQueryLayout> definitionQueryLayout)
        {
            int popupExpressionCount;
            string popupName = string.Empty;
            string popupExpression = string.Empty;
            foreach (StandaloneTable standaloneTable in standaloneTableList)
            {
                CSVLayout csvLayout = new CSVLayout()
                {
                    ActiveDefinitionQuery = Common.EncloseStringInDoubleQuotes(standaloneTable.DefinitionQuery),
                    ClassName = standaloneTable.GetTable().GetName(),
                    GroupLayerName = Common.EncloseStringInDoubleQuotes(groupLayerName),
                    LayerName = Common.EncloseStringInDoubleQuotes(standaloneTable.Name),
                    LayerPos = layerPos.ToString(),
                    LayerSource = standaloneTable.GetTable().GetPath().ToString(),
                };

                if (string.IsNullOrEmpty(groupLayerName))
                    csvLayout.LayerType = "Standalone Table";
                else
                    csvLayout.LayerType = "Table in Group Layer";

                //Primary Display Field            
                CIMStandaloneTable cimStandaloneTable = standaloneTable.GetDefinition();
                CIMExpressionInfo cimExpressionInfo = cimStandaloneTable.DisplayExpressionInfo;
                string displayField = cimStandaloneTable.DisplayField;
                if (cimExpressionInfo != null)
                    displayField = cimExpressionInfo.Expression.Replace("\"", "'");  //double quotes messes up the delimeters in the CSV

                //Pop-ups
                string popupUseLayerFields = GetPopupUseLayerFieldsVal(cimStandaloneTable.PopupInfo);
                popupExpressionCount = AddPopupInfoToList(csvLayout, cimStandaloneTable.PopupInfo, ref popupLayoutList);
                GetPopupInfoInfoForCSV(popupLayoutList, popupExpressionCount, ref popupName, ref popupExpression);

                //Definition Queries
                string additionalDefQueriesText = AddDefinitionQueriesToList(csvLayout, standaloneTable.DefinitionQueries, standaloneTable.DefinitionQuery, ref definitionQueryLayout);

                //assign values
                csvLayout.AdditionalDefinitionQueries = additionalDefQueriesText;
                csvLayout.DisplayField = Common.EncloseStringInDoubleQuotes(displayField);
                csvLayout.PopupExpressionCount = popupExpressionCount.ToString();
                csvLayout.PopupExpressionName = popupName;
                csvLayout.PopupExpressionArcade = popupExpression;
                csvLayout.PopupUseLayerFields = popupUseLayerFields.ToString();

                //Add record to list
                csvLayoutList.Add(csvLayout);

                layerPos += 1;
            }

            return layerPos; // need to identify next layer position for "table in group layers"
        }

        private static void WriteDisplayFilterCSV(List<DisplayFilterLayout> displayFilterList, string outputFile)
        {
            using (StreamWriter sw = new StreamWriter(outputFile))
            {
                //Header information
                sw.WriteLine(DateTime.Now + "," + "Layer Info - Display Filters");
                sw.WriteLine();
                sw.WriteLine("Project," + Project.Current.Path);
                sw.WriteLine("Map," + Common.GetActiveMapName());
                sw.WriteLine();

                //Get all properties defined in the class.  This will be used to generate the CSV file
                DisplayFilterLayout emptyRec = new DisplayFilterLayout();
                PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                //Write column headers based on properties in the class
                string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                sw.WriteLine(columnHeader);

                foreach (DisplayFilterLayout row in displayFilterList)
                {
                    string output = Common.ExtractClassValuesToString(row, properties);
                    sw.WriteLine(output);
                }
            }
        }

        private static void WriteLayerInfoCSV(List<CSVLayout> csvLayoutList, string outputFile)
        {
            using (StreamWriter sw = new StreamWriter(outputFile))
            {
                //Header information
                sw.WriteLine(DateTime.Now + "," + "Layer Info");
                sw.WriteLine();
                sw.WriteLine("Project," + Project.Current.Path);
                sw.WriteLine("Map," + Common.GetActiveMapName());
                sw.WriteLine("Coordinate System," + MapView.Active.Map.SpatialReference.Name);
                sw.WriteLine("Map Units," + MapView.Active.Map.SpatialReference.Unit);
                sw.WriteLine("Layers," + MapView.Active.Map.GetLayersAsFlattenedList().OfType<Layer>().Count());
                sw.WriteLine("Standalone Tables," + MapView.Active.Map.StandaloneTables.Count);
                int tablesInGroupLayers = Common.GetCountOfTablesInGroupLayers();
                if (tablesInGroupLayers > 0)
                    sw.WriteLine("Tables in Group Layers," + Common.GetCountOfTablesInGroupLayers());

                sw.WriteLine();

                //Get all properties defined in the class.  This will be used to generate the CSV file
                CSVLayout emptyRec = new CSVLayout();
                PropertyInfo[] csvProperties = Common.GetPropertiesOfClass(emptyRec);

                //Write column headers based on properties in the class
                string columnHeader = Common.ExtractClassPropertyNamesToString(csvProperties);
                sw.WriteLine(columnHeader);

                foreach (CSVLayout row in csvLayoutList)
                {
                    string output = Common.ExtractClassValuesToString(row, csvProperties);
                    sw.WriteLine(output);
                }
            }
        }

        private static void WriteLabelCSV(List<LabelLayout> labelLayoutList, string outputFile)
        {
            using (StreamWriter sw = new StreamWriter(outputFile))
            {
                //Header information
                sw.WriteLine(DateTime.Now + "," + "Layer Info - Labels");
                sw.WriteLine();
                sw.WriteLine("Project," + Project.Current.Path);
                sw.WriteLine("Map," + Common.GetActiveMapName());
                sw.WriteLine();

                //Get all properties defined in the class.  This will be used to generate the CSV file
                LabelLayout emptyRec = new LabelLayout();
                PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                //Write column headers based on properties in the class
                string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                sw.WriteLine(columnHeader);

                foreach (LabelLayout row in labelLayoutList)
                {
                    string output = Common.ExtractClassValuesToString(row, properties);
                    sw.WriteLine(output);
                }
            }
        }

        private static void WritePopupCSV(List<PopupLayout> popupLayoutList, string outputFile)
        {
            using (StreamWriter sw = new StreamWriter(outputFile))
            {
                //Header information
                sw.WriteLine(DateTime.Now + "," + "Layer Info - Popup Expressions");
                sw.WriteLine();
                sw.WriteLine("Project," + Project.Current.Path);
                sw.WriteLine("Map," + Common.GetActiveMapName());
                sw.WriteLine();

                //Get all properties defined in the class.  This will be used to generate the CSV file
                PopupLayout emptyRec = new PopupLayout();
                PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                //Write column headers based on properties in the class
                string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                sw.WriteLine(columnHeader);

                foreach (PopupLayout row in popupLayoutList)
                {
                    string output = Common.ExtractClassValuesToString(row, properties);
                    sw.WriteLine(output);
                }
            }
        }

        private static void WriteSharedTraceConfigurationCSV(List<SharedTraceConfigurationLayout> sharedTraceConfigurationList, string outputFile)
        {
            using (StreamWriter sw = new StreamWriter(outputFile))
            {
                //Header information
                sw.WriteLine(DateTime.Now + "," + "Layer Info - Shared Trace Configuration");
                sw.WriteLine();
                sw.WriteLine("Project," + Project.Current.Path);
                sw.WriteLine("Map," + Common.GetActiveMapName());
                sw.WriteLine();

                //Get all properties defined in the class.  This will be used to generate the CSV file
                SharedTraceConfigurationLayout emptyRec = new SharedTraceConfigurationLayout();
                PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                //Write column headers based on properties in the class
                string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                sw.WriteLine(columnHeader);

                foreach (SharedTraceConfigurationLayout row in sharedTraceConfigurationList)
                {
                    string output = Common.ExtractClassValuesToString(row, properties);
                    sw.WriteLine(output);
                }
            }
        }

        private static void WriteDefQueriesCSV(List<DefinitionQueryLayout> defQueriesList, string outputFile)
        {
            using (StreamWriter sw = new StreamWriter(outputFile))
            {
                //Header information
                sw.WriteLine(DateTime.Now + "," + "Layer Info - Definition Queries");
                sw.WriteLine();
                sw.WriteLine("Project," + Project.Current.Path);
                sw.WriteLine("Map," + Common.GetActiveMapName());
                sw.WriteLine();

                //Get all properties defined in the class.  This will be used to generate the CSV file
                DefinitionQueryLayout emptyRec = new DefinitionQueryLayout();
                PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                //Write column headers based on properties in the class
                string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                sw.WriteLine(columnHeader);

                foreach (DefinitionQueryLayout row in defQueriesList)
                {
                    string output = Common.ExtractClassValuesToString(row, properties);
                    sw.WriteLine(output);
                }
            }
        }

        private static void GetDisplayFilterInfoForCSV(List<DisplayFilterLayout> displayFilterLayoutList, int displayFilterCount, ref string displayFilterExpression, ref string displayFilterName)
        {
            if (displayFilterCount == 1)
            {
                DisplayFilterLayout filter = displayFilterLayoutList.LastOrDefault();
                displayFilterName = filter.DisplayFilterName;

                if (filter.DisplayFilterType == "By Scale")
                    displayFilterExpression = filter.DisplayFilterType;
                else
                    displayFilterExpression = filter.DisplayFilterExpresssion;
            }
            else if (displayFilterCount >= 2)
                displayFilterName = "see LayerInfo_DisplayFilters.csv";
        }

        private static void GetPopupInfoInfoForCSV(List<PopupLayout> popupLayoutList, int popupCount, ref string popupName, ref string popupExpression)
        {
            if (popupCount == 1)
            {
                PopupLayout popup = popupLayoutList.LastOrDefault();
                popupName = popup.PopupExpresssionName;
                popupExpression = popup.PopupExpressionArcade;
            }
            else if (popupCount >= 2)
            {
                popupName = string.Empty;
                popupExpression = "see LayerInfo_PopupExpr.csv";
            }
        }
                
        private static void DetermineSymbology(CIMFeatureLayer cimFeatureLayerDef, out string primarySymbology, out string field1, out string field2, out string field3)
        {
            primarySymbology = string.Empty;
            field1 = string.Empty;
            field2 = string.Empty;
            field3 = string.Empty;

            //Symbology
            if (cimFeatureLayerDef.Renderer is CIMSimpleRenderer)
                primarySymbology = "Single Symbol";
            else if (cimFeatureLayerDef.Renderer is CIMUniqueValueRenderer uniqueRenderer)
            {
                primarySymbology = "Unique Values";

                switch (uniqueRenderer.Fields.Length)
                {
                    case 1:
                        field1 = uniqueRenderer.Fields[0];
                        break;
                    case 2:
                        field1 = uniqueRenderer.Fields[0];
                        field2 = uniqueRenderer.Fields[1];
                        break;
                    case 3:
                        field1 = uniqueRenderer.Fields[0];
                        field2 = uniqueRenderer.Fields[1];
                        field3 = uniqueRenderer.Fields[2];
                        break;
                }
            }
            else if (cimFeatureLayerDef.Renderer is CIMChartRenderer)
                primarySymbology = "Charts";
            else if (cimFeatureLayerDef.Renderer is CIMClassBreaksRendererBase classBreaksRenderer)
                primarySymbology = classBreaksRenderer.ClassBreakType.ToString();
            else if (cimFeatureLayerDef.Renderer is CIMDictionaryRenderer)
                primarySymbology = "Dictionary";
            else if (cimFeatureLayerDef.Renderer is CIMDotDensityRenderer)
                primarySymbology = "Dot Density";
            else if (cimFeatureLayerDef.Renderer is CIMHeatMapRenderer)
                primarySymbology = "Heat Map";
            else if (cimFeatureLayerDef.Renderer is CIMProportionalRenderer)
                primarySymbology = "Proportional Symbols";
            else if (cimFeatureLayerDef.Renderer is CIMRepresentationRenderer)
                primarySymbology = "Representation";
        }
               
        private static string AddDefinitionQueriesToList(CSVLayout csvLayout, IReadOnlyList<DefinitionQuery> definitionQuery, string activeFilterName, ref List<DefinitionQueryLayout> definitionQueryLayoutList)
        {
            string returnMessage = string.Empty;
            int cnt = 0;

            if (definitionQuery.Count() > 0)
            {
                bool activeDefQuery;
                foreach (DefinitionQuery filter in definitionQuery)
                {
                    if (string.IsNullOrEmpty(activeFilterName))
                        activeDefQuery = false;
                    else
                    {
                        if (activeFilterName == filter.Name)
                            activeDefQuery = true;
                        else
                            activeDefQuery = false;
                    }

                    DefinitionQueryLayout definitionQueryLayout = new DefinitionQueryLayout()
                    {
                        LayerPos = csvLayout.LayerPos,
                        LayerType = csvLayout.LayerType,
                        GroupLayerName = csvLayout.GroupLayerName,
                        LayerName = csvLayout.LayerName,
                        DefinitionQueryName = filter.Name,
                        DefinitionQuery = Common.EncloseStringInDoubleQuotes(filter.Name),
                        Active = activeDefQuery.ToString()
                    };

                    definitionQueryLayoutList.Add(definitionQueryLayout);
                    cnt += 1;
                }
            }

            // if active definition filter is defined, only indicate additional def queries if count is greater than 1.
            if (!string.IsNullOrEmpty(activeFilterName))
            {
                if (cnt > 1)
                    returnMessage = _defQueriesMesg;
            }
            else  // No active definition query
            {
                if (cnt > 0)  // Return Message indicates if additional queries exist.
                    returnMessage = _defQueriesMesg;
            }
            return returnMessage;
        }

        private static int AddDisplayFiltersToList(CSVLayout csvLayout, CIMDisplayFilter[] cimDisplayFilterChoices, CIMDisplayFilter[] cimDisplayFilter, ref List<DisplayFilterLayout> displayFilterList)
        {
            int recsAdded = 0;
            //In Pro, there are 2 choices to set the Active Display Filters
            //option 1:  Manually 
            if (cimDisplayFilterChoices != null)
            {
                for (int j = 0; j < cimDisplayFilterChoices.Length; j++)
                {
                    DisplayFilterLayout rec = new DisplayFilterLayout()
                    {
                        LayerPos = csvLayout.LayerPos,
                        LayerType = csvLayout.LayerType,
                        GroupLayerName = csvLayout.GroupLayerName,
                        LayerName = csvLayout.LayerName,
                        DisplayFilterType = "Manually",
                        DisplayFilterName = Common.EncloseStringInDoubleQuotes(cimDisplayFilterChoices[j].Name),
                        DisplayFilterExpresssion = Common.EncloseStringInDoubleQuotes(cimDisplayFilterChoices[j].WhereClause),
                    };
                    displayFilterList.Add(rec);
                    recsAdded += 1;
                }
            }

            //option 2:  By Scale
            if (cimDisplayFilter != null)
            {
                for (int k = 0; k < cimDisplayFilter.Length; k++)
                {
                    if (cimDisplayFilter[k].Name == "Hide Display")
                        continue;

                    DisplayFilterLayout rec = new DisplayFilterLayout()
                    {
                        LayerPos = csvLayout.LayerPos,
                        LayerType = csvLayout.LayerType,
                        GroupLayerName = csvLayout.GroupLayerName,
                        LayerName = csvLayout.LayerName,
                        DisplayFilterType = "By Scale",
                        DisplayFilterName = Common.EncloseStringInDoubleQuotes(cimDisplayFilter[k].Name),
                        MinScale = Common.GetScaleValueText(cimDisplayFilter[k].MinScale),
                        MaxScale = Common.GetScaleValueText(cimDisplayFilter[k].MaxScale)
                    };
                    displayFilterList.Add(rec);
                    recsAdded += 1;
                }
            }
            return recsAdded;
        }

        private static int AddLabelInfoToList(CSVLayout csvLayout, CIMFeatureLayer cimFeatureLayer, ref List<LabelLayout> labelLayoutList)
        {
            int labelCount = 0;

            if (cimFeatureLayer.LabelClasses != null)
            {
                for (int i = 0; i < cimFeatureLayer.LabelClasses.Length; i++)
                {
                    CIMLabelClass cimLabelClass = cimFeatureLayer.LabelClasses[i];
                    string expr = cimLabelClass.Expression.Replace("\"", "'");  //double quotes messes up the delimeters in the CSV

                    LabelLayout labelRec = new LabelLayout()
                    {
                        LayerPos = csvLayout.LayerPos,
                        LayerType = csvLayout.LayerType,
                        LayerName = csvLayout.LayerName,
                        GroupLayerName = csvLayout.GroupLayerName,
                        Visibile = cimLabelClass.Visibility.ToString(),
                        LabelName = Common.EncloseStringInDoubleQuotes(cimLabelClass.Name),
                        LabelEngine = cimLabelClass.ExpressionEngine.ToString(),
                        LabelExpression = Common.EncloseStringInDoubleQuotes(expr),
                        MinScale = Common.GetScaleValueText(cimLabelClass.MinimumScale),
                        MaxScale = Common.GetScaleValueText(cimLabelClass.MaximumScale)
                    };

                    labelLayoutList.Add(labelRec);
                    labelCount += 1;
                }
            }
            return labelCount;
        }

        private static int AddPopupInfoToList(CSVLayout csvLayout, CIMPopupInfo cimPopupInfo, ref List<PopupLayout> popupLayoutList)
        {
            //Include Pop-up expressions if exist
            int popupExpressionCount = 0;

            if (cimPopupInfo != null)
            {
                if (cimPopupInfo.ExpressionInfos != null)
                {
                    bool popupExprVisibility = false;
                    for (int i = 0; i < cimPopupInfo.ExpressionInfos.Length; i++)
                    {
                        //determine if expression is visible in popup
                        CIMMediaInfo[] cimMediaInfos = cimPopupInfo.MediaInfos;
                        for (int j = 0; j < cimMediaInfos.Length; j++)
                        {
                            if (cimMediaInfos[j] is CIMTableMediaInfo cimTableMediaInfo)
                            {
                                string[] fields = cimTableMediaInfo.Fields;
                                for (int k = 0; k < fields.Length; k++)
                                {
                                    if (fields[k] == "expression/" + cimPopupInfo.ExpressionInfos[i].Name)
                                    {
                                        popupExprVisibility = true;
                                        break;
                                    }
                                }
                            }

                            //Break out of 2nd loop (j) if already found the expression
                            if (popupExprVisibility)
                                break;
                        }

                        //Write popup info
                        PopupLayout popupRec = new PopupLayout()
                        {
                            LayerPos = csvLayout.LayerPos,
                            LayerType = csvLayout.LayerType,
                            LayerName = csvLayout.LayerName,
                            GroupLayerName = csvLayout.GroupLayerName,
                            PopupExpresssionName = cimPopupInfo.ExpressionInfos[i].Name,
                            PopupExpresssionTitle = Common.EncloseStringInDoubleQuotes(cimPopupInfo.ExpressionInfos[i].Title.Replace("\"", "'")),
                            PopupExpresssionVisible = popupExprVisibility.ToString(),
                            PopupExpressionArcade = Common.EncloseStringInDoubleQuotes(cimPopupInfo.ExpressionInfos[i].Expression.Replace("\"", "'"))
                        };

                        //Microsoft Excel has a character limit of 32,767 characters in each cell
                        if (popupRec.PopupExpressionArcade.Length > 32767)
                            popupRec.PopupExpressionArcade = "Expression length is greater than a single cell in Excel can handle";

                        popupLayoutList.Add(popupRec);
                        popupExpressionCount += 1;
                    }
                }
            }
            return popupExpressionCount;
        }

        private static string GetPopupUseLayerFieldsVal(CIMPopupInfo cimPopupInfo)
        {
            if (cimPopupInfo != null)
            {
                CIMMediaInfo[] cimMediaInfos = cimPopupInfo.MediaInfos;
                for (int j = 0; j < cimMediaInfos.Length; j++)
                {
                    if (cimMediaInfos[j] is CIMTableMediaInfo cimTableMediaInfo)
                        return cimTableMediaInfo.UseLayerFields.ToString();
                }
                return string.Empty;
            }
            else
                return "Default settings used";
        }

        private class CSVLayout
        {
            public string LayerPos { get; set; }
            public string LayerType { get; set; }
            public string GroupLayerName { get; set; }
            public string LayerName { get; set; }
            public string IsVisible { get; set; }
            public string IsExpanded { get; set; }
            public string LayerSource { get; set; }
            public string ClassName { get; set; }
            public string IsSubtypeLayer { get; set; }
            public string SubtypeValue { get; set; }
            public string GeometryType { get; set; }
            public string IsSnappable { get; set; }
            public string IsSelectable { get; set; }
            public string IsEditable { get; set; }
            public string RefreshRate { get; set; }
            public string SharedTraceConfigurationCount { get; set; }
            public string SharedTraceConfiguration { get; set; }
            public string ActiveDefinitionQuery { get; set; }
            public string AdditionalDefinitionQueries { get; set; }
            public string DisplayFilterCount { get; set; }
            public string DisplayFilterName { get; set; }
            public string DisplayFilterExpresssion { get; set; }
            public string MaxScale { get; set; }
            public string MinScale { get; set; }
            public string ShowMapTips { get; set; }
            public string PrimarySymbology { get; set; }
            public string SymbologyField1 { get; set; }
            public string SymbologyField2 { get; set; }
            public string SymbologyField3 { get; set; }
            public string EditTemplateCount { get; set; }
            public string DisplayField { get; set; }
            public string LabelCount { get; set; }
            public string IsLabelVisible { get; set; }
            public string LabelName { get; set; }
            public string LabelExpression { get; set; }
            public string LabelMaxScale { get; set; }
            public string LabelMinScale { get; set; }
            public string PopupUseLayerFields { get; set; }
            public string PopupExpressionCount { get; set; }
            public string PopupExpressionName { get; set; }
            public string PopupExpressionArcade { get; set; }
        }

        private class PopupLayout
        {
            public string LayerPos { get; set; }
            public string LayerType { get; set; }
            public string GroupLayerName { get; set; }
            public string LayerName { get; set; }
            public string PopupExpresssionName { get; set; }
            public string PopupExpresssionTitle { get; set; }
            public string PopupExpresssionVisible { get; set; }
            public string PopupExpressionArcade { get; set; }
        }

        private class DefinitionQueryLayout
        {
            public string LayerPos { get; set; }
            public string LayerType { get; set; }
            public string GroupLayerName { get; set; }
            public string LayerName { get; set; }
            public string Active { get; set; }
            public string DefinitionQueryName { get; set; }
            public string DefinitionQuery { get; set; }
        }

        private class DisplayFilterLayout
        {
            public string LayerPos { get; set; }
            public string LayerType { get; set; }
            public string GroupLayerName { get; set; }
            public string LayerName { get; set; }
            public string DisplayFilterType { get; set; }
            public string DisplayFilterName { get; set; }
            public string DisplayFilterExpresssion { get; set; }
            public string MaxScale { get; set; }
            public string MinScale { get; set; }
        }

        private class LabelLayout
        {
            public string LayerPos { get; set; }
            public string LayerType { get; set; }
            public string GroupLayerName { get; set; }
            public string LayerName { get; set; }
            public string LabelName { get; set; }
            public string Visibile { get; set; }
            public string LabelEngine { get; set; }
            public string LabelExpression { get; set; }
            public string MaxScale { get; set; }
            public string MinScale { get; set; }
        }

        private class SharedTraceConfigurationLayout
        {
            public string LayerPos { get; set; }
            public string LayerType { get; set; }
            public string GroupLayerName { get; set; }
            public string LayerName { get; set; }
            public string TraceConfiguration { get; set; }
        }
    }
}