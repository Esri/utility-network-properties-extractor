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
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    // 11/10/22
    // https://pro.arcgis.com/en/pro-app/2.9/tool-reference/utility-networks/export-subnetwork.htm
    // Geoprocessing Tool, Export Subnetwork, has a parameter for "result_fields" which identifies all the fields to be written to a JSON file.
    //   Using ArcGIS Pro's GUI to manually select thoses fields in the GP Tool is not efficient.
    //   This Pro SDK button will extract all Attribute fields to a text file in the format that the GP tool is expecting it.
    //   If you don't want all fields on every class, either manually remove specific fields or modify method, BuildResultsString()
    // Note: Export Subnetwork is not supported in client-server mode.  So this button will raise an error when exectued against a map using database connections.
    internal class ExportSubnetworkResultFieldsButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Asset Groups to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await BuildResultFieldsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Build Export Subnetwork Result Fields");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task BuildResultFieldsAsync()
        {
            return QueuedTask.Run(() =>
            {
                List<UtilityNetworkDataSourceInMap> utilityNetworkDataSourceInMapList = DataSourcesInMapHelper.GetUtilityNetworkDataSourcesInMap();
                if (utilityNetworkDataSourceInMapList.Count == 0)
                    return;

                string resultFields = string.Empty;
                foreach (UtilityNetworkDataSourceInMap utilityNetworkDataSourceInMap in utilityNetworkDataSourceInMapList)
                {
                    using (Geodatabase geodatabase = utilityNetworkDataSourceInMap.Geodatabase)
                    {
                        string outputFile = Common.BuildTextFileName("ExportSubnetworkResultFields", utilityNetworkDataSourceInMap.NameForCSV);
                        using (StreamWriter sw = new StreamWriter(outputFile))
                        {
                            IReadOnlyList<FeatureClassDefinition> fcDefinitionList = geodatabase.GetDefinitions<FeatureClassDefinition>();
                            IReadOnlyList<TableDefinition> tableDefinitionList = geodatabase.GetDefinitions<TableDefinition>();

                            //Get all Network Sources
                            UtilityNetworkDefinition utilityNetworkDefinition = utilityNetworkDataSourceInMap.UtilityNetwork.GetDefinition();
                            IOrderedEnumerable<NetworkSource> networkSourceList = utilityNetworkDefinition.GetNetworkSources().OrderBy(x => x.ID);
                            foreach (NetworkSource networkSource in networkSourceList)
                            {
                                // don't include these UN classes when builidng the ResultFields
                                if (networkSource.UsageType == SourceUsageType.SubnetLine || networkSource.UsageType == SourceUsageType.Association || networkSource.UsageType == SourceUsageType.SystemJunction)
                                    continue;

                                // Utility Network FeatureClasses's UsageType values are between 0 and 7
                                if ((int)networkSource.UsageType <= 7)
                                {
                                    //search for featureclass
                                    foreach (FeatureClassDefinition fcDefinition in fcDefinitionList)
                                    {
                                        string fcName = fcDefinition.GetName();

                                        if (utilityNetworkDataSourceInMap.WorkspaceFactory == WorkspaceFactory.FeatureService.ToString())
                                            fcName = ReformatClassNameFromService(fcName);

                                        if (fcName == networkSource.Name)
                                        {
                                            IReadOnlyList<Field> fieldsList = fcDefinition.GetFields();
                                            BuildResultsString(fcName, fieldsList, ref resultFields);
                                            break;
                                        }
                                    }
                                }
                                else // Utility Network Tables
                                {
                                    foreach (TableDefinition tableDefinition in tableDefinitionList)
                                    {
                                        string tableName = tableDefinition.GetName();

                                        if (utilityNetworkDataSourceInMap.WorkspaceFactory == WorkspaceFactory.FeatureService.ToString())
                                            tableName = ReformatClassNameFromService(tableName);

                                        if (tableName == networkSource.Name)
                                        {
                                            IReadOnlyList<Field> fieldsList = tableDefinition.GetFields();
                                            BuildResultsString(tableName, fieldsList, ref resultFields);
                                            break;
                                        }
                                    }
                                }
                            }

                            sw.WriteLine(Common.EncloseStringInDoubleQuotes(resultFields));
                            sw.Flush();
                        }
                    }
                }
            });
        }

        private static string ReformatClassNameFromService(string fcName)
        {
            //strip out the leading number from the table name "L0Electric_Device".  
            //Also need to replace the underscore with a blank space
            int index = fcName.LastIndexOfAny("0123456789".ToCharArray());
            return fcName.Substring(index + 1).Replace("_", " ");
        }

        private static string StripOffDbOwner(string fcName)
        {
            //When exporting from a Mobile GDB, need to strip off db owner ("main.ElectricDevice") from the class name
            //When data source is an database connection, need to strip off database name and owner ("prod.gis.ElectricDevice") from the class name.
            int index = fcName.LastIndexOf(".");
            if (index == -1)
                return fcName;
            else
                return fcName.Substring(index + 1);
        }

        private static void BuildResultsString(string fcName, IReadOnlyList<Field> fieldsList, ref string resultFields)
        {
            fcName = StripOffDbOwner(fcName);

            foreach (Field field in fieldsList)
            {
                if (field.FieldType == FieldType.Geometry || field.FieldType == FieldType.Blob || field.FieldType == FieldType.Raster || field.Name.Contains('('))
                    continue;
                else
                    resultFields += string.Format("'{0}' {1};", fcName, field.Name);
            }
        }
    }
}