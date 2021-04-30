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
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class NoNetworkRulesButton : Button
    {
        private static string _fileName = string.Empty;
        private static bool _fileGenerated = false;

        protected async override void OnClick()
        {
            try
            {
                await ExtractNoNetworkRulesAsync(true);
                if (_fileGenerated)
                    MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract No Network Rules");
            }
        }

        public static Task ExtractNoNetworkRulesAsync(bool showNoUtilityNetworkPrompt)
        {
            _fileGenerated = false;

            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayerInUn);
                if (utilityNetwork == null)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("Utility Network not found in the active map", "Extract No Network Rules", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayerInUn);
                Common.CreateOutputDirectory();

                string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _fileName = string.Format("{0}_{1}_NoNetworkRules.csv", dateFormatted, reportHeaderInfo.ProProjectName);
                string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                using (StreamWriter sw = new StreamWriter(outputFile))
                {
                    //Header information
                    UtilityNetworkDefinition utilityNetworkDefinition = utilityNetwork.GetDefinition();
                    Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "No Network Rule Assignments");

                    List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                    //Get all properties defined in the class.  This will be used to generate the CSV file
                    CSVLayout emptyRec = new CSVLayout();
                    PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                    //Write column headers based on properties in the class
                    string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                    sw.WriteLine(columnHeader); ;

                    IReadOnlyList<Rule> allRules = utilityNetworkDefinition.GetRules();
                    IReadOnlyList<NetworkSource> networkSourceList = utilityNetworkDefinition.GetNetworkSources();
                    foreach (NetworkSource networkSource in networkSourceList)
                    {
                        IReadOnlyList<AssetGroup> assetGroupList = networkSource.GetAssetGroups();
                        foreach (AssetGroup assetGroup in assetGroupList)
                        {
                            //Asset Types
                            IReadOnlyList<AssetType> assetTypeList = assetGroup.GetAssetTypes();
                            foreach (AssetType assetType in assetTypeList)
                            {
                                if (!AreRulesAssigned(networkSource.ID, assetGroup.Code, assetType.Code, allRules))
                                {
                                    CSVLayout rec = new CSVLayout()
                                    {
                                        ClassName = networkSource.Name,
                                        AssetGroupCode = assetGroup.Code.ToString(),
                                        AssetGroup = assetGroup.Name,
                                        AssetTypeCode = assetType.Code.ToString(),
                                        AssetType = assetType.Name
                                    };
                                    csvLayoutList.Add(rec);
                                }
                            }
                        }
                    }

                    //Write body
                    foreach (CSVLayout row in csvLayoutList)
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

        private static bool AreRulesAssigned(int networkSourceID, int assetGroup, int assetType, IReadOnlyList<Rule> allRules)
        {
            //Return true if at least 1 network rule is assigned
            foreach (Rule rule in allRules)
            {
                if (rule.RuleElements[0].NetworkSource.ID == networkSourceID)
                {
                    if (rule.RuleElements[0].AssetGroup.Code == assetGroup && rule.RuleElements[0].AssetType.Code == assetType)
                        return true;
                }
                else if (rule.RuleElements[1].NetworkSource.ID == networkSourceID)
                {
                    if (rule.RuleElements[1].AssetGroup.Code == assetGroup && rule.RuleElements[1].AssetType.Code == assetType)
                        return true;
                }
            }
            return false;
        }

        private class CSVLayout
        {
            public string ClassName { get; set; }
            public string AssetGroupCode { get; set; }
            public string AssetGroup { get; set; }
            public string AssetTypeCode { get; set; }
            public string AssetType { get; set; }
        }
    }
}