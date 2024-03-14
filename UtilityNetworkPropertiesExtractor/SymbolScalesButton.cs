using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Layouts.Utilities;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
                    sw.WriteLine("Coordinate System," + MapView.Active.Map.SpatialReference.Name);
                    sw.WriteLine("Map Units," + MapView.Active.Map.SpatialReference.Unit);
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
                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name)
                        };


                        ////Get layer min and max scales for all layers
                        //if (layer.MaxScale == 0 && layer.MinScale == 0)
                        //    scaleRec.LayerRange = "Not Set";
                        //else
                        //    scaleRec.LayerRange = GetScaleValue(layer.MaxScale) + " - " + GetScaleValue(layer.MinScale);

                        ////Clear our layerName for these types of layers
                        //if (layerType == "Group Layer" || layerType == "Subtype Group Layer")
                        //    scaleRec.LayerName = string.Empty;

                        ////Get Layer and Symbol min & max scales
                        if (layerType == "Feature Layer")
                        {
                            csvLayout.LayerMinScale = GetScaleValue(layer.MinScale);
                            csvLayout.LayerMaxScale = GetScaleValue(layer.MaxScale);
                            CSVLayoutList.Add(csvLayout);

                           
                            CIMFeatureLayer cimFeatureLayerDef = layer.GetDefinition() as CIMFeatureLayer;
                            if (cimFeatureLayerDef.Renderer is CIMSimpleRenderer)
                            {
                                var primarySymbology = "Single Symbol";
                            }
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
                                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name)
                                        };

                                        csvLayout.SymbolLabel = cimUniqueValueClass.Label;
                                        csvLayout.SymbolMinScale = GetScaleValue(cimUniqueValueClass.Symbol.MinScale);
                                        csvLayout.SymbolMaxScale = GetScaleValue(cimUniqueValueClass.Symbol.MaxScale);
                                        CSVLayoutList.Add(csvLayout);

                                        //CIMUniqueValue[] cimUniqueValues = cimUniqueValueClass.Values;
                                        //foreach (CIMUniqueValue cimUniqueValue in cimUniqueValues)
                                        //{
                                        //    var fieldValues = cimUniqueValue.FieldValues[0];
                                        //    var blah = fieldValues;
                                        //}
                                    }
                                }
                            }


                            //CSVLayoutList.Add(csvLayout);
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
            public string SymbolLabel { get; set; }
            public string SymbolMaxScale { get; set; }
            public string SymbolMinScale { get; set; }
        }
    }
}