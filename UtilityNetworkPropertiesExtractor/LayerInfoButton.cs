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

        protected async override void OnClick()
        {
            try
            {
                await ExtractLayerInfoAsync();
                MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Layer Info");
            }
        }

        public static Task ExtractLayerInfoAsync()
        {
            return QueuedTask.Run(() =>
            {
                Common.CreateOutputDirectory();

                string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _fileName = string.Format("{0}_{1}_LayerInfo.csv", dateFormatted, Common.GetProProjectName());

                List<CSVLayout> csvLayoutList = new List<CSVLayout>();
                List<PopupLayout> popupLayoutList = new List<PopupLayout>();
                List<DisplayFilterLayout> displayFilterLayoutList = new List<DisplayFilterLayout>();
                List<SharedTraceConfigurationLayout> sharedTraceConfigurationLayout = new List<SharedTraceConfigurationLayout>();

                InterrogateLayers(ref csvLayoutList, ref popupLayoutList, ref displayFilterLayoutList, ref sharedTraceConfigurationLayout);

                string layerInfoFile = Path.Combine(Common.ExtractFilePath, _fileName);
                WriteLayerInfoCSV(csvLayoutList, layerInfoFile);

                string popupFile = layerInfoFile.Replace("LayerInfo", "LayerInfo_Popups");
                WritePopupCSV(popupLayoutList, popupFile);

                string displayFilterFile = layerInfoFile.Replace("LayerInfo", "LayerInfo_DisplayFilters");
                WriteDisplayFilterCSV(displayFilterLayoutList, displayFilterFile);

                string sharedTraceConfigFile = layerInfoFile.Replace("LayerInfo", "LayerInfo_SharedTraceConfiguration");
                WriteSharedTraceConfigurationCSV(sharedTraceConfigurationLayout, sharedTraceConfigFile);
            });
        }

        private static void WriteLayerInfoCSV(List<CSVLayout> csvLayoutList, string outputFile)
        {
            using (StreamWriter sw = new StreamWriter(outputFile))
            {
                //Header information
                sw.WriteLine(DateTime.Now + "," + "Layer Info");
                sw.WriteLine();
                sw.WriteLine("Project," + Project.Current.Path);
                sw.WriteLine("Map," + MapView.Active.Map.Name);
                sw.WriteLine("Layer Count," + MapView.Active.Map.GetLayersAsFlattenedList().OfType<Layer>().Count());
                sw.WriteLine("Table Count," + MapView.Active.Map.StandaloneTables.Count);
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

        private static void WriteDisplayFilterCSV(List<DisplayFilterLayout> displayFilterList, string outputFile)
        {
            using (StreamWriter sw = new StreamWriter(outputFile))
            {
                //Header information
                sw.WriteLine(DateTime.Now + "," + "Layer Info - Display Filters");
                sw.WriteLine();
                sw.WriteLine("Project," + Project.Current.Path);
                sw.WriteLine("Map," + MapView.Active.Map.Name);
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

        private static void WritePopupCSV(List<PopupLayout> popupLayoutList, string outputFile)
        {
            using (StreamWriter sw = new StreamWriter(outputFile))
            {
                //Header information
                sw.WriteLine(DateTime.Now + "," + "Layer Info - Popups");
                sw.WriteLine();
                sw.WriteLine("Project," + Project.Current.Path);
                sw.WriteLine("Map," + MapView.Active.Map.Name);
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
                sw.WriteLine("Map," + MapView.Active.Map.Name);
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

        private static void InterrogateLayers(ref List<CSVLayout> csvLayoutList, ref List<PopupLayout> popupLayoutList, ref List<DisplayFilterLayout> displayFilterLayoutList, ref List<SharedTraceConfigurationLayout> sharedTraceConfigurationLayout)
        {
            int layerPos = 1;
            string layerType = "";
            string prevGroupLayerName = string.Empty;
            string layerContainer = string.Empty;
            bool increaseLayerPos = false;
            int popupExpressionCount = 0;
            int displayFilterCount = 0;
            string popupName = string.Empty;
            string popupExpression = string.Empty;
            string displayFilterExpression = string.Empty;
            string displayFilterName = string.Empty;

            List<Layer> layerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<Layer>().ToList();
            foreach (Layer layer in layerList)
            {
                try
                {
                    popupExpressionCount = 0;
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

                    if (layer is FeatureLayer featureLayer)
                    {
                        layerType = "Feature Layer";
                        CIMFeatureLayer cimFeatureLayerDef = layer.GetDefinition() as CIMFeatureLayer;
                        CIMFeatureTable cimFeatureTable = cimFeatureLayerDef.FeatureTable;
                        CIMExpressionInfo cimExpressionInfo = cimFeatureTable.DisplayExpressionInfo; ;

                        //Primary Display Field
                        string displayField = cimFeatureTable.DisplayField;
                        if (cimExpressionInfo != null)
                            displayField = cimExpressionInfo.Expression.Replace("\"", "'");  //double quotes messes up the delimeters in the CSV

                        //Labeling
                        string labelExpression = string.Empty;
                        string labelMinScale = string.Empty;
                        string labelMaxScale = string.Empty;
                        if (cimFeatureLayerDef.LabelClasses != null)
                        {
                            if (cimFeatureLayerDef.LabelClasses.Length != 0)
                            {
                                List<CIMLabelClass> cimLabelClassList = cimFeatureLayerDef.LabelClasses.ToList();
                                CIMLabelClass cimLabelClass = cimLabelClassList.FirstOrDefault();
                                labelExpression = cimLabelClass.Expression.Replace("\"", "'");  //double quotes messes up the delimeters in the CSV
                                labelMinScale = GetScaleValue(cimLabelClass.MinimumScale);
                                labelMaxScale = GetScaleValue(cimLabelClass.MaximumScale);
                            }
                        }

                        //symbology
                        DetermineSymbology(cimFeatureLayerDef, out string primarySymbology, out string field1, out string field2, out string field3);

                        string subtypeValue = string.Empty;
                        if (featureLayer.IsSubtypeLayer)
                            subtypeValue = featureLayer.SubtypeValue.ToString();


                        //Include Pop-up expressions if exist
                        if (cimFeatureLayerDef.PopupInfo != null)
                        {
                            if (cimFeatureLayerDef.PopupInfo.ExpressionInfos != null)
                            {
                                bool popupExprVisibility = false;
                                for (int i = 0; i < cimFeatureLayerDef.PopupInfo.ExpressionInfos.Length; i++)
                                {
                                    //determine if expression is visible in popup
                                    CIMMediaInfo[] cimMediaInfos = cimFeatureLayerDef.PopupInfo.MediaInfos;
                                    for (int j = 0; j < cimMediaInfos.Length; j++)
                                    {
                                        if (cimMediaInfos[j] is CIMTableMediaInfo cimTableMediaInfo)
                                        {
                                            string[] fields = cimTableMediaInfo.Fields;
                                            for (int k = 0; k < fields.Length; k++)
                                            {
                                                if (fields[k] == "expression/" + cimFeatureLayerDef.PopupInfo.ExpressionInfos[i].Name)
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
                                        LayerPos = layerPos.ToString(),
                                        LayerType = layerType,
                                        GroupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer),
                                        LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                        PopupExpresssionName = cimFeatureLayerDef.PopupInfo.ExpressionInfos[i].Name,
                                        PopupExpresssionTitle = Common.EncloseStringInDoubleQuotes(cimFeatureLayerDef.PopupInfo.ExpressionInfos[i].Title.Replace("\"", "'")),
                                        PopupExpresssionVisible = popupExprVisibility.ToString(),
                                        PopupExpressionArcade = Common.EncloseStringInDoubleQuotes(cimFeatureLayerDef.PopupInfo.ExpressionInfos[i].Expression.Replace("\"", "'"))
                                    };
                                    popupLayoutList.Add(popupRec);
                                    popupExpressionCount += 1;
                                }
                            }
                        }

                        GetPopupInfoInfoForCSV(popupLayoutList, popupExpressionCount, ref popupName, ref popupExpression);

                        if (cimFeatureLayerDef.EnableDisplayFilters)
                        {
                            CIMDisplayFilter[] cimDisplayFilterChoices = cimFeatureLayerDef.DisplayFilterChoices;
                            CIMDisplayFilter[] cimDisplayFilter = cimFeatureLayerDef.DisplayFilters;
                            displayFilterCount = AddDisplayFiltersToList(layerPos.ToString(), layerType, Common.EncloseStringInDoubleQuotes(layerContainer), Common.EncloseStringInDoubleQuotes(layer.Name), cimDisplayFilterChoices, cimDisplayFilter, ref displayFilterLayoutList);
                            GetDisplayFilterInfoForCSV(displayFilterLayoutList, displayFilterCount, ref displayFilterExpression, ref displayFilterName);
                        }

                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            LayerType = layerType,
                            GroupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer),
                            IsVisible = layer.IsVisible.ToString(),
                            LayerSource = featureLayer.GetTable().GetPath().ToString(),
                            ClassName = featureLayer.GetTable().GetName(),
                            IsSubtypeLayer = featureLayer.IsSubtypeLayer.ToString(),
                            SubtypeValue = subtypeValue,
                            GeometryType = featureLayer.ShapeType.ToString(),
                            IsSelectable = featureLayer.IsSelectable.ToString(),
                            IsSnappable = featureLayer.IsSnappable.ToString(),
                            IsEditable = featureLayer.IsEditable.ToString(),
                            RefreshRate = cimFeatureLayerDef.RefreshRate.ToString(),
                            DefinitionQuery = Common.EncloseStringInDoubleQuotes(featureLayer.DefinitionFilter.DefinitionExpression),
                            DisplayFilterCount = displayFilterCount.ToString(),
                            DisplayFilterName = displayFilterName,
                            DisplayFilterExpresssion = displayFilterExpression,
                            MinScale = GetScaleValue(layer.MinScale),
                            MaxScale = GetScaleValue(layer.MaxScale),
                            ShowMapTips = cimFeatureLayerDef.ShowMapTips.ToString(),
                            PrimarySymbology = primarySymbology,
                            SymbologyField1 = field1,
                            SymbologyField2 = field2,
                            SymbologyField3 = field3,
                            EditTemplateCount = cimFeatureLayerDef.FeatureTemplates?.Count().ToString(),
                            DisplayField = Common.EncloseStringInDoubleQuotes(displayField),
                            IsLabelVisible = featureLayer.IsLabelVisible.ToString(),
                            LabelExpression = Common.EncloseStringInDoubleQuotes(labelExpression),
                            LabelMinScale = labelMinScale,
                            LabelMaxScale = labelMaxScale,
                            PopupExpresssionCount = popupExpressionCount.ToString(),
                            PopupExpresssionName = popupName,
                            PopupExpressionArcade = popupExpression
                        };
                        csvLayoutList.Add(rec);
                        increaseLayerPos = true;
                    }
                    else if (layer is SubtypeGroupLayer subtypeGroupLayer)
                    {
                        layerType = "Subtype Group Layer";
                        CIMSubtypeGroupLayer cimSubtypeGroupLayer = layer.GetDefinition() as CIMSubtypeGroupLayer;
                        if (cimSubtypeGroupLayer.EnableDisplayFilters)
                        {
                            CIMDisplayFilter[] cimDisplayFilterChoices = cimSubtypeGroupLayer.DisplayFilterChoices;
                            CIMDisplayFilter[] cimDisplayFilter = cimSubtypeGroupLayer.DisplayFilters;
                            displayFilterCount = AddDisplayFiltersToList(layerPos.ToString(), layerType, Common.EncloseStringInDoubleQuotes(layer.Name), string.Empty, cimDisplayFilterChoices, cimDisplayFilter, ref displayFilterLayoutList);
                            GetDisplayFilterInfoForCSV(displayFilterLayoutList, displayFilterCount, ref displayFilterExpression, ref displayFilterName);
                        }

                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            LayerType = layerType,
                            GroupLayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            IsVisible = layer.IsVisible.ToString(),
                            DefinitionQuery = Common.EncloseStringInDoubleQuotes(subtypeGroupLayer.DefinitionFilter.DefinitionExpression),
                            DisplayFilterCount = displayFilterCount.ToString(),
                            DisplayFilterName = displayFilterName,
                            DisplayFilterExpresssion = displayFilterExpression,
                            MinScale = GetScaleValue(layer.MinScale),
                            MaxScale = GetScaleValue(layer.MaxScale)
                        };
                        csvLayoutList.Add(rec);
                        increaseLayerPos = true;
                    }
                    else if (layer is GroupLayer groupLayer)
                    {
                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            GroupLayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            LayerType = "Group Layer",
                            IsVisible = layer.IsVisible.ToString(),
                            MinScale = GetScaleValue(layer.MinScale),
                            MaxScale = GetScaleValue(layer.MaxScale)
                        };
                        csvLayoutList.Add(rec);
                        increaseLayerPos = true;
                    }
                    else if (layer is AnnotationLayer annotationLayer)
                    {
                        layerType = "Annotation";
                        CIMAnnotationLayer cimAnnotationLayerDef = layer.GetDefinition() as CIMAnnotationLayer;
                        if (cimAnnotationLayerDef.EnableDisplayFilters)
                        {
                            CIMDisplayFilter[] cimDisplayFilterChoices = cimAnnotationLayerDef.DisplayFilterChoices;
                            CIMDisplayFilter[] cimDisplayFilter = cimAnnotationLayerDef.DisplayFilters;
                            displayFilterCount = AddDisplayFiltersToList(layerPos.ToString(), layerType, Common.EncloseStringInDoubleQuotes(layerContainer), Common.EncloseStringInDoubleQuotes(layer.Name), cimDisplayFilterChoices, cimDisplayFilter, ref displayFilterLayoutList);
                            GetDisplayFilterInfoForCSV(displayFilterLayoutList, displayFilterCount, ref displayFilterExpression, ref displayFilterName);
                        }

                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            LayerType = layerType,
                            GroupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer),
                            IsVisible = layer.IsVisible.ToString(),
                            LayerSource = annotationLayer.GetTable().GetPath().ToString(),
                            ClassName = annotationLayer.GetTable().GetName(),
                            IsSubtypeLayer = "FALSE",
                            GeometryType = annotationLayer.ShapeType.ToString(),
                            IsSelectable = annotationLayer.IsSelectable.ToString(),
                            IsEditable = annotationLayer.IsEditable.ToString(),
                            RefreshRate = cimAnnotationLayerDef.RefreshRate.ToString(),
                            DefinitionQuery = Common.EncloseStringInDoubleQuotes(annotationLayer.DefinitionFilter.DefinitionExpression),
                            DisplayFilterCount = displayFilterCount.ToString(),
                            DisplayFilterName = displayFilterName,
                            DisplayFilterExpresssion = displayFilterExpression,
                            MinScale = GetScaleValue(annotationLayer.MinScale),
                            MaxScale = GetScaleValue(annotationLayer.MaxScale)
                        };
                        csvLayoutList.Add(rec);
                        increaseLayerPos = true;
                    }
                    else if (layer is AnnotationSubLayer annotationSubLayer)
                    {
                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            LayerType = "Annotation Sub Layer",
                            GroupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer),
                            IsVisible = layer.IsVisible.ToString(),
                            MinScale = GetScaleValue(layer.MinScale),
                            MaxScale = GetScaleValue(layer.MaxScale)
                        };

                        csvLayoutList.Add(rec);
                        increaseLayerPos = true;
                    }
                    else if (layer is DimensionLayer dimensionLayer)
                    {
                        layerType = "Dimension";
                        CIMDimensionLayer cimDimensionLayerDef = layer.GetDefinition() as CIMDimensionLayer;
                        if (cimDimensionLayerDef.EnableDisplayFilters)
                        {
                            CIMDisplayFilter[] cimDisplayFilterChoices = cimDimensionLayerDef.DisplayFilterChoices;
                            CIMDisplayFilter[] cimDisplayFilter = cimDimensionLayerDef.DisplayFilters;
                            displayFilterCount = AddDisplayFiltersToList(layerPos.ToString(), layerType, Common.EncloseStringInDoubleQuotes(layerContainer), Common.EncloseStringInDoubleQuotes(layer.Name), cimDisplayFilterChoices, cimDisplayFilter, ref displayFilterLayoutList);
                            GetDisplayFilterInfoForCSV(displayFilterLayoutList, displayFilterCount, ref displayFilterExpression, ref displayFilterName);
                        }

                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            LayerType = layerType,
                            GroupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer),
                            IsVisible = layer.IsVisible.ToString(),
                            LayerSource = dimensionLayer.GetTable().GetPath().ToString(),
                            ClassName = dimensionLayer.GetTable().GetName(),
                            IsSubtypeLayer = "FALSE",
                            GeometryType = dimensionLayer.ShapeType.ToString(),
                            IsSelectable = dimensionLayer.IsSelectable.ToString(),
                            IsEditable = dimensionLayer.IsEditable.ToString(),
                            RefreshRate = cimDimensionLayerDef.RefreshRate.ToString(),
                            DefinitionQuery = Common.EncloseStringInDoubleQuotes(dimensionLayer.DefinitionFilter.DefinitionExpression),
                            DisplayFilterCount = displayFilterCount.ToString(),
                            DisplayFilterName = displayFilterName,
                            DisplayFilterExpresssion = displayFilterExpression,
                            MinScale = GetScaleValue(dimensionLayer.MinScale),
                            MaxScale = GetScaleValue(dimensionLayer.MaxScale)
                        };
                        csvLayoutList.Add(rec);
                        increaseLayerPos = true;

                    }
                    else if (layer is UtilityNetworkLayer utilityNetworkLayer)
                    {
                        layerType = "Utility Network Layer";
                        string sharedTraceConfiguation = "";

                        //Active Trace Configuration introduced in Utility Network version 5.
                        if (utilityNetworkLayer.UNVersion >= 5)
                        {
                            CIMUtilityNetworkLayer cimUtilityNetworkLayer = layer.GetDefinition() as CIMUtilityNetworkLayer;
                            CIMNetworkTraceConfiguration[] cimNetworkTraceConfigurations = cimUtilityNetworkLayer.ActiveTraceConfigurations;
                            if (cimNetworkTraceConfigurations != null)
                            {
                                sharedTraceConfiguation = cimNetworkTraceConfigurations.Length.ToString();

                                for (int j = 0; j < cimNetworkTraceConfigurations.Length; j++)
                                {
                                    SharedTraceConfigurationLayout traceConfig = new SharedTraceConfigurationLayout()
                                    {
                                        LayerPos = layerPos.ToString(),
                                        LayerType = layerType,
                                        LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                        GroupLayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
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
                            sharedTraceConfiguation = "see LayerInfo_SharedTraceConfiguration.csv";

                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            GroupLayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            LayerType = layerType,
                            IsVisible = layer.IsVisible.ToString(),
                            SharedTraceConfigurationCount = sharedTraceConfigurationLayout.Count.ToString(),
                            SharedTraceConfiguration = sharedTraceConfiguation
                        };
                        csvLayoutList.Add(rec);
                        increaseLayerPos = true;
                    }
                    else if (layer is TiledServiceLayer tiledServiceLayer)
                    {
                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            LayerType = "Tiled Service Layer",
                            IsVisible = layer.IsVisible.ToString(),
                            LayerSource = tiledServiceLayer.URL
                        };
                        csvLayoutList.Add(rec);
                        increaseLayerPos = true;
                    }
                    else if (layer is VectorTileLayer vectorTileLayer)
                    {
                        CIMVectorTileDataConnection cimVectorTileDataConn = layer.GetDataConnection() as CIMVectorTileDataConnection;

                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            LayerType = "Vector Tile Layer",
                            IsVisible = layer.IsVisible.ToString(),
                            LayerSource = cimVectorTileDataConn.URI
                        };
                        csvLayoutList.Add(rec);
                        increaseLayerPos = true;
                    }
                    else if (layer is GraphicsLayer graphicsLayer)
                    {
                        CIMGraphicsLayer cimGraphicsLayer = layer.GetDefinition() as CIMGraphicsLayer;

                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            GroupLayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            LayerType = "Graphics Layer",
                            IsVisible = layer.IsVisible.ToString(),
                            IsSelectable = cimGraphicsLayer.Selectable.ToString(),
                            RefreshRate = cimGraphicsLayer.RefreshRate.ToString(),
                            MinScale = GetScaleValue(layer.MinScale),
                            MaxScale = GetScaleValue(layer.MaxScale)
                        };
                        csvLayoutList.Add(rec);
                        increaseLayerPos = true;
                    }
                    else if (layer.MapLayerType == MapLayerType.BasemapBackground)
                    {
                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            LayerType = "Basemap",
                            IsVisible = layer.IsVisible.ToString()
                        };
                        csvLayoutList.Add(rec);
                        increaseLayerPos = true;
                    }
                    else
                    {
                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            LayerType = "Not Defined in this tool",
                            IsVisible = layer.IsVisible.ToString()
                        };
                        csvLayoutList.Add(rec);
                        increaseLayerPos = true;
                    }
                }
                catch (Exception ex)
                {
                    CSVLayout rec = new CSVLayout()
                    {
                        LayerPos = layerPos.ToString(),
                        LayerType = "Extract Error",
                        GroupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer),
                        LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),                       
                        IsVisible = layer.IsVisible.ToString(),
                        LayerSource = ex.Message
                    };
                    csvLayoutList.Add(rec);
                    increaseLayerPos = true;
                }

                //increment counter by 1
                if (increaseLayerPos)
                    layerPos += 1;

                increaseLayerPos = false;
            }

            //Standalone Tables
            layerType = "Table";
            IReadOnlyList<StandaloneTable> standaloneTableList = MapView.Active.Map.StandaloneTables;
            foreach (StandaloneTable standaloneTable in standaloneTableList)
            {
                CIMStandaloneTable cimStandaloneTable = standaloneTable.GetDefinition();
                CIMExpressionInfo cimExpressionInfo = cimStandaloneTable.DisplayExpressionInfo;

                //Primary Display Field
                string displayField = cimStandaloneTable.DisplayField;
                if (cimExpressionInfo != null)
                    displayField = cimExpressionInfo.Expression.Replace("\"", "'");  //double quotes messes up the delimeters in the CSV

                //Include Pop-up expressions if exist
                if (cimStandaloneTable.PopupInfo != null)
                {
                    if (cimStandaloneTable.PopupInfo.ExpressionInfos != null)
                    {
                        bool popupExprVisibility = false;
                        for (int i = 0; i < cimStandaloneTable.PopupInfo.ExpressionInfos.Length; i++)
                        {
                            //determine if expression is visible in popup
                            CIMMediaInfo[] cimMediaInfos = cimStandaloneTable.PopupInfo.MediaInfos;
                            for (int j = 0; j < cimMediaInfos.Length; j++)
                            {
                                if (cimMediaInfos[j] is CIMTableMediaInfo cimTableMediaInfo)
                                {
                                    string[] fields = cimTableMediaInfo.Fields;
                                    for (int k = 0; k < fields.Length; k++)
                                    {
                                        if (fields[k] == "expression/" + cimStandaloneTable.PopupInfo.ExpressionInfos[i].Name)
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
                                LayerPos = layerPos.ToString(),
                                LayerType = layerType,
                                LayerName = Common.EncloseStringInDoubleQuotes(standaloneTable.Name),
                                PopupExpresssionName = cimStandaloneTable.PopupInfo.ExpressionInfos[i].Name,
                                PopupExpresssionTitle = Common.EncloseStringInDoubleQuotes(cimStandaloneTable.PopupInfo.ExpressionInfos[i].Title.Replace("\"", "'")),
                                PopupExpresssionVisible = popupExprVisibility.ToString(),
                                PopupExpressionArcade = Common.EncloseStringInDoubleQuotes(cimStandaloneTable.PopupInfo.ExpressionInfos[i].Expression.Replace("\"", "'"))
                            };
                            popupLayoutList.Add(popupRec);
                            popupExpressionCount += 1;
                        }
                    }
                }

                GetPopupInfoInfoForCSV(popupLayoutList, popupExpressionCount, ref popupName, ref popupExpression);

                CSVLayout rec = new CSVLayout()
                {
                    LayerPos = layerPos.ToString(),
                    LayerName = Common.EncloseStringInDoubleQuotes(standaloneTable.Name),
                    LayerType = layerType,
                    LayerSource = standaloneTable.GetTable().GetPath().ToString(),
                    ClassName = standaloneTable.GetTable().GetName(),
                    DefinitionQuery = Common.EncloseStringInDoubleQuotes(standaloneTable.DefinitionFilter.DefinitionExpression),
                    DisplayField = Common.EncloseStringInDoubleQuotes(displayField),
                    PopupExpresssionCount = popupExpressionCount.ToString(),
                    PopupExpresssionName = popupName,
                    PopupExpressionArcade = popupExpression
                };
                csvLayoutList.Add(rec);

                layerPos += 1;
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
                displayFilterName = "see LayerInfo_DisplayFilter.csv";
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
                popupExpression = "see LayerInfo_Popup.csv";
            }
        }

        private static int AddDisplayFiltersToList(string layerPos, string layerType, string groupLayerName, string layerName, CIMDisplayFilter[] cimDisplayFilterChoices, CIMDisplayFilter[] cimDisplayFilter, ref List<DisplayFilterLayout> displayFilterList)
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
                        LayerPos = layerPos,
                        LayerType = layerType,
                        GroupLayerName = groupLayerName,
                        LayerName = layerName,
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
                        LayerPos = layerPos,
                        LayerType = layerType,
                        GroupLayerName = groupLayerName,
                        LayerName = layerName,
                        DisplayFilterType = "By Scale",
                        DisplayFilterName = Common.EncloseStringInDoubleQuotes(cimDisplayFilter[k].Name),
                        MinScale = GetScaleValue(cimDisplayFilter[k].MinScale),
                        MaxScale = GetScaleValue(cimDisplayFilter[k].MaxScale)
                    };
                    displayFilterList.Add(rec);
                    recsAdded += 1;
                }
            }
            return recsAdded;
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

        private static string GetScaleValue(double scale)
        {
            if (scale == 0)
                return "<None>";  // In Pro, when there is no scale set, the value is null.  Thru the SDK, it was showing 0.
            else
                return scale.ToString();
        }

        private class CSVLayout
        {
            public string LayerPos { get; set; }
            public string LayerType { get; set; }
            public string GroupLayerName { get; set; }
            public string LayerName { get; set; }
            public string IsVisible { get; set; }
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
            public string DefinitionQuery { get; set; }
            public string DisplayFilterCount { get; set; }
            public string DisplayFilterName { get; set; }
            public string DisplayFilterExpresssion { get; set; }
            public string MinScale { get; set; }
            public string MaxScale { get; set; }
            public string ShowMapTips { get; set; }
            public string PrimarySymbology { get; set; }
            public string SymbologyField1 { get; set; }
            public string SymbologyField2 { get; set; }
            public string SymbologyField3 { get; set; }
            public string EditTemplateCount { get; set; }
            public string DisplayField { get; set; }
            public string IsLabelVisible { get; set; }
            public string LabelExpression { get; set; }
            public string LabelMinScale { get; set; }
            public string LabelMaxScale { get; set; }
            public string PopupExpresssionCount { get; set; }
            public string PopupExpresssionName { get; set; }
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

        private class DisplayFilterLayout
        {
            public string LayerPos { get; set; }
            public string LayerType { get; set; }
            public string GroupLayerName { get; set; }
            public string LayerName { get; set; }
            public string DisplayFilterType { get; set; }
            public string DisplayFilterName { get; set; }
            public string DisplayFilterExpresssion { get; set; }
            public string MinScale { get; set; }
            public string MaxScale { get; set; }
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