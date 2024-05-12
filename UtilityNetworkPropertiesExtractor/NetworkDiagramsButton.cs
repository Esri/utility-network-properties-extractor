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
using ArcGIS.Core.Data.NetworkDiagrams;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Newtonsoft.Json;
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
    internal class NetworkDiagramsButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Network Diagram Info to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractNetworkDiagramsAsync(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Network Diagram Info");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractNetworkDiagramsAsync(bool showNoUtilityNetworkPrompt)
        {
            return QueuedTask.Run(() =>
            {
                List<UtilityNetworkDataSourceInMap> utilityNetworkDataSourceInMapList = DataSourcesInMapHelper.GetUtilityNetworkDataSourcesInMap();
                if (utilityNetworkDataSourceInMapList.Count == 0)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("A Utility Network was not found in the active map", "Extract Network Diagram Info", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                foreach (UtilityNetworkDataSourceInMap utilityNetworkDataSourceInMap in utilityNetworkDataSourceInMapList)
                {
                    using (Geodatabase geodatabase = utilityNetworkDataSourceInMap.Geodatabase)
                    {
                        string outputFile = Common.CreateCsvFile("NetworkDiagramInfo", utilityNetworkDataSourceInMap.NameForCSV);
                        using (StreamWriter sw = new StreamWriter(outputFile))
                        {
                            List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                            //Header information
                            UtilityNetworkDefinition utilityNetworkDefinition = utilityNetworkDataSourceInMap.UtilityNetwork.GetDefinition();
                            Common.WriteHeaderInfoForUtilityNetwork(sw, utilityNetworkDataSourceInMap, "Network Diagram Info");

                            //Get all properties defined in the class.  This will be used to generate the CSV file
                            CSVLayout emptyRec = new CSVLayout();
                            PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                            //Write column headers based on properties in the class
                            string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                            sw.WriteLine(columnHeader);

                            if (utilityNetworkDataSourceInMap.WorkspaceFactory == WorkspaceFactory.FeatureService.ToString())
                            {
                                ArcGISPortal portal = ArcGISPortalManager.Current.GetActivePortal();
                                if (portal == null)
                                    throw new Exception("You must be logged into portal to extract the Utility Network FeatureService Info");

                                string unFeatureServiceURL = Common.AppendTokenToUrl(utilityNetworkDataSourceInMap.URI, portal.GetToken());
                                string diagramDatasetServiceURL = unFeatureServiceURL.Replace("FeatureServer", "NetworkDiagramServer/diagramDataset");
                                EsriHttpResponseMessage response = Common.QueryRestPointUsingGet(diagramDatasetServiceURL);

                                string json = response?.Content?.ReadAsStringAsync()?.Result;
                                if (json == null)
                                    throw new Exception("Failed to get data from Utility Network Feature Service endpoint");

                                JSONMappings.ArcRestError arcRestError = JsonConvert.DeserializeObject<JSONMappings.ArcRestError>(json);
                                if (arcRestError?.error != null)
                                    throw new Exception(arcRestError?.error.code + " - " + arcRestError?.error.message + "\n" + diagramDatasetServiceURL);

                                JSONMappings.NetworkDiagramTemplateJSONMapping parsedJson = JsonConvert.DeserializeObject<JSONMappings.NetworkDiagramTemplateJSONMapping>(json);

                                for (int i = 0; i < parsedJson.diagramTemplateInfos.Length; i++)
                                {
                                    //globalids needed for GP Tool
                                    CSVLayout rec = new CSVLayout()
                                    {
                                        Name = Common.EncloseStringInDoubleQuotes(Convert.ToString(parsedJson.diagramTemplateInfos[i].name)),
                                        LastModifiedTime = Convert.ToString(Common.ConvertEpochTimeToReadableDate(parsedJson.diagramTemplateInfos[i].lastUpdateDate)),
                                        DiagramStorage = parsedJson.diagramTemplateInfos[i].enableDiagramStorage.ToString(),
                                        IsSystem = parsedJson.diagramTemplateInfos[i].usedByATier.ToString(),
                                        ExtendDiagram = parsedJson.diagramTemplateInfos[i].enableDiagramExtend.ToString(),
                                        Description = Common.EncloseStringInDoubleQuotes(Convert.ToString(parsedJson.diagramTemplateInfos[i].description)),
                                        CreationDate = Convert.ToString(Common.ConvertEpochTimeToReadableDate(parsedJson.diagramTemplateInfos[i].creationDate))
                                    };
                                    csvLayoutList.Add(rec);
                                }
                            }
                            else
                            {
                                DiagramManager diagramManager = utilityNetworkDataSourceInMap.UtilityNetwork.GetDiagramManager();
                                IEnumerable<DiagramTemplate> diagramTemplateList = diagramManager.GetDiagramTemplates().OrderBy(x => x.Name);
                                foreach (DiagramTemplate diagramTemplate in diagramTemplateList)
                                {
                                    CSVLayout rec = new CSVLayout
                                    {
                                        Name = diagramTemplate.Name,
                                    };
                                    csvLayoutList.Add(rec);
                                }
                            }

                            foreach (CSVLayout row in csvLayoutList.OrderBy(x => x.Name))
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
            public string DiagramTemplate { get; set; }
            public string Name { get; set; }
            public string CreationDate { get; set; }
            public string LastModifiedTime { get; set; }
            public string DiagramStorage { get; set; }
            public string IsSystem { get; set; }
            public string ExtendDiagram { get; set; }
            public string Description { get; set; }
        }
    }
}