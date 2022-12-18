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
    internal class AssetGroupsButton : Button
    {
        private static string _fileName = string.Empty;
        private static bool _fileGenerated = false;

        protected async override void OnClick()
        {
            try
            {
                await ExtractAssetGroupsAsync(true);
                if (_fileGenerated)
                    MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Asset Groups");
            }
        }

        public static Task ExtractAssetGroupsAsync(bool showNoUtilityNetworkPrompt)
        {
            _fileGenerated = false;

            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayerInUn);
                if (utilityNetwork == null)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("Utility Network not found in the active map", "Extract Asset Groups", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayerInUn);
                Common.CreateOutputDirectory();

                string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _fileName = string.Format("{0}_{1}_AssetGroups.csv", dateFormatted, reportHeaderInfo.MapName);
                string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                using (StreamWriter sw = new StreamWriter(outputFile))
                {
                    //Header information
                    UtilityNetworkDefinition utilityNetworkDefinition = utilityNetwork.GetDefinition();
                    Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "Asset Groups");

                    //Get all properties defined in the class.  This will be used to generate the CSV file
                    CSVLayout emptyRec = new CSVLayout();
                    PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                    //Write column headers based on properties in the class
                    string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                    sw.WriteLine(columnHeader);

                    List<CSVLayout> csvLayoutList = new List<CSVLayout>();
                    CSVLayout rec;

                    //Network Sources
                    IOrderedEnumerable<NetworkSource> networkSourceList = utilityNetworkDefinition.GetNetworkSources().OrderBy(x => x.ID);
                    foreach (NetworkSource networkSource in networkSourceList)
                    {
                        //Asset Groups
                        IOrderedEnumerable<AssetGroup> assetGroupList = networkSource.GetAssetGroups().OrderBy(x => x.Code);
                        foreach (AssetGroup assetGroup in assetGroupList)
                        {

                            //Subnetwork will only have assetgroups.  Write entry to CSV.
                            if (networkSource.UsageType == SourceUsageType.SubnetLine)
                            {
                                rec = new CSVLayout()
                                {
                                    NetworkSourceID = networkSource.ID.ToString(),
                                    ClassName = networkSource.Name,
                                    AssetGroupCode = assetGroup.Code.ToString(),
                                    AssetGroup = assetGroup.Name
                                };
                                csvLayoutList.Add(rec);
                            }

                            //Asset Types
                            string networkEdgePolicy = string.Empty;
                            string containerSplitPolicy = string.Empty;
                            string terminalConfigName = string.Empty;
                            string categories = string.Empty;

                            IOrderedEnumerable<AssetType> assetTypeList = assetGroup.GetAssetTypes().OrderBy(y => y.Code);
                            foreach (AssetType assetType in assetTypeList)
                            {
                                if (assetType.IsTerminalConfigurationSupported())
                                    terminalConfigName = assetType.GetTerminalConfiguration().Name;
                                else
                                    terminalConfigName = string.Empty;

                                if (assetType.IsLinearConnectivityPolicySupported())
                                    networkEdgePolicy = assetType.GetLinearConnectivityPolicy().ToString();
                                else
                                    networkEdgePolicy = string.Empty;

                                if (assetType.IsContainerSplitPolicySupported())
                                    containerSplitPolicy = assetType.GetContainerSplitPolicy().ToString();
                                else
                                    containerSplitPolicy = string.Empty;

                                categories = string.Empty;
                                IReadOnlyList<string> categoriesList = assetType.CategoryList;
                                foreach (string category in categoriesList)
                                    categories += category + ";";

                                if (categoriesList.Count != 0)
                                {
                                    int pos = categories.LastIndexOf(";");
                                    categories = categories.Remove(pos);
                                }

                                rec = new CSVLayout()
                                {
                                    NetworkSourceID = networkSource.ID.ToString(),
                                    ClassName = networkSource.Name,
                                    AssetGroupCode = assetGroup.Code.ToString(),
                                    AssetGroup = assetGroup.Name,
                                    AssetTypeCode = assetType.Code.ToString(),
                                    AssetType = assetType.Name,
                                    AssocationRole = assetType.AssociationRoleType.ToString(),
                                    ContainerViewScale = assetType.ContainerViewScale.ToString(),
                                    AssocationDeletionSemantics = assetType.AssociationDeletionSemantics.ToString(),
                                    TerminalConfiguration = terminalConfigName,
                                    NetworkEdgeConnectivityPolicy = networkEdgePolicy,
                                    ContainerSplitPolicy = containerSplitPolicy,
                                    Categories = categories
                                };

                                csvLayoutList.Add(rec);
                            }

                            //blank link between each asset group
                            rec = new CSVLayout()
                            {
                                NetworkSourceID = networkSource.ID.ToString(),
                                ClassName = networkSource.Name
                            };
                            csvLayoutList.Add(rec);
                        }
                    }

                    //Write body of CSV
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

        private class CSVLayout
        {
            public string NetworkSourceID { get; set; }
            public string ClassName { get; set; }
            public string AssetGroupCode { get; set; }
            public string AssetGroup { get; set; }
            public string AssetTypeCode { get; set; }
            public string AssetType { get; set; }
            public string AssocationRole { get; set; }
            public string ContainerViewScale { get; set; }
            public string AssocationDeletionSemantics { get; set; }
            public string TerminalConfiguration { get; set; }
            public string NetworkEdgeConnectivityPolicy { get; set; }
            public string ContainerSplitPolicy { get; set; }
            public string Categories { get; set; }
        }
    }
}