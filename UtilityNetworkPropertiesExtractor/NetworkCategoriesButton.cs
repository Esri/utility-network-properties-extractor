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
    internal class NetworkCategoriesButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Network Categories to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractNetworkCategoriesAsync(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Network Categories");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractNetworkCategoriesAsync(bool showNoUtilityNetworkPrompt)
        {
            return QueuedTask.Run(() =>
            {
                List<UtilityNetworkDataSourceInMap> utilityNetworkDataSourceInMapList = DataSourcesInMapHelper.GetUtilityNetworkDataSourcesInMap();
                if (utilityNetworkDataSourceInMapList.Count == 0)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("A Utility Network was not found in the active map", "Extract Network Categories", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                foreach (UtilityNetworkDataSourceInMap utilityNetworkDataSourceInMap in utilityNetworkDataSourceInMapList)
                {
                    using (Geodatabase geodatabase = utilityNetworkDataSourceInMap.Geodatabase)
                    {
                        string outputFile = Common.BuildCsvName("NetworkCategories", utilityNetworkDataSourceInMap.NameForCSV);
                        using (StreamWriter sw = new StreamWriter(outputFile))
                        {
                            //Header information
                            UtilityNetworkDefinition utilityNetworkDefinition = utilityNetworkDataSourceInMap.UtilityNetwork.GetDefinition();
                            Common.WriteHeaderInfoForUtilityNetwork(sw, utilityNetworkDataSourceInMap, "Network Categories");

                            //Network Categories
                            sw.WriteLine();
                            sw.WriteLine("Network Categories");
                            IEnumerable<string> categories = utilityNetworkDefinition.GetAvailableCategories().OrderBy(x => x).ToList();
                            foreach (string category in categories)
                                sw.WriteLine("," + category);

                            sw.WriteLine("");

                            List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                            //Get all properties defined in the class.  This will be used to generate the CSV file
                            CSVLayout emptyRec = new CSVLayout();
                            PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                            //Write column headers based on properties in the class
                            string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                            sw.WriteLine(columnHeader);

                            IReadOnlyList<NetworkSource> networkSourceList = utilityNetworkDefinition.GetNetworkSources();
                            foreach (NetworkSource networkSource in networkSourceList)
                            {
                                //Network Categories
                                IReadOnlyList<AssetGroup> assetGroupList = networkSource.GetAssetGroups();
                                foreach (AssetGroup assetGroup in assetGroupList)
                                {
                                    //Asset Types
                                    IReadOnlyList<AssetType> assetTypeList = assetGroup.GetAssetTypes();
                                    foreach (AssetType assetType in assetTypeList)
                                    {
                                        //Categories
                                        IReadOnlyList<string> categoriesList = assetType.CategoryList;
                                        foreach (string category in categoriesList)
                                        {
                                            CSVLayout rec = new CSVLayout()
                                            {
                                                NetworkCategory = category,
                                                ClassName = networkSource.Name,
                                                AssetGroupCode = assetGroup.Code.ToString(),
                                                AssetGroup = assetGroup.Name,
                                                AssetTypeCode = assetType.Code.ToString(),
                                                AssetType = assetType.Name,
                                            };

                                            csvLayoutList.Add(rec);
                                        }
                                    }
                                }
                            }

                            foreach (CSVLayout row in csvLayoutList.OrderBy(x => x.NetworkCategory))
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
            public string Assignments { get; set; }
            public string NetworkCategory { get; set; }
            public string ClassName { get; set; }
            public string AssetGroupCode { get; set; }
            public string AssetGroup { get; set; }
            public string AssetTypeCode { get; set; }
            public string AssetType { get; set; }
        }
    }
} 