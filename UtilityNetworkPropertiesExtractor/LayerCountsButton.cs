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
using ArcGIS.Core.Data;
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
    //For each layer and table in the map, it's underlying datasource is queried for it's record count.
    //The number of layers in the map will impact the execution duration.  
    internal class LayerCountsButton : Button
    {
        private static string _fileName = string.Empty;

        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Layer and Table Counts to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();

                await ExtractLayerCountAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Layer Counts");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractLayerCountAsync()
        {
            return QueuedTask.Run(() =>
            {
                Common.CreateOutputDirectory();

                string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _fileName = string.Format("{0}_{1}_LayerCounts.csv", dateFormatted, Common.GetActiveMapName());
                string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                using (StreamWriter sw = new StreamWriter(outputFile))
                {
                    //Header information
                    sw.WriteLine(DateTime.Now + "," + "Layer and Table Counts");
                    sw.WriteLine();
                    sw.WriteLine("Project," + Project.Current.Path);
                    sw.WriteLine("Map," + Common.GetActiveMapName());
                    sw.WriteLine("Layers," + MapView.Active.Map.GetLayersAsFlattenedList().OfType<Layer>().Count());
                    sw.WriteLine("Standalone Tables," + MapView.Active.Map.StandaloneTables.Count);
                    int tablesInGroupLayers = Common.GetCountOfTablesInGroupLayers();
                    if (tablesInGroupLayers > 0)
                        sw.WriteLine("Tables in Group Layers," + Common.GetCountOfTablesInGroupLayers());
                    sw.WriteLine();

                    //Get all properties defined in the class.  This will be used to generate the CSV file
                    CSVLayout emptyRec = new CSVLayout();
                    PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                    //Write column headers based on properties in the class
                    string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                    sw.WriteLine(columnHeader);

                    List<CSVLayout> CSVLayoutList = new List<CSVLayout>();

                    int layerPos = 1;
                    string prevGroupLayerName = string.Empty;
                    string layerContainer = string.Empty;
                    long recordCount = 0;
                    long sum = 0;
                    string definitionQuery = string.Empty;
                    string groupLayerName = string.Empty;
                    string layerType = string.Empty;


                    List<Layer> layerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<Layer>().ToList();
                    foreach (Layer layer in layerList)
                    {
                        CSVLayout csvLayout = new CSVLayout();
                        definitionQuery = string.Empty;
                        recordCount = 0;

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
                        {   //In the TOC, these 4 layers will have child layers
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

                        csvLayout.LayerPos = layerPos.ToString();
                        csvLayout.LayerType = layerType;
                        csvLayout.LayerName = Common.EncloseStringInDoubleQuotes(layer.Name);
                        csvLayout.GroupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer);

                        if (layer is FeatureLayer featureLayer)
                        {
                            CIMFeatureLayer cimFeatureLayerDef = layer.GetDefinition() as CIMFeatureLayer;
                            CIMFeatureTable cimFeatureTable = cimFeatureLayerDef.FeatureTable;
                            FeatureClass featureClass = featureLayer.GetFeatureClass();

                            if (featureClass != null)
                            {
                                QueryFilter queryFilter = new QueryFilter();

                                if (cimFeatureTable.UseSubtypeValue)
                                {
                                    string subtypeField = featureClass.GetDefinition().GetSubtypeField();
                                    queryFilter.WhereClause = $"{subtypeField} = {cimFeatureTable.SubtypeValue}";
                                    definitionQuery = queryFilter.WhereClause;
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty(featureLayer.DefinitionQuery))
                                    {
                                        queryFilter.WhereClause = featureLayer.DefinitionQuery;
                                        definitionQuery = queryFilter.WhereClause;
                                    }
                                }

                                if (!string.IsNullOrEmpty(queryFilter.WhereClause))
                                    recordCount = featureClass.GetCount(queryFilter);
                                else
                                    recordCount = featureClass.GetCount();
                            }
                        }
                        else if (layer is BasicFeatureLayer basicFeatureLayer)  //Annotation or Dimensions layer
                        {
                            recordCount = basicFeatureLayer.GetTable().GetCount();
                        }
                        else if (layer is GroupLayer)
                        {
                            csvLayout.GroupLayerName = csvLayout.LayerName;
                            csvLayout.LayerName = string.Empty;
                            recordCount = 0;
                        }
                        else if (layer is UtilityNetworkLayer)
                        {
                            csvLayout.GroupLayerName = csvLayout.LayerName;
                            recordCount = 0;
                        }
                        else if (layer is SubtypeGroupLayer subtypeGroupLayer)
                        {
                            csvLayout.GroupLayerName = csvLayout.LayerName;
                            csvLayout.LayerName = string.Empty;
                            recordCount = 0;
                        }

                        csvLayout.DefinitionQuery = definitionQuery;
                        csvLayout.RecordCount = Common.EncloseStringInDoubleQuotes($"{recordCount:n0}");

                        CSVLayoutList.Add(csvLayout);

                        sum += recordCount;
                        layerPos += 1;
                    }

                    //Standalone Tables
                    IReadOnlyList<StandaloneTable> standaloneTableList = MapView.Active.Map.StandaloneTables;
                    InterrogateStandaloneTables(standaloneTableList, string.Empty, ref CSVLayoutList, ref layerPos, ref sum);

                    //Tables in Group Layers
                    //  Will show up at the bottom of the CSV.  This isn't quite right.
                    if (tablesInGroupLayers > 0)
                    {
                        List<GroupLayer> groupLayerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<GroupLayer>().ToList();
                        foreach (GroupLayer groupLayer in groupLayerList)
                        {
                            if (groupLayer.StandaloneTables.Count > 0)
                                InterrogateStandaloneTables(groupLayer.StandaloneTables, groupLayer.Name, ref CSVLayoutList, ref layerPos, ref sum);
                        }
                    }

                    CSVLayoutList.Add(emptyRec);

                    //Sum all the records
                    CSVLayout summary = new CSVLayout()
                    {
                        LayerName = "Total Records",
                        RecordCount = Common.EncloseStringInDoubleQuotes($"{sum:n0}")
                    };

                    CSVLayoutList.Add(summary);

                    //Write entries to CSV File
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

        private static void InterrogateStandaloneTables(IReadOnlyList<StandaloneTable> standaloneTableList, string groupLayerName, ref List<CSVLayout> CSVLayoutList, ref int layerPos, ref long sum)
        {
            string layerType = "Standalone Table";
            if (! string.IsNullOrEmpty(groupLayerName))
                layerType = "Table in Group Layer";

            long recordCount;
            string definitionQuery;
            foreach (StandaloneTable standaloneTable in standaloneTableList)
            {
                recordCount = 0;
                definitionQuery = string.Empty;
                if (!string.IsNullOrEmpty(standaloneTable.DefinitionQuery))
                {
                    QueryFilter queryFilter = new QueryFilter() { WhereClause = standaloneTable.DefinitionQuery };
                    recordCount = standaloneTable.GetTable().GetCount(queryFilter);
                    definitionQuery = queryFilter.WhereClause;
                }
                else
                    recordCount = standaloneTable.GetTable().GetCount();

                CSVLayout rec = new CSVLayout()
                {
                    LayerPos = layerPos.ToString(),
                    LayerType = layerType,
                    LayerName = Common.EncloseStringInDoubleQuotes(standaloneTable.Name),
                    GroupLayerName = groupLayerName,
                    DefinitionQuery = definitionQuery,
                    RecordCount = Common.EncloseStringInDoubleQuotes($"{recordCount:n0}")
                };
                CSVLayoutList.Add(rec);

                sum += recordCount;
                layerPos += 1;
            }
        }

        private class CSVLayout
        {
            public string LayerPos { get; set; }
            public string LayerType { get; set; }
            public string GroupLayerName { get; set; }
            public string LayerName { get; set; }
            public string DefinitionQuery { get; set; }
            public string RecordCount { get; set; }
        }
    }
}