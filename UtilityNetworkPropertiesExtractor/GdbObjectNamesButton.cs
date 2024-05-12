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
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
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
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting GDB Object Info to:\n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractGdbObjectNamesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract GDB Object Info Info");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractGdbObjectNamesAsync()
        {
            return QueuedTask.Run(() =>
            {
                List<DataSourceInMap> dataSourceInMapList = DataSourcesInMapHelper.GetDataSourcesInMap();
                foreach (DataSourceInMap dataSourceInMap in dataSourceInMapList)
                {
                    //Skip over shapefiles  
                    if (dataSourceInMap.WorkspaceFactory == WorkspaceFactory.Shapefile.ToString() )
                        continue;

                    using (Geodatabase geodatabase = dataSourceInMap.Geodatabase)
                    {
                        string outputFile = Common.BuildCsvName("GdbObjectNames", dataSourceInMap.NameForCSV);
                        using (StreamWriter sw = new StreamWriter(outputFile))
                        {
                            //Header information
                            Common.WriteHeaderInfoForGeodatabase(sw, dataSourceInMap, "GDB Object Names");

                            //Get all properties defined in the class.  This will be used to generate the CSV file
                            CSVLayout emptyRec = new CSVLayout();
                            PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                            //Write column headers based on properties in the class
                            string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                            sw.WriteLine(columnHeader);

                            List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                            List<DatasetType> datasetTypeList = new List<DatasetType> 
                                                               {DatasetType.UtilityNetwork,
                                                                DatasetType.FeatureDataset,
                                                                DatasetType.FeatureClass,
                                                                DatasetType.Table,
                                                                DatasetType.RelationshipClass,
                                                                DatasetType.AttributedRelationshipClass};

                            if (dataSourceInMap.WorkspaceFactory == WorkspaceFactory.FeatureService.ToString())
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

                                try
                                {

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
                                                NetworkSource assemblyNetworkSource = un.GetDefinition().GetNetworkSources().Where(x => x.UsageType == SourceUsageType.Assembly).First();
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
                                catch (Exception ex)  // suppress error message
                                {
                                    if (ex.HResult != -2146233088) // No database permissions to perform the operation.
                                        MessageBox.Show(ex.Message);
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