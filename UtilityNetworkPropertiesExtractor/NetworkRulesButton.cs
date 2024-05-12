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
    internal class NetworkRulesButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Network Rules to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractNetworkRulesAsync(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Network Rules");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractNetworkRulesAsync(bool showNoUtilityNetworkPrompt)
        {
            return QueuedTask.Run(() =>
            {
                List<UtilityNetworkDataSourceInMap> utilityNetworkDataSourceInMapList = DataSourcesInMapHelper.GetUtilityNetworkDataSourcesInMap();
                if (utilityNetworkDataSourceInMapList.Count == 0)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("A Utility Network was not found in the active map", "Extract Network Rules", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                foreach (UtilityNetworkDataSourceInMap utilityNetworkDataSourceInMap in utilityNetworkDataSourceInMapList)
                {
                    using (Geodatabase geodatabase = utilityNetworkDataSourceInMap.Geodatabase)
                    {
                        string outputFile = Common.CreateCsvFile("NetworkRules", utilityNetworkDataSourceInMap.NameForCSV);
                        using (StreamWriter sw = new StreamWriter(outputFile))
                        {
                            //Header information
                            UtilityNetworkDefinition utilityNetworkDefinition = utilityNetworkDataSourceInMap.UtilityNetwork.GetDefinition();
                            Common.WriteHeaderInfoForUtilityNetwork(sw, utilityNetworkDataSourceInMap, "Network Rules");

                            IOrderedEnumerable<Rule> rulesList = utilityNetworkDefinition.GetRules().OrderBy(x => x.Type).ThenBy(x => x.ID);

                            //1.  List out the Rule types with their count
                            CSVLayoutCounts emptyCountRec = new CSVLayoutCounts();
                            PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyCountRec);

                            //Write column headers based on properties in the class
                            string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                            sw.WriteLine(columnHeader);

                            //build section consisting of Rule Types and counts
                            int ruleTypeCount = 0;
                            List<CSVLayoutCounts> countsList = new List<CSVLayoutCounts>();
                            foreach (int rt in Enum.GetValues(typeof(RuleType)))
                            {
                                ruleTypeCount = rulesList.Where(x => x.Type == (RuleType)rt).Count();

                                CSVLayoutCounts ruleRec = new CSVLayoutCounts()
                                {
                                    RuleCode = rt.ToString(), //The rule code can be used in sql queries against table un_#_rules
                                    RuleType = ((RuleType)rt).ToString(),
                                    Count = ruleTypeCount.ToString()
                                };
                                countsList.Add(ruleRec);
                            }

                            //Include total number of rules
                            CSVLayoutCounts totalCountRec = new CSVLayoutCounts()
                            {
                                Count = rulesList.Count().ToString()
                            };
                            countsList.Add(totalCountRec);

                            //Write section to CSV file
                            countsList.Add(emptyCountRec);
                            foreach (CSVLayoutCounts row in countsList)
                            {
                                string output = Common.ExtractClassValuesToString(row, properties);
                                sw.WriteLine(output);
                            }

                            //2.  Body of the report. 
                            //Get all properties defined in the class.  This will be used to generate the CSV file
                            CSVLayoutRules emptyRec = new CSVLayoutRules();
                            properties = Common.GetPropertiesOfClass(emptyRec);

                            //Write column headers based on properties in the class
                            columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                            sw.WriteLine(columnHeader);

                            //Build list of all Rules
                            List<CSVLayoutRules> csvLayoutList = new List<CSVLayoutRules>();
                            foreach (Rule rule in rulesList)
                            {
                                IReadOnlyList<RuleElement> ruleElementList = rule.RuleElements;

                                CSVLayoutRules rec = new CSVLayoutRules()
                                {
                                    RuleID = rule.ID.ToString(),
                                    RuleType = rule.Type.ToString(),
                                    FromClassName = ruleElementList[0].NetworkSource.Name,
                                    FromAssetGroupCode = ruleElementList[0].AssetGroup.Code.ToString(),
                                    FromAssetGroup = ruleElementList[0].AssetGroup.Name,
                                    FromAssetTypeCode = ruleElementList[0].AssetType.Code.ToString(),
                                    FromAssetType = ruleElementList[0].AssetType.Name,
                                    FromTerminal = ruleElementList[0].Terminal?.Name,

                                    ToClassName = ruleElementList[1].NetworkSource.Name,
                                    ToAssetGroupCode = ruleElementList[1].AssetGroup.Code.ToString(),
                                    ToAssetGroup = ruleElementList[1].AssetGroup.Name,
                                    ToAssetTypeCode = ruleElementList[1].AssetType.Code.ToString(),
                                    ToAssetType = ruleElementList[1].AssetType.Name,
                                    ToTerminal = ruleElementList[1].Terminal?.Name,
                                };

                                if (ruleElementList.Count == 3)
                                {
                                    rec.ViaClassName = ruleElementList[2].NetworkSource.Name;
                                    rec.ViaAssetGroupCode = ruleElementList[2].AssetGroup.Code.ToString();
                                    rec.ViaAssetGroup = ruleElementList[2].AssetGroup.Name;
                                    rec.ViaAssetTypeCode = ruleElementList[2].AssetType.Code.ToString();
                                    rec.ViaAssetType = ruleElementList[2].AssetType.Name;
                                    rec.ViaTerminal = ruleElementList[2].Terminal?.Name;
                                }
                                csvLayoutList.Add(rec);
                            }

                            //write body of report.  Order by Rule Type, From AssetGroup, From AssetType, To AssetGroup, ToAssetType
                            foreach (CSVLayoutRules row in csvLayoutList.OrderBy(x => x.RuleType).ThenBy(x => Convert.ToInt32(x.FromAssetGroupCode)).ThenBy(x => Convert.ToInt32(x.FromAssetTypeCode)).ThenBy(x => Convert.ToInt32(x.ToAssetGroupCode)).ThenBy(x => Convert.ToInt32(x.ToAssetTypeCode)))
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

        private class CSVLayoutCounts
        {
            public string RuleCode { get; set; }
            public string RuleType { get; set; }
            public string Count { get; set; }
        }

        private class CSVLayoutRules
        {
            public string RuleID { get; set; }
            public string RuleType { get; set; }
            public string FromClassName { get; set; }
            public string FromAssetGroupCode { get; set; }
            public string FromAssetGroup { get; set; }
            public string FromAssetTypeCode { get; set; }
            public string FromAssetType { get; set; }
            public string FromTerminal { get; set; }
            public string ToClassName { get; set; }
            public string ToAssetGroupCode { get; set; }
            public string ToAssetGroup { get; set; }
            public string ToAssetTypeCode { get; set; }
            public string ToAssetType { get; set; }
            public string ToTerminal { get; set; }
            public string ViaClassName { get; set; }
            public string ViaAssetGroupCode { get; set; }
            public string ViaAssetGroup { get; set; }
            public string ViaAssetTypeCode { get; set; }
            public string ViaAssetType { get; set; }
            public string ViaTerminal { get; set; }
        }
    }
} 