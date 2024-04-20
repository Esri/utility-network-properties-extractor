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
    internal class SymbolScalesButton : Button
    {
        private static string _fileName = string.Empty;

        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Symbol Scales to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();

                await ExtractSymbolScalesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Symbol Scales");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractSymbolScalesAsync()
        {
            return QueuedTask.Run(() =>
            {
                Common.CreateOutputDirectory();

                string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _fileName = string.Format("{0}_{1}_SymbolScales.csv", dateFormatted, Common.GetActiveMapName());
                string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                using (StreamWriter sw = new StreamWriter(outputFile))
                {
                    //Header information
                    sw.WriteLine(DateTime.Now + "," + "Symbol Scales");
                    sw.WriteLine();
                    sw.WriteLine("Project," + Project.Current.Path);
                    sw.WriteLine("Map," + Common.GetActiveMapName());
                    sw.WriteLine("Layers," + MapView.Active.Map.GetLayersAsFlattenedList().OfType<Layer>().Count());
                    sw.WriteLine();

                    //Get all properties defined in the class.  This will be used to generate the CSV file
                    CSVLayout emptyRec = new CSVLayout();
                    PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                    //Write column headers based on properties in the class
                    string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                    sw.WriteLine(columnHeader);

                    List<CSVLayout> CSVLayoutList = new List<CSVLayout>();

                    int layerPos = 1;
                    string groupLayerName = string.Empty;
                    string prevGroupLayerName = string.Empty;
                    string layerContainer = string.Empty;
                    string layerType = string.Empty;

                    List<Layer> layerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<Layer>().ToList();
                    foreach (Layer layer in layerList)
                    {
                        //Determine if in a group layer
                        layerContainer = layer.Parent.ToString();
                        if (layerContainer != MapView.Active.Map.Name) // Group layer
                        {
                            if (layerContainer != prevGroupLayerName)
                                prevGroupLayerName = layerContainer;
                        }
                        else
                            layerContainer = string.Empty;

                        layerType = Common.GetLayerTypeDescription(layer);
                        switch (layerType)
                        {
                            case "Annotation Layer":
                            case "Group Layer":
                            case "Subtype Group Layer":
                            case "Utility Network Layer":
                                groupLayerName = Common.EncloseStringInDoubleQuotes(layer.Name);
                                break;
                            default:
                                groupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer);
                                break;
                        }

                        CSVLayout csvLayout = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            LayerType = layerType,
                            GroupLayerName = groupLayerName,
                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            LayerMinScale = GetScaleValue(layer.MinScale),
                            LayerMaxScale = GetScaleValue(layer.MaxScale)
                        };

                        if (layerType == "Feature Layer")
                        {
                            //Add Layer details to the list                            
                            CIMFeatureLayer cimFeatureLayerDef = layer.GetDefinition() as CIMFeatureLayer;
                            csvLayout.Renderer = cimFeatureLayerDef.Renderer.ToString().Replace("ArcGIS.Core.CIM.", "");
                            CSVLayoutList.Add(csvLayout);

                            //Based on renderer type, get the symbol scales.
                            //Simple Renderer
                            if (cimFeatureLayerDef.Renderer is CIMSimpleRenderer cimSimpleRenderer)
                            {
                                csvLayout = new CSVLayout()
                                {
                                    LayerPos = layerPos.ToString(),
                                    GroupLayerName = groupLayerName,
                                    LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                    SymbolMinScale = GetScaleValue(cimSimpleRenderer.Symbol.MinScale),
                                    SymbolMaxScale = GetScaleValue(cimSimpleRenderer.Symbol.MaxScale)
                                };

                                if (!string.IsNullOrEmpty(cimSimpleRenderer.Label))
                                    csvLayout.SymbolLabel = cimSimpleRenderer.Label;
                                else
                                    csvLayout.SymbolLabel = "<blank>";

                                CSVLayoutList.Add(csvLayout);
                            }

                            //Unqiue Renderer
                            else if (cimFeatureLayerDef.Renderer is CIMUniqueValueRenderer uniqueRenderer)
                            {
                                CIMUniqueValueGroup[] cimUniqueValueGroups = uniqueRenderer.Groups;
                                foreach (CIMUniqueValueGroup cimUniqueValueGroup in cimUniqueValueGroups)
                                {
                                    CIMUniqueValueClass[] cimUniqueValueClasses = cimUniqueValueGroup.Classes;
                                    foreach (CIMUniqueValueClass cimUniqueValueClass in cimUniqueValueClasses)
                                    {
                                        csvLayout = new CSVLayout()
                                        {
                                            LayerPos = layerPos.ToString(),
                                            GroupLayerName = groupLayerName,
                                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                            SymbolMinScale = GetScaleValue(cimUniqueValueClass.Symbol.MinScale),
                                            SymbolMaxScale = GetScaleValue(cimUniqueValueClass.Symbol.MaxScale)
                                        };

                                        if (!string.IsNullOrEmpty(cimUniqueValueClass.Label))
                                            csvLayout.SymbolLabel = Common.EncloseStringInDoubleQuotes(cimUniqueValueClass.Label);
                                        else
                                            csvLayout.SymbolLabel = "<blank>";

                                        CSVLayoutList.Add(csvLayout);
                                    }
                                }
                            }

                            //Heat Map
                            else if (cimFeatureLayerDef.Renderer is CIMHeatMapRenderer cimHeatMapRenderer)
                            {
                                List<string> heatMapLabels = new List<string> { "Sparse", "Dense" };
                                foreach (string heatMapLabel in heatMapLabels)
                                {
                                    csvLayout = new CSVLayout()
                                    {
                                        LayerPos = layerPos.ToString(),
                                        GroupLayerName = groupLayerName,
                                        LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                        SymbolLabel = heatMapLabel,
                                        SymbolMinScale = "N/A",
                                        SymbolMaxScale = "N/A"
                                    };
                                    CSVLayoutList.Add(csvLayout);
                                }
                            }

                            //Graduated Colors
                            else if (cimFeatureLayerDef.Renderer is CIMClassBreaksRenderer cimClassBreaksRenderer)
                            {
                                foreach (CIMClassBreak cimClassBreak in cimClassBreaksRenderer.Breaks)
                                {
                                    csvLayout = new CSVLayout()
                                    {
                                        LayerPos = layerPos.ToString(),
                                        GroupLayerName = groupLayerName,
                                        LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                                        SymbolLabel = Common.EncloseStringInDoubleQuotes(cimClassBreak.Label),
                                        SymbolMinScale = GetScaleValue(cimClassBreak.Symbol.MinScale),
                                        SymbolMaxScale = GetScaleValue(cimClassBreak.Symbol.MaxScale)
                                    };
                                    CSVLayoutList.Add(csvLayout);
                                }
                            }
                        }
                        else
                            CSVLayoutList.Add(csvLayout);

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

        private static string GetScaleValue(double scale)
        {
            if (scale == 0)
                return "None";  // In Pro, when there is no scale set, the value is null.  Thru the SDK, it was showing 0.
            else
                return scale.ToString();
        }

        private class CSVLayout
        {
            public string LayerPos { get; set; }
            public string LayerType { get; set; }
            public string GroupLayerName { get; set; }
            public string LayerName { get; set; }
            public string LayerMaxScale { get; set; }
            public string LayerMinScale { get; set; }
            public string Renderer { get; set; }
            public string SymbolLabel { get; set; }
            public string SymbolMaxScale { get; set; }
            public string SymbolMinScale { get; set; }
        }
    }
}