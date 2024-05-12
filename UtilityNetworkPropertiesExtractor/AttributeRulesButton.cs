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
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class AttributeRulesButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Attribute Rule CSV(s) to:\n" + Common.ExtractFilePath); ;

            try
            {               
                progDlg.Show();

                await ExtractAttributeRulesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Attribute Rules");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static async Task ExtractAttributeRulesAsync()
        {
            await QueuedTask.Run(async () =>
            {
                List<DataSourceInMap> dataSourceInMapList = DataSourcesInMapHelper.GetDataSourcesInMap();
                foreach (DataSourceInMap dataSourceInMap in dataSourceInMapList)
                {
                    if (dataSourceInMap.WorkspaceFactory != WorkspaceFactory.FeatureService.ToString() || dataSourceInMap.WorkspaceFactory != WorkspaceFactory.Shapefile.ToString())
                    {
                        using (Geodatabase geodatabase = dataSourceInMap.Geodatabase)
                        {
                            // FeatureClasses
                            string pathToTable;
                            IReadOnlyList<FeatureClassDefinition> fcDefinitionList = geodatabase.GetDefinitions<FeatureClassDefinition>();
                            foreach (FeatureClassDefinition fcDefinition in fcDefinitionList)
                            {
                                //Determine the feature dataset name (if applicable) for the featureclass
                                using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(fcDefinition.GetName()))
                                {
                                    FeatureDataset featureDataset = featureClass.GetFeatureDataset();
                                    if (featureDataset != null)
                                    {
                                        //Use the absolute path for local files results in escape characters for things like spaces, which the GP tool can't handle for input/output of local files.
                                        pathToTable = $"{geodatabase.GetPath().LocalPath}/{featureDataset.GetName()}/{fcDefinition.GetName()}";
                                    }
                                    else
                                        pathToTable = $"{geodatabase.GetPath().LocalPath}/{fcDefinition.GetName()}";

                                    //Call GP Tool to Export Attribute Rules
                                    await CallGPTool(dataSourceInMap, pathToTable, fcDefinition.GetName());
                                }
                            }

                            // Tables
                            IReadOnlyList<TableDefinition> tableDefinitionList = geodatabase.GetDefinitions<TableDefinition>();
                            foreach (TableDefinition tableDefinition in tableDefinitionList)
                            {
                                using (Table table = geodatabase.OpenDataset<Table>(tableDefinition.GetName()))
                                {
                                    //Use the absolute path for local files results in escape characters for things like spaces, which the GP tool can't handle for input/output of local files.
                                    pathToTable = $"{geodatabase.GetPath().LocalPath}/{tableDefinition.GetName()}";

                                    //Call GP Tool to Export Attribute Rules
                                    await CallGPTool(dataSourceInMap, pathToTable, tableDefinition.GetName());
                                }
                            }
                        }
                    }

                    //Delete files were ARs weren't assigned to the object.
                    DeleteEmptyFiles(dataSourceInMap);
                }
            });
        }

        private static async Task CallGPTool(DataSourceInMap dataSourceInMap, string pathToTable, string objectName)
        {
            string attrRuleOutputFile = Common.CreateCsvFile($"AttributeRules_{objectName}", dataSourceInMap.NameForCSV);
            IReadOnlyList<string> attrRuleArgs = Geoprocessing.MakeValueArray(pathToTable, attrRuleOutputFile);
            await Geoprocessing.ExecuteToolAsync("management.ExportAttributeRules", attrRuleArgs);
        }

        private static void DeleteEmptyFiles(DataSourceInMap dataSourceInMap)
        {
            //Delete files that only have 1 line (header) which means 0 Attribute Rules were assigned
            DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(Common.ExtractFilePath, dataSourceInMap.NameForCSV));
            List<FileInfo> blankFiles = directoryInfo.GetFiles().Where(f => f.Extension == ".csv" && f.Name.Contains("_AttributeRules")).ToList();
            foreach (FileInfo bf in blankFiles)
            {
                string[] lines = File.ReadAllLines(bf.FullName);
                int cnt = lines.Count();

                if (cnt == 1)
                    bf.Delete();
            }

            //Delete the .xml files that are genereated by the GP tool
            List<FileInfo> deleteableFiles = directoryInfo.GetFiles().Where(f => f.Extension == ".xml" && f.Name.Contains("_AttributeRules")).ToList();
            foreach (FileInfo file in deleteableFiles)
                file.Delete();

            FileInfo[] schemaIniFile = directoryInfo.GetFiles("schema.ini");
            foreach (FileInfo schemaIni in schemaIniFile)
                schemaIni.Delete();
        }       
    }
}