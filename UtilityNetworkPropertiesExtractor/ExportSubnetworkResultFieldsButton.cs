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
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UtilityNetworkPropertiesExtractor
{
    internal class ExportSubnetworkResultFieldsButton : Button
    {
        private static string _fileName = string.Empty;

        protected override void OnClick()
        {
            try
            {

                BuildResultFieldsAsync();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Build Export Subnetwork Result Fields");
            }
        }

        public static Task BuildResultFieldsAsync()
        {
            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayerInUn);
                if (utilityNetwork == null)
                    return;

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayerInUn);
                Common.CreateOutputDirectory();

                string mesg = string.Empty;
                string resultFields = string.Empty;

                using (Geodatabase geodatabase = featureLayerInUn.GetTable().GetDatastore() as Geodatabase)
                {
                    string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    _fileName = string.Format("{0}_{1}_ExportSubnetworkResultFields.txt", dateFormatted, reportHeaderInfo.ProProjectName);
                    string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                    using (StreamWriter sw = new StreamWriter(outputFile))
                    {
                        IReadOnlyList<FeatureClassDefinition> fcDefinitionList = geodatabase.GetDefinitions<FeatureClassDefinition>();
                        IReadOnlyList<TableDefinition> tableDefinitionList = geodatabase.GetDefinitions<TableDefinition>();

                        //Network Sources
                        UtilityNetworkDefinition utilityNetworkDefinition = utilityNetwork.GetDefinition();
                        IOrderedEnumerable<NetworkSource> networkSourceList = utilityNetworkDefinition.GetNetworkSources().OrderBy(x => x.ID);
                        foreach (NetworkSource networkSource in networkSourceList)
                        {
                            if (networkSource.UsageType == SourceUsageType.SubnetLine || networkSource.UsageType == SourceUsageType.Association || networkSource.UsageType == SourceUsageType.SystemJunction)
                                continue;

                            // UN FeatureClasses's UsageType values are between 0 and 7
                            if ((int)networkSource.UsageType <= 7)
                            {
                                //search for featureclass
                                foreach (FeatureClassDefinition fcDefinition in fcDefinitionList)
                                {
                                    string fcName = fcDefinition.GetName();

                                    fcName = ReformatClassNames(fcName);
                                    if (fcName == networkSource.Name)
                                    {
                                        mesg += "FC:" + networkSource.Name + "\n";
                                        IReadOnlyList<Field> fieldsList = fcDefinition.GetFields();
                                        BuildResultsString(fcName, fieldsList, ref resultFields);
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                foreach (TableDefinition tableDefinition in tableDefinitionList)
                                {
                                    string tableName = tableDefinition.GetName();

                                    tableName = ReformatClassNames(tableName);
                                    if (tableName == networkSource.Name)
                                    {
                                        mesg += "Table:" + networkSource.Name + "\n";
                                        IReadOnlyList<Field> fieldsList = tableDefinition.GetFields();
                                        BuildResultsString(tableName, fieldsList, ref resultFields);
                                        break;
                                    }
                                }
                            }
                        }

                        sw.WriteLine(Common.EncloseStringInDoubleQuotes(resultFields));
                        MessageBox.Show(mesg);
                    }
                }
            });
        }

        private static string ReformatClassNames(string fcName)
        {
            //strip out the leading number from the table name "L0Electric_Device".  
            //Also need to replace the underscore with a blank space
            int index = fcName.LastIndexOfAny("0123456789".ToCharArray());
            fcName = fcName.Substring(index + 1).Replace("_", " ");
            return fcName;
        }

        private static void BuildResultsString(string fcName, IReadOnlyList<Field> fieldsList, ref string resultFields)
        {
            foreach (Field field in fieldsList)
            {
                if (field.FieldType == FieldType.Geometry || field.FieldType == FieldType.Blob || field.FieldType == FieldType.Raster || field.Name.Contains('('))
                    continue;

                else
                    resultFields += string.Format("'{0}' {1};", fcName, field.Name);
            }

            return;
        }
    }
}
