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
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class ContingentValuesButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Contingent Value CSV(s) to:\n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractContingentValuesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Contingent Values");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static async Task ExtractContingentValuesAsync()
        {
            await QueuedTask.Run(async () =>
            {
                Dictionary<string, TableAndDataSource> tablesDict = new Dictionary<string, TableAndDataSource>();

                //If Subtype Group layers are in the map, will have multiple layers pointing to same source featureclass
                //Populate Dictionary of distinct featureclasses
                IEnumerable<FeatureLayer> featureLayerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>();
                foreach (FeatureLayer featureLayer in featureLayerList)
                {
                    DataSourceInMap dataSourcesInMap = DataSourcesInMapHelper.GetDataSourceOfLayerForDatabaseGPToolUsage(featureLayer);
                    if (dataSourcesInMap != null)
                    {
                        Table table = featureLayer.GetTable();

                        TableAndDataSource tableAndDataSource = new TableAndDataSource()
                        {
                            DataSource = dataSourcesInMap.NameForCSV,
                            Table = table
                        };

                        if (!tablesDict.ContainsKey(table.GetName()))
                            tablesDict.Add(table.GetName(), tableAndDataSource);
                    }
                }

                //Standalone Tables
                IReadOnlyList<StandaloneTable> standaloneTableList = MapView.Active.Map.StandaloneTables;
                foreach (StandaloneTable standaloneTable in standaloneTableList)
                {
                    DataSourceInMap dataSourcesInMap = DataSourcesInMapHelper.GetDataSourceOfLayerForDatabaseGPToolUsage(standaloneTable);
                    if (dataSourcesInMap != null)
                    {
                        Table table = standaloneTable.GetTable();

                        TableAndDataSource tableAndDataSource = new TableAndDataSource()
                        {
                            DataSource = dataSourcesInMap.NameForCSV,
                            Table = table
                        };

                        if (!tablesDict.ContainsKey(table.GetName()))
                            tablesDict.Add(table.GetName(), tableAndDataSource);
                    }
                }

                //Execute GP for each table in the dictionary
                foreach (KeyValuePair<string, TableAndDataSource> pair in tablesDict)
                {
                    TableAndDataSource tableAndDataSource = pair.Value;

                    string fcName = pair.Key;
                    int pos = pair.Key.LastIndexOf(".");

                    if (pos != -1) // strip off schema and owner of Featureclass Name (if exists).  Ex:  meh.unadmin.ElectricDevice
                        fcName = pair.Key.Substring(pos + 1);

                    string cvGroupOutputFile = Common.BuildCsvName($"ContingentValuesGroups_{fcName}", tableAndDataSource.DataSource);
                    string cvOutputFile = Common.BuildCsvName($"ContingentValues_{fcName}", tableAndDataSource.DataSource);

                    string pathToTable = pair.Key;
                    IReadOnlyList<string> cvArgs;

                    using (Datastore datastore = tableAndDataSource.Table.GetDatastore())
                    {
                        if (datastore is UnknownDatastore)
                            continue;

                        Uri uri = datastore.GetPath();
                        string datastorePath = uri.LocalPath;

                        FeatureClass featureclass = tableAndDataSource.Table as FeatureClass;
                        FeatureDataset featureDataset = null;

                        if (featureclass != null)
                            featureDataset = featureclass.GetFeatureDataset();

                        if (featureDataset == null)
                        {
                            //<path to connfile>.sde/meh.unadmin.featureclass
                            pathToTable = string.Format("{0}\\{1}", datastorePath, tableAndDataSource.Table.GetName());
                        }
                        else
                        {
                            //<path to connfile>.sde/meh.unadmin.Electric\meh.unadmin.ElectricDevice
                            string featureDatasetName = featureclass.GetFeatureDataset().GetName();
                            pathToTable = string.Format("{0}\\{1}\\{2}", datastorePath, featureDatasetName, tableAndDataSource.Table.GetName());
                        }

                        ////arcpy.management.ExportContingentValues("DHC Line", r"C:\temp\ProSdk_CSV\DHC_Line_CV_groups.CSV", r"C:\temp\ProSdk_CSV\DHC_Line_CV.CSV")
                        pathToTable = pathToTable.Replace("\\", "/");
                        cvArgs = Geoprocessing.MakeValueArray(pathToTable, cvGroupOutputFile, cvOutputFile);
                        var result = await Geoprocessing.ExecuteToolAsync("management.ExportContingentValues", cvArgs);
                    }
                }

                //Loop through directories and delete "_ContingentValues" files that are empty
                string[] directories = Directory.GetDirectories(Common.ExtractFilePath);
                foreach (string directory in directories)
                {
                    //Delete files that only have 1 line (header) which means 0 Contingent Values are assigned
                    DirectoryInfo directoryInfo = new DirectoryInfo(directory);
                    List<FileInfo> blankFiles = directoryInfo.GetFiles().Where(f => f.Extension == ".csv" && f.Name.Contains("_ContingentValues")).ToList();
                    foreach (FileInfo bf in blankFiles)
                    {
                        string[] lines = File.ReadAllLines(bf.FullName);
                        int cnt = lines.Count();

                        if (cnt == 1)
                            bf.Delete();
                    }

                    //Delete the .xml files that are genereated by the GP tool
                    List<FileInfo> deleteableFiles = directoryInfo.GetFiles().Where(f => f.Extension == ".xml" && f.Name.Contains("_ContingentValues")).ToList();
                    foreach (FileInfo file in deleteableFiles)
                        file.Delete();

                    FileInfo[] schemaIniFile = directoryInfo.GetFiles("schema.ini");
                    foreach (FileInfo schemaIni in schemaIniFile)
                        schemaIni.Delete();
                }
            });
        }
        private class TableAndDataSource
        {
            public Table Table { get; set; }
            public string DataSource { get; set; }
        }
    }
}