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
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class GdbObjectNamesButton : Button
    {
        private static string _fileName = string.Empty;

        protected async override void OnClick()
        {
            try
            {
                await ExtractGdbObjectNamesAsync();
                MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract GDB Object Names");
            }
        }

        public static Task ExtractGdbObjectNamesAsync()
        {
            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayer);
                if (utilityNetwork == null)
                    featureLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().First();

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayer);

                using (Geodatabase geodatabase = featureLayer.GetTable().GetDatastore() as Geodatabase)
                {
                    Common.CreateOutputDirectory();
                    string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    _fileName = string.Format("{0}_{1}_GdbObjectNames.csv", dateFormatted, reportHeaderInfo.MapName);
                    string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                    using (StreamWriter sw = new StreamWriter(outputFile))
                    {
                        //Header information
                        UtilityNetworkDefinition utilityNetworkDefinition = null;
                        if (utilityNetwork != null)
                            utilityNetworkDefinition = utilityNetwork.GetDefinition();

                        Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "GDB Object Names");

                        //Get all properties defined in the class.  This will be used to generate the CSV file
                        CSVLayout emptyRec = new CSVLayout();
                        PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                        //Write column headers based on properties in the class
                        string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                        sw.WriteLine(columnHeader);

                        List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                        List<DatasetType> datasetTypeList = new List<DatasetType>
                                                                    { DatasetType.UtilityNetwork,
                                                                      DatasetType.FeatureDataset,
                                                                      DatasetType.FeatureClass,
                                                                      DatasetType.Table,
                                                                      DatasetType.RelationshipClass,
                                                                      DatasetType.AttributedRelationshipClass};

                        if (reportHeaderInfo.SourceType == Common.DatastoreTypeDescriptions.FeatureService)
                            datasetTypeList.Remove(DatasetType.FeatureDataset); // Exception raised with this dataset type on a featureservice

                        foreach (DatasetType datasetType in datasetTypeList)
                        {
                            IReadOnlyList<Definition> definitionList = null;
                            switch (datasetType)
                            {
                                case DatasetType.UtilityNetwork:
                                    definitionList = geodatabase.GetDefinitions<UtilityNetworkDefinition>();
                                    break;

                                case DatasetType.FeatureDataset:
                                    definitionList = geodatabase.GetDefinitions<FeatureDatasetDefinition>();
                                    break;

                                case DatasetType.FeatureClass:
                                    definitionList = geodatabase.GetDefinitions<FeatureClassDefinition>();
                                    break;

                                case DatasetType.Table:
                                    definitionList = geodatabase.GetDefinitions<TableDefinition>();
                                    break;

                                case DatasetType.RelationshipClass:
                                    definitionList = geodatabase.GetDefinitions<RelationshipClassDefinition>();
                                    break;

                                case DatasetType.AttributedRelationshipClass:
                                    definitionList = geodatabase.GetDefinitions<AttributedRelationshipClassDefinition>();
                                    break;
                            }

                            //Loop through each object in the definitionList and determine it's FeatureDataset before writing it to the CSV file
                            foreach (Definition definition in definitionList)
                            {
                                string featureDatasetName = string.Empty;

                                //Determine the feature dataset name (if applicable) for the featureclass
                                if (datasetType == DatasetType.FeatureClass)
                                {
                                    using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(definition.GetName()))
                                    {
                                        FeatureDataset featureDataset = featureClass.GetFeatureDataset();
                                        if (featureDataset != null)
                                            featureDatasetName = featureDataset.GetName();
                                    }
                                }

                                //Determine the feature dataset name (if applicable) for the RelationshipClass
                                else if (datasetType == DatasetType.RelationshipClass)
                                {
                                    using (RelationshipClass relationshipClass = geodatabase.OpenDataset<RelationshipClass>(definition.GetName()))
                                    {
                                        FeatureDataset featureDataset = relationshipClass.GetFeatureDataset();
                                        if (featureDataset != null)
                                            featureDatasetName = featureDataset.GetName();
                                    }
                                }

                                //Determine the feature dataset name for the Utility Network object
                                else if (datasetType == DatasetType.UtilityNetwork)
                                {
                                    using (UtilityNetwork un = geodatabase.OpenDataset<UtilityNetwork>(definition.GetName()))
                                    {
                                        NetworkSource assemblyNetworkSource = utilityNetworkDefinition.GetNetworkSources().Where(x => x.UsageType == SourceUsageType.Assembly).First();
                                        FeatureClass assemblyFeatureClass = un.GetTable(assemblyNetworkSource) as FeatureClass;
                                        featureDatasetName = assemblyFeatureClass.GetFeatureDataset()?.GetName();
                                    }
                                }

                                CSVLayout rec = new CSVLayout()
                                {
                                    ObjectType = definition.DatasetType.ToString(),
                                    ObjectName = definition.GetName(),
                                    FeatureDataset = featureDatasetName
                                };
                                csvLayoutList.Add(rec);
                            }
                        }

                        //Write body of CSV
                        foreach (CSVLayout row in csvLayoutList.OrderBy(x => x.ObjectType).ThenBy(x => x.ObjectName))
                        {
                            string output = Common.ExtractClassValuesToString(row, properties);
                            sw.WriteLine(output);
                        }

                        sw.Flush();
                        sw.Close();
                    }
                }
            });
        }

        private class CSVLayout
        {
            public string ObjectType { get; set; }
            public string ObjectName { get; set; }
            public string FeatureDataset { get; set; }
        }
    }
}