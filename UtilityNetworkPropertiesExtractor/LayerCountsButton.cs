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
                string outputFile = Common.BuildCsvNameContainingMapName("LayerCounts");
                using (StreamWriter sw = new StreamWriter(outputFile))
                {
                    
                    List<Layer> layerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<Layer>().ToList();
                    IReadOnlyList<StandaloneTable> standaloneTableList = MapView.Active.Map.GetStandaloneTablesAsFlattenedList();

                    //Header information
                    Common.WriteHeaderInfoForMap(sw, "Layer and Table Counts");
                    sw.WriteLine("Layers," + layerList.Count);
                    sw.WriteLine("Standalone Tables," + standaloneTableList.Count);
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
                    bool addToCsvLayoutList = false;


                    IReadOnlyList<MapMember> mapMemberList = MapView.Active.Map.GetMapMembersAsFlattenedList();
                    foreach (MapMember mapMember in mapMemberList)
                    {
                        Layer layer;
                        CSVLayout csvLayout = new CSVLayout();
                        definitionQuery = string.Empty;
                        recordCount = 0;
                        addToCsvLayoutList = true;

                        if (mapMember is Layer)
                        {
                            layer = mapMember as Layer;
                            layerContainer = layer.Parent.ToString();
                            if (layerContainer != MapView.Active.Map.Name) // Group layer
                            {
                                if (layerContainer != prevGroupLayerName)
                                    prevGroupLayerName = layerContainer;
                            }
                            else
                                layerContainer = string.Empty;
                        }

                        layerType = Common.GetLayerTypeDescription(mapMember);
                        switch (layerType)
                        {   //In the TOC, these 4 layers will have child layers
                            case "Annotation Layer":
                            case "Group Layer":
                            case "Subtype Group Layer":
                            case "Utility Network Layer":
                                groupLayerName = Common.EncloseStringInDoubleQuotes(mapMember.Name);
                                break;
                            default:
                                groupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer);
                                break;
                        }

                        csvLayout.LayerPos = layerPos.ToString();
                        csvLayout.LayerType = layerType;
                        csvLayout.LayerName = Common.EncloseStringInDoubleQuotes(mapMember.Name);
                        csvLayout.GroupLayerName = Common.EncloseStringInDoubleQuotes(layerContainer);

                        if (mapMember is FeatureLayer featureLayer)
                        {
                            CIMFeatureLayer cimFeatureLayerDef = featureLayer.GetDefinition() as CIMFeatureLayer;
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
                        else if (mapMember is BasicFeatureLayer basicFeatureLayer)  //Annotation or Dimensions layer
                        {
                            recordCount = basicFeatureLayer.GetTable().GetCount();
                        }
                        else if (mapMember is GroupLayer)
                        {
                            csvLayout.GroupLayerName = csvLayout.LayerName;
                            csvLayout.LayerName = string.Empty;
                            recordCount = 0;
                        }
                        else if (mapMember is UtilityNetworkLayer)
                        {
                            csvLayout.GroupLayerName = csvLayout.LayerName;
                            recordCount = 0;
                        }
                        else if (mapMember is SubtypeGroupLayer subtypeGroupLayer)
                        {
                            csvLayout.GroupLayerName = csvLayout.LayerName;
                            csvLayout.LayerName = string.Empty;
                            recordCount = 0;
                        }
                        else if (mapMember is StandaloneTable standaloneTable)
                        {
                            if (standaloneTable.Parent.ToString().Equals(MapView.Active.Map.Name))
                                layerContainer = "";
                            else
                                layerContainer = standaloneTable.Parent.ToString();

                            InterrogateStandaloneTables(standaloneTable, layerContainer, ref CSVLayoutList, ref layerPos, ref sum);

                            //check if subtype group table
                            if (standaloneTable is SubtypeGroupTable subtypeGroupTable)
                            {
                                IReadOnlyList<StandaloneTable> sgtList = subtypeGroupTable.StandaloneTables;
                                foreach (StandaloneTable sgt in sgtList)
                                    InterrogateStandaloneTables(sgt, standaloneTable.Name, ref CSVLayoutList, ref layerPos, ref sum);
                                // layerPos = InterrogateStandaloneTable(sgt, layerPos, mapMember.Name, ref csvLayoutList, ref popupLayoutList, ref definitionQueryLayout);

                            }

                            //Since already added Table info to CsvLayoutList, don't do it again.
                            addToCsvLayoutList = false;
                        }

                        //Assign record to the list
                        if (addToCsvLayoutList)
                        {
                            csvLayout.DefinitionQuery = definitionQuery;
                            csvLayout.RecordCount = Common.EncloseStringInDoubleQuotes($"{recordCount:n0}");

                            CSVLayoutList.Add(csvLayout);

                            sum += recordCount;
                            layerPos += 1;
                        }
                    }

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

        private static void InterrogateStandaloneTables(StandaloneTable standaloneTable, string groupLayer, ref List<CSVLayout> CSVLayoutList, ref int layerPos, ref long sum)
        {
            long recordCount;
            string definitionQuery;
            

            string layerType = Common.GetLayerTypeDescription(standaloneTable);
            string layerName = Common.EncloseStringInDoubleQuotes(standaloneTable.Name);
            definitionQuery = string.Empty;
            QueryFilter queryFilter = new QueryFilter();
            Table table = standaloneTable.GetTable();

            if (standaloneTable is SubtypeGroupTable) // exclude the SubtypeGroupTable from the report
            {
                groupLayer = Common.EncloseStringInDoubleQuotes(standaloneTable.Name);
                layerType = "Subtype Group Table";
                layerName = string.Empty;
                definitionQuery = string.Empty;
                recordCount = 0;
            }
            else
            {
                if (standaloneTable.IsSubtypeTable)
                {
                    string subtypeField = table.GetDefinition().GetSubtypeField();
                    queryFilter.WhereClause = $"{subtypeField} = {standaloneTable.SubtypeValue}";
                    definitionQuery = queryFilter.WhereClause;
                }
                else
                {
                    if (!string.IsNullOrEmpty(standaloneTable.DefinitionQuery))
                    {
                        queryFilter.WhereClause = standaloneTable.DefinitionQuery;
                        definitionQuery = queryFilter.WhereClause;
                    }
                }

                if (!string.IsNullOrEmpty(queryFilter.WhereClause))
                    recordCount = table.GetCount(queryFilter);
                else
                    recordCount = table.GetCount();
            }

            CSVLayout rec = new CSVLayout()
            {
                LayerPos = layerPos.ToString(),
                LayerType = layerType,
                LayerName = layerName,
                GroupLayerName = groupLayer,
                DefinitionQuery = definitionQuery,
                RecordCount = Common.EncloseStringInDoubleQuotes($"{recordCount:n0}")
            };
            
            CSVLayoutList.Add(rec);
            layerPos += 1;
            sum += recordCount;
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