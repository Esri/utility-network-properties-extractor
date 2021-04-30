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
                string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

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
                    PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                    //Write column headers based on properties in the class
                    string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                    sw.WriteLine(columnHeader);

                    int layerPos = 1;
                    string prevGroupLayerName = string.Empty;
                    string layerContainer = string.Empty;
                    bool increaseLayerPos = false;

                    List<CSVLayout> CSVLayoutList = new List<CSVLayout>();

                    List<Layer> layerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<Layer>().ToList();
                    foreach (Layer layer in layerList)
                    {
                        try
                        {
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

                                CSVLayout rec = new CSVLayout()
                                {
                                    LayerPos = layerPos.ToString(),
                                    LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                    LayerType = "Feature Layer",
                                    GroupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer),
                                    IsVisible = layer.IsVisible.ToString(),
                                    LayerSource = featureLayer.GetTable().GetPath().ToString(),
                                    ClassName = featureLayer.GetTable().GetName(),
                                    IsSubtypeGroupLayer = featureLayer.IsSubtypeLayer.ToString(),
                                    GeometryType = featureLayer.ShapeType.ToString(),
                                    IsSelectable = featureLayer.IsSelectable.ToString(),
                                    IsEditable = featureLayer.IsEditable.ToString(),
                                    RefreshRate = cimFeatureLayerDef.RefreshRate.ToString(),
                                    DefinitionQuery = featureLayer.DefinitionFilter.DefinitionExpression,
                                    MinScale = GetScaleValue(layer.MinScale),
                                    MaxScale = GetScaleValue(layer.MaxScale),
                                    PrimarySymbology = primarySymbology,
                                    SymbologyField1 = field1,
                                    SymbologyField2 = field2,
                                    SymbologyField3 = field3,
                                    EditTemplateCount = cimFeatureLayerDef.FeatureTemplates?.Count().ToString(),
                                    DisplayField = Common.EncloseStringInDoubleQuotes(displayField),
                                    IsLabelVisible = featureLayer.IsLabelVisible.ToString(),
                                    LabelExpression = Common.EncloseStringInDoubleQuotes(labelExpression),
                                    LabelMinScale = labelMinScale,
                                    LabelMaxScale = labelMaxScale
                                };
                                CSVLayoutList.Add(rec);
                                increaseLayerPos = true;

                                if (cimFeatureLayerDef.EnableDisplayFilters)
                                {
                                    CIMDisplayFilter[] cimDisplayFilterChoices = cimFeatureLayerDef.DisplayFilterChoices;
                                    CIMDisplayFilter[] cimDisplayFilter = cimFeatureLayerDef.DisplayFilters;
                                    AddDisplayFiltersToList(rec, cimDisplayFilterChoices, cimDisplayFilter, ref CSVLayoutList);
                                }

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
                                            CSVLayout popupRec = new CSVLayout()
                                            {
                                                LayerPos = rec.LayerPos,
                                                LayerName = rec.LayerName,
                                                PopupExpresssionName = cimFeatureLayerDef.PopupInfo.ExpressionInfos[i].Name,
                                                PopupExpresssionTitle = Common.EncloseStringInDoubleQuotes(cimFeatureLayerDef.PopupInfo.ExpressionInfos[i].Title.Replace("\"", "'")),
                                                PopupExpresssionVisible = popupExprVisibility.ToString(),
                                                PopupExpressionArcade = Common.EncloseStringInDoubleQuotes(cimFeatureLayerDef.PopupInfo.ExpressionInfos[i].Expression.Replace("\"", "'"))
                                            };
                                            CSVLayoutList.Add(popupRec);
                                        }
                                    }
                                }
                            }
                            else if (layer is SubtypeGroupLayer subtypeGroupLayer)
                            {
                                CIMSubtypeGroupLayer cimSubtypeGroupLayer = layer.GetDefinition() as CIMSubtypeGroupLayer;

                                CSVLayout rec = new CSVLayout()
                                {
                                    LayerPos = layerPos.ToString(),
                                    LayerType = "Subtype Group Layer",
                                    GroupLayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                    IsVisible = layer.IsVisible.ToString(),
                                    DefinitionQuery = subtypeGroupLayer.DefinitionFilter.DefinitionExpression,
                                    MinScale = GetScaleValue(layer.MinScale),
                                    MaxScale = GetScaleValue(layer.MaxScale)
                                };
                                CSVLayoutList.Add(rec);
                                increaseLayerPos = true;

                                if (cimSubtypeGroupLayer.EnableDisplayFilters)
                                {
                                    CIMDisplayFilter[] cimDisplayFilterChoices = cimSubtypeGroupLayer.DisplayFilterChoices;
                                    CIMDisplayFilter[] cimDisplayFilter = cimSubtypeGroupLayer.DisplayFilters;
                                    AddDisplayFiltersToList(rec, cimDisplayFilterChoices, cimDisplayFilter, ref CSVLayoutList);
                                }
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
                                CSVLayoutList.Add(rec);
                                increaseLayerPos = true;
                            }
                            else if (layer is AnnotationLayer annotationLayer)
                            {
                                CIMAnnotationLayer cimAnnotationLayer = layer.GetDefinition() as CIMAnnotationLayer;

                                CSVLayout rec = new CSVLayout()
                                {
                                    LayerPos = layerPos.ToString(),
                                    LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                    LayerType = "Annotation",
                                    GroupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer),
                                    IsVisible = layer.IsVisible.ToString(),
                                    LayerSource = annotationLayer.GetTable().GetPath().ToString(),
                                    ClassName = annotationLayer.GetTable().GetName(),
                                    IsSubtypeGroupLayer = "FALSE",
                                    GeometryType = annotationLayer.ShapeType.ToString(),
                                    IsSelectable = annotationLayer.IsSelectable.ToString(),
                                    IsEditable = annotationLayer.IsEditable.ToString(),
                                    RefreshRate = cimAnnotationLayer.RefreshRate.ToString(),
                                    DefinitionQuery = annotationLayer.DefinitionFilter.DefinitionExpression,
                                    MinScale = GetScaleValue(annotationLayer.MinScale),
                                    MaxScale = GetScaleValue(annotationLayer.MaxScale),
                                };
                                CSVLayoutList.Add(rec);
                                increaseLayerPos = true;

                                CIMAnnotationLayer cimAnnotationLayerDef = layer.GetDefinition() as CIMAnnotationLayer;
                                if (cimAnnotationLayerDef.EnableDisplayFilters)
                                {
                                    CIMDisplayFilter[] cimDisplayFilterChoices = cimAnnotationLayerDef.DisplayFilterChoices;
                                    CIMDisplayFilter[] cimDisplayFilter = cimAnnotationLayerDef.DisplayFilters;
                                    AddDisplayFiltersToList(rec, cimDisplayFilterChoices, cimDisplayFilter, ref CSVLayoutList);
                                }
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

                                CSVLayoutList.Add(rec);
                                increaseLayerPos = true;
                            }
                            else if (layer is DimensionLayer dimensionLayer)
                            {
                                CIMDimensionLayer cimDimensionLayer = layer.GetDefinition() as CIMDimensionLayer;

                                CSVLayout rec = new CSVLayout()
                                {
                                    LayerPos = layerPos.ToString(),
                                    LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                    LayerType = "Dimension",
                                    GroupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer),
                                    IsVisible = layer.IsVisible.ToString(),
                                    LayerSource = dimensionLayer.GetTable().GetPath().ToString(),
                                    ClassName = dimensionLayer.GetTable().GetName(),
                                    IsSubtypeGroupLayer = "FALSE",
                                    GeometryType = dimensionLayer.ShapeType.ToString(),
                                    IsSelectable = dimensionLayer.IsSelectable.ToString(),
                                    IsEditable = dimensionLayer.IsEditable.ToString(),
                                    RefreshRate = cimDimensionLayer.RefreshRate.ToString(),
                                    DefinitionQuery = dimensionLayer.DefinitionFilter.DefinitionExpression,
                                    MinScale = GetScaleValue(dimensionLayer.MinScale),
                                    MaxScale = GetScaleValue(dimensionLayer.MaxScale)
                                };
                                CSVLayoutList.Add(rec);
                                increaseLayerPos = true;

                                CIMDimensionLayer cimDimensionLayerDef = layer.GetDefinition() as CIMDimensionLayer;
                                if (cimDimensionLayerDef.EnableDisplayFilters)
                                {
                                    CIMDisplayFilter[] cimDisplayFilterChoices = cimDimensionLayerDef.DisplayFilterChoices;
                                    CIMDisplayFilter[] cimDisplayFilter = cimDimensionLayerDef.DisplayFilters;
                                    AddDisplayFiltersToList(rec, cimDisplayFilterChoices, cimDisplayFilter, ref CSVLayoutList);
                                }

                            }
                            else if (layer is UtilityNetworkLayer utilityNetworkLayer)
                            {
                                CSVLayout rec = new CSVLayout()
                                {
                                    LayerPos = layerPos.ToString(),
                                    LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                    GroupLayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                    LayerType = "Utility Network Layer",
                                    IsVisible = layer.IsVisible.ToString()
                                };
                                CSVLayoutList.Add(rec);

                                //Active Trace Configuration introduced in Utility Network version 5.
                                CIMUtilityNetworkLayer cimUtilityNetworkLayer = layer.GetDefinition() as CIMUtilityNetworkLayer;
                                CIMNetworkTraceConfiguration[] cimNetworkTraceConfigurations = cimUtilityNetworkLayer.ActiveTraceConfigurations;
                                if (cimNetworkTraceConfigurations != null)
                                {
                                    for (int j = 0; j < cimNetworkTraceConfigurations.Length; j++)
                                    {
                                        rec = new CSVLayout()
                                        {
                                            LayerPos = layerPos.ToString(),
                                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                            GroupLayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                            ActiveTraceConfiguration = Common.EncloseStringInDoubleQuotes(cimNetworkTraceConfigurations[j].Name)
                                        };
                                        CSVLayoutList.Add(rec);
                                    }
                                }
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
                                CSVLayoutList.Add(rec);
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
                                CSVLayoutList.Add(rec);
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
                                CSVLayoutList.Add(rec);
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
                                CSVLayoutList.Add(rec);
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
                                CSVLayoutList.Add(rec);
                                increaseLayerPos = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            CSVLayout rec = new CSVLayout()
                            {
                                LayerPos = layerPos.ToString(),
                                GroupLayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                LayerName = "Extract Error",
                                IsVisible = layer.IsVisible.ToString(),
                                LayerSource = ex.Message
                            };
                            CSVLayoutList.Add(rec);
                            increaseLayerPos = true;
                        }

                        //increment counter by 1
                        if (increaseLayerPos)
                            layerPos += 1;

                        increaseLayerPos = false;
                    }

                    //Standalone Tables
                    IReadOnlyList<StandaloneTable> standaloneTableList = MapView.Active.Map.StandaloneTables;
                    foreach (StandaloneTable standaloneTable in standaloneTableList)
                    {
                        CIMStandaloneTable cimStandaloneTable = standaloneTable.GetDefinition() as CIMStandaloneTable;
                        CIMExpressionInfo cimExpressionInfo = cimStandaloneTable.DisplayExpressionInfo;

                        //Primary Display Field
                        string displayField = cimStandaloneTable.DisplayField;
                        if (cimExpressionInfo != null)
                            displayField = cimExpressionInfo.Expression.Replace("\"", "'");  //double quotes messes up the delimeters in the CSV

                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            LayerName = Common.EncloseStringInDoubleQuotes(standaloneTable.Name),
                            LayerType = "Table",
                            LayerSource = standaloneTable.GetTable().GetPath().ToString(),
                            ClassName = standaloneTable.GetTable().GetName(),
                            DefinitionQuery = standaloneTable.DefinitionFilter.DefinitionExpression,
                            DisplayField = Common.EncloseStringInDoubleQuotes(displayField),
                        };
                        CSVLayoutList.Add(rec);


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
                                    CSVLayout popupRec = new CSVLayout()
                                    {
                                        LayerPos = rec.LayerPos,
                                        LayerName = rec.LayerName,
                                        PopupExpresssionName = cimStandaloneTable.PopupInfo.ExpressionInfos[i].Name,
                                        PopupExpresssionTitle = Common.EncloseStringInDoubleQuotes(cimStandaloneTable.PopupInfo.ExpressionInfos[i].Title.Replace("\"", "'")),
                                        PopupExpresssionVisible = popupExprVisibility.ToString(),
                                        PopupExpressionArcade = Common.EncloseStringInDoubleQuotes(cimStandaloneTable.PopupInfo.ExpressionInfos[i].Expression.Replace("\"", "'"))
                                    };
                                    CSVLayoutList.Add(popupRec);
                                }
                            }
                        }

                        layerPos += 1;
                    }

                    foreach (CSVLayout row in CSVLayoutList)
                    {
                        string output = Common.ExtractClassValuesToString(row, properties);
                        sw.WriteLine(output);
                    }

                    sw.Flush();
                    sw.Close();
                }
            });
        }

        private static void AddDisplayFiltersToList(CSVLayout parentRec, CIMDisplayFilter[] cimDisplayFilterChoices, CIMDisplayFilter[] cimDisplayFilter, ref List<CSVLayout> layerAttributeList)
        {
            //In Pro, there are 2 choices to set the Active Display Filters
            //option 1:  Manually 
            if (cimDisplayFilterChoices != null)
            {
                for (int j = 0; j < cimDisplayFilterChoices.Length; j++)
                {
                    CSVLayout rec = new CSVLayout()
                    {
                        LayerPos = parentRec.LayerPos,
                        GroupLayerName = parentRec.GroupLayerName,
                        DisplayFilterName = Common.EncloseStringInDoubleQuotes(cimDisplayFilterChoices[j].Name),
                        DisplayFilterExpresssion = Common.EncloseStringInDoubleQuotes(cimDisplayFilterChoices[j].WhereClause),
                    };
                    layerAttributeList.Add(rec);
                }
            }

            //option 2:  By Scale
            if (cimDisplayFilter != null)
            {
                for (int k = 0; k < cimDisplayFilter.Length; k++)
                {
                    CSVLayout rec = new CSVLayout()
                    {
                        LayerPos = parentRec.LayerPos,
                        GroupLayerName = parentRec.GroupLayerName,
                        DisplayFilterName = Common.EncloseStringInDoubleQuotes(cimDisplayFilter[k].Name),
                        MinScale = GetScaleValue(cimDisplayFilter[k].MinScale),
                        MaxScale = GetScaleValue(cimDisplayFilter[k].MaxScale)
                    };
                    layerAttributeList.Add(rec);
                }
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
            public string IsSubtypeGroupLayer { get; set; }
            public string GeometryType { get; set; }
            public string IsSelectable { get; set; }
            public string IsEditable { get; set; }
            public string RefreshRate { get; set; }
            public string ActiveTraceConfiguration { get; set; }
            public string DefinitionQuery { get; set; }
            public string DisplayFilterName { get; set; }
            public string DisplayFilterExpresssion { get; set; }
            public string MinScale { get; set; }
            public string MaxScale { get; set; }
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
            public string PopupExpresssionName { get; set; }
            public string PopupExpresssionTitle { get; set; }
            public string PopupExpresssionVisible { get; set; }
            public string PopupExpressionArcade { get; set; }
        }
    }
}
