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
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class NetworkAttributesButton : Button
    {
        private static string _fileName = string.Empty;
        private static bool _fileGenerated = false;

        protected async override void OnClick()
        {
            try
            {
                _fileGenerated = false;
                await ExtractNetworkAttributesAsync(true);
                if (_fileGenerated)
                    MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Network Attributes");
            }
        }

        public static Task ExtractNetworkAttributesAsync(bool showNoUtilityNetworkPrompt)
        {
            _fileGenerated = false;

            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayerInUn);
                if (utilityNetwork == null)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("Utility Network not found in the active map", "Extract Network Attributes", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayerInUn);
                Common.CreateOutputDirectory();

                string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _fileName = string.Format("{0}_{1}_NetworkAttributes.csv", dateFormatted, reportHeaderInfo.ProProjectName);
                string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                using (StreamWriter sw = new StreamWriter(outputFile))
                {
                    //Header information
                    UtilityNetworkDefinition utilityNetworkDefinition = utilityNetwork.GetDefinition();
                    Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "Network Attributes");

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
                    _fileGenerated = true;
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