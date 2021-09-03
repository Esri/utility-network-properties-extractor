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
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
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
    internal class TraceConfigurationButton : Button
    {
        private static string _fileName = string.Empty;
        private static readonly EsriHttpClient esriHttpClient = new EsriHttpClient();

        protected async override void OnClick()
        {
            try
            {
                ProgressDialog progDlg = new ProgressDialog("Extracting Trace Configuration .JSON and .CSV to: \n" + Common.ExtractFilePath);
                progDlg.Show();

                await ExtractTraceConfigurationAsync(true);

                progDlg.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Trace Configuration");
            }
        }

        public static async Task ExtractTraceConfigurationAsync(bool showNoUtilityNetworkPrompt)
        {
            await QueuedTask.Run(async () =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayer);
                if (utilityNetwork == null)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("Utility Network not found in the active map", "Extract Trace Configuration", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayer);

                using (Geodatabase geodatabase = featureLayer.GetTable().GetDatastore() as Geodatabase)
                {
                    Common.CreateOutputDirectory();
                    string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    _fileName = string.Format("{0}_{1}_TraceConfiguration.csv", dateFormatted, reportHeaderInfo.ProProjectName);
                    string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                    using (StreamWriter sw = new StreamWriter(outputFile))
                    {
                        //Header information
                        UtilityNetworkDefinition utilityNetworkDefinition = utilityNetwork.GetDefinition();
                        Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "Trace Configuration");

                        if (Convert.ToInt32(reportHeaderInfo.UtiltyNetworkSchemaVersion) < 5)
                        {
                            sw.WriteLine("Trace Configuration was introduced at Utility Network Version 5");
                            return;
                        }

                        //Get all properties defined in the class.  This will be used to generate the CSV file
                        CSVLayout emptyRec = new CSVLayout();
                        PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                        //Write column headers based on properties in the class
                        string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                        sw.WriteLine(columnHeader);

                        List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                        if (reportHeaderInfo.SourceType == Common.DatastoreTypeDescriptions.FeatureService)
                        {
                            ArcGISPortal portal = ArcGISPortalManager.Current.GetActivePortal();
                            if (portal == null)
                            {
                                //logger.Warn($"The portal with uri of {portalUri} was not found in the portal manager.");
                                throw new Exception($"Portal hosting the utility network was not found ({portal.PortalUri})... Please add the portal and log in.");
                            }

                            var token = portal.GetToken();

                            string traceConfigUrl = string.Empty;
                            CIMDataConnection dataConn = featureLayer.GetDataConnection();
                            if (dataConn is CIMStandardDataConnection stDataConn)
                            {
                                string[] splitConnectionStr = stDataConn.WorkspaceConnectionString.Split(';');
                                string urlParam = splitConnectionStr?.FirstOrDefault(val => val.Contains("URL"));
                                string unUrl = urlParam?.Split('=')[1];
                                traceConfigUrl = unUrl.Replace("FeatureServer", "UtilityNetworkServer/traceConfigurations/query");
                                traceConfigUrl = $"{traceConfigUrl}?f=json&token={token}";
                            }

                            EsriHttpResponseMessage response;
                            try
                            {
                                response = esriHttpClient.Get(traceConfigUrl);
                                response.EnsureSuccessStatusCode();
                            }
                            catch (Exception e)
                            {
                                throw e;
                            }

                            var json = response?.Content?.ReadAsStringAsync()?.Result;
                            if (json == null)
                                throw new Exception("Failed to get data from trace configuration endpoint");

                            TraceConfigurationJSONMapping parsedJson = JsonConvert.DeserializeObject<TraceConfigurationJSONMapping>(json);

                            string globalids = string.Empty;
                            for (int i = 0; i < parsedJson.traceConfigurations.Length; i++)
                            {
                                //globalids needed for GP Tool
                                globalids += parsedJson.traceConfigurations[i].globalId + ";";

                                CSVLayout rec = new CSVLayout()
                                {
                                    Name = Common.EncloseStringInDoubleQuotes(Convert.ToString(parsedJson.traceConfigurations[i].name)),
                                    Description = Common.EncloseStringInDoubleQuotes(Convert.ToString(parsedJson.traceConfigurations[i].description)),
                                    Creator = Convert.ToString(parsedJson.traceConfigurations[i].creator)
                                };
                                csvLayoutList.Add(rec);
                            }

                            await CallGpTool(outputFile, globalids);
                        }

                        else
                        { 
                            //Get table definition for Trace Configuration table:  UN_<datasetid>_TraceConfigurations 
                            //  Example in file GDB:  UN_5_TraceConfigurations
                            TableDefinition traceConfigDefinition = geodatabase.GetDefinitions<TableDefinition>().FirstOrDefault(x => x.GetName().Contains("TraceConfigurations"));
                            using (Table table = geodatabase.OpenDataset<Table>(traceConfigDefinition.GetName()))
                            {
                                QueryFilter queryFilter = new QueryFilter
                                {
                                    SubFields = "GLOBALID, NAME, DESCRIPTION, CREATOR",
                                    PostfixClause = "ORDER BY NAME"
                                };

                                string globalids = string.Empty;

                                using (RowCursor rowCursor = table.Search(queryFilter, false))
                                {
                                    while (rowCursor.MoveNext())
                                    {
                                        using (Row row = rowCursor.Current)
                                        {
                                            //globalids needed for GP Tool
                                            globalids += row["GLOBALID"] + ";";

                                            CSVLayout rec = new CSVLayout()
                                            {
                                                Name = Common.EncloseStringInDoubleQuotes(Convert.ToString(row["NAME"])),
                                                Description = Common.EncloseStringInDoubleQuotes(Convert.ToString(row["DESCRIPTION"])),
                                                Creator = Convert.ToString(row["CREATOR"]),
                                            };
                                            csvLayoutList.Add(rec);
                                        }
                                    }
                                }

                                await CallGpTool(outputFile, globalids);
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
                    }
                }
            });
        }

        private static async Task CallGpTool(string outputFile, string globalids)
        {
            //https://pro.arcgis.com/en/pro-app/latest/tool-reference/utility-networks/export-trace-configurations.htm

            if (string.IsNullOrEmpty(globalids))
                return;

            UtilityNetworkLayer unLayer = GetUtilityNetworkLayer();
            if (unLayer is null)
                return;

            string traceConfigJsonFullPath = outputFile.Replace(".csv", ".json");
            IReadOnlyList<string> gpArgs = Geoprocessing.MakeValueArray(unLayer, globalids, traceConfigJsonFullPath);
            await Geoprocessing.ExecuteToolAsync("un.ExportTraceConfigurations", gpArgs);
        }

        private static UtilityNetworkLayer GetUtilityNetworkLayer()
        {
            IEnumerable<Layer> layers = MapView.Active.Map.GetLayersAsFlattenedList().OfType<UtilityNetworkLayer>();
            return layers.FirstOrDefault() as UtilityNetworkLayer;
        }

        private class CSVLayout
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Creator { get; set; }
        }
    }
}