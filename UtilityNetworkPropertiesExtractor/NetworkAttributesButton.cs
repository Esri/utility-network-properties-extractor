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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class NetworkAttributesButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Network Attributes to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractNetworkAttributesAsync(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Network Attributes");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractNetworkAttributesAsync(bool showNoUtilityNetworkPrompt)
        {
            return QueuedTask.Run(() =>
            {
                List<UtilityNetworkDataSourceInMap> utilityNetworkDataSourceInMapList = DataSourcesInMapHelper.GetUtilityNetworkDataSourcesInMap();
                if (utilityNetworkDataSourceInMapList.Count == 0)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("A Utility Network was not found in the active map", "Extract Network Attributes", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                foreach (UtilityNetworkDataSourceInMap utilityNetworkDataSourceInMap in utilityNetworkDataSourceInMapList)
                {
                    using (Geodatabase geodatabase = utilityNetworkDataSourceInMap.Geodatabase)
                    {
                        string outputFile = Common.BuildCsvName("NetworkAttributes", utilityNetworkDataSourceInMap.Name);
                        using (StreamWriter sw = new StreamWriter(outputFile))
                        {
                            //Header information
                            UtilityNetworkDefinition utilityNetworkDefinition = utilityNetworkDataSourceInMap.UtilityNetwork.GetDefinition();
                            Common.WriteHeaderInfoForUtilityNetwork(sw, utilityNetworkDataSourceInMap, "Network Attributes");

                            //Network Attributes
                            List<CSVLayoutNetworkAttributes> CSVLayoutNetworkAttributesList = new List<CSVLayoutNetworkAttributes>();

                            //Get all properties defined in the class.  This will be used to generate the CSV file
                            CSVLayoutNetworkAttributes emptyRec = new CSVLayoutNetworkAttributes();
                            PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                            //Write column headers based on properties in the class
                            string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                            sw.WriteLine(columnHeader);

                            IEnumerable<NetworkAttribute> networkAttributes = utilityNetworkDefinition.GetNetworkAttributes().OrderBy(x => x.Name);
                            foreach (NetworkAttribute networkAttribute in networkAttributes)
                            {
                                CSVLayoutNetworkAttributes rec = new CSVLayoutNetworkAttributes()
                                {
                                    Name = networkAttribute.Name,
                                    DataType = networkAttribute.Type.ToString(),
                                    Domain = networkAttribute.Domain?.GetName()
                                };
                                CSVLayoutNetworkAttributesList.Add(rec);
                            }
                            CSVLayoutNetworkAttributesList.Add(emptyRec);

                            foreach (CSVLayoutNetworkAttributes row in CSVLayoutNetworkAttributesList)
                            {
                                string output = Common.ExtractClassValuesToString(row, properties);
                                sw.WriteLine(output);
                            }

                            //Network Attribute Assignments
                            List<CSVLayoutNetworksAttributesAssignments> CSVLayoutNetworksAttributesAssignmentsList = new List<CSVLayoutNetworksAttributesAssignments>();

                            CSVLayoutNetworksAttributesAssignments emptyAssignmentRec = new CSVLayoutNetworksAttributesAssignments();
                            properties = Common.GetPropertiesOfClass(emptyAssignmentRec);

                            //Write column headers based on properties in the class
                            columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                            sw.WriteLine(columnHeader);

                            foreach (NetworkAttribute networkAttribute in networkAttributes)
                            {
                                IReadOnlyList<NetworkAttributeAssignment> assignments = networkAttribute.Assignments;
                                foreach (NetworkAttributeAssignment assignment in assignments)
                                {
                                    CSVLayoutNetworksAttributesAssignments assignRec = new CSVLayoutNetworksAttributesAssignments()
                                    {
                                        NetworkAttribute = networkAttribute.Name,
                                        ClassName = assignment.NetworkSource.Name,
                                        FieldName = assignment.Field?.Name
                                    };
                                    CSVLayoutNetworksAttributesAssignmentsList.Add(assignRec);
                                }
                            }

                            foreach (CSVLayoutNetworksAttributesAssignments row in CSVLayoutNetworksAttributesAssignmentsList)
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

        private class CSVLayoutNetworkAttributes
        {
            public string NetworkAttributes { get; set; }
            public string Name { get; set; }
            public string DataType { get; set; }
            public string Domain { get; set; }
        }

        private class CSVLayoutNetworksAttributesAssignments
        {
            public string Assignments { get; set; }
            public string NetworkAttribute { get; set; }
            public string ClassName { get; set; }
            public string FieldName { get; set; }
        }
    }
} 