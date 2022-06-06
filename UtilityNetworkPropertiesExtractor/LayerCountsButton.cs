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
                MessageBox.Show(ex.Message, "Extract Trace Configuration");
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
                _fileName = string.Format("{0}_{1}_LayerCounts.csv", dateFormatted, Common.GetProProjectName());
                string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                using (StreamWriter sw = new StreamWriter(outputFile))
                {
                    //Header information
                    sw.WriteLine(DateTime.Now + "," + "Layer and Table Counts");
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

                    List<CSVLayout> CSVLayoutList = new List<CSVLayout>();

                    int layerPos = 1;
                    string prevGroupLayerName = string.Empty;
                    string layerContainer = string.Empty;
                    int recordCount = 0;
                    int sum = 0;
                    string definitionQuery = string.Empty;

                    List<Layer> layerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<Layer>().ToList();
                    foreach (Layer layer in layerList)
                    {
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
                                                
                        if (layer is FeatureLayer featureLayer)
                        {
                            CIMFeatureLayer cimFeatureLayerDef = layer.GetDefinition() as CIMFeatureLayer;
                            CIMFeatureTable cimFeatureTable = cimFeatureLayerDef.FeatureTable;
                            FeatureClass featureClass = featureLayer.GetFeatureClass();
                            QueryFilter queryFilter = new QueryFilter();

                            if (cimFeatureTable.UseSubtypeValue)
                            {
                                string subtypeField = featureClass.GetDefinition().GetSubtypeField();
                                queryFilter.WhereClause = $"{subtypeField} = {cimFeatureTable.SubtypeValue}";
                                definitionQuery = queryFilter.WhereClause;
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(featureLayer.DefinitionFilter.DefinitionExpression))
                                {
                                    queryFilter.WhereClause = featureLayer.DefinitionFilter.DefinitionExpression;
                                    definitionQuery = queryFilter.WhereClause;
                                }
                            }

                            if (!string.IsNullOrEmpty(queryFilter.WhereClause))
                                recordCount = featureClass.GetCount(queryFilter);
                            else
                                recordCount = featureClass.GetCount();
                        }
                        else if (layer is BasicFeatureLayer basicFeatureLayer)  //Annotation or Dimensions layer
                        {
                            recordCount = basicFeatureLayer.GetTable().GetCount();
                        }
                        else if (layer is GroupLayer groupLayer)
                        {
                            layerContainer = groupLayer.Name;
                            recordCount = 0;
                        }
                        
                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            LayerName = Common.EncloseStringInDoubleQuotes(layer.Name),
                            GroupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer),
                            DefinitionQuery = definitionQuery,
                            RecordCount = Common.EncloseStringInDoubleQuotes($"{recordCount:n0}")
                        };
                        CSVLayoutList.Add(rec);

                        sum += recordCount;
                        layerPos += 1;
                    }

                    //Standalone Tables
                    IReadOnlyList<StandaloneTable> standaloneTableList = MapView.Active.Map.StandaloneTables;
                    foreach (StandaloneTable standaloneTable in standaloneTableList)
                    {
                        definitionQuery = string.Empty;
                        if (!string.IsNullOrEmpty(standaloneTable.DefinitionFilter.DefinitionExpression))
                        {
                            QueryFilter queryFilter = new QueryFilter() { WhereClause = standaloneTable.DefinitionFilter.DefinitionExpression };
                            recordCount = standaloneTable.GetTable().GetCount(queryFilter);
                            definitionQuery = queryFilter.WhereClause;
                        }
                        else
                            recordCount = standaloneTable.GetTable().GetCount();

                        CSVLayout rec = new CSVLayout()
                        {
                            LayerPos = layerPos.ToString(),
                            GroupLayerName = "Standalone Tables",
                            LayerName = Common.EncloseStringInDoubleQuotes(standaloneTable.Name),
                            DefinitionQuery = definitionQuery,
                            RecordCount = Common.EncloseStringInDoubleQuotes($"{recordCount:n0}")
                        };
                        CSVLayoutList.Add(rec);

                        sum += recordCount;
                        layerPos += 1;
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

        private class CSVLayout
        {
            public string LayerPos { get; set; }
            public string GroupLayerName { get; set; }
            public string LayerName { get; set; }
            public string DefinitionQuery { get; set; }
            public string RecordCount { get; set; }
        }
    }
}