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

        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting trace configuration files to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();

                await ExtractTraceConfigurationAsync(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Trace Configuration");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static async Task ExtractTraceConfigurationAsync(bool showNoUtilityNetworkPrompt)
        {
            //Extracts a CSV file of all trace names along with a JSON file of the actual configuration
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

                        UtilityNetworkLayer unLayer = Common.FindTheUtilityNetworkLayer();

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
                                throw new Exception("You must be logged into portal to extract the trace configuration's JSON.");

                            string traceConfigUrl = GetTraceConfigurationQueryUrl(unLayer, portal.GetToken());
                            EsriHttpResponseMessage response = Common.QueryRestPointUsingGet(traceConfigUrl);

                            string json = response?.Content?.ReadAsStringAsync()?.Result;
                            if (json == null)
                                throw new Exception("Failed to get data from trace configuration endpoint");

                            JSONMappings.ArcRestError arcRestError = JsonConvert.DeserializeObject<JSONMappings.ArcRestError>(json);
                            if (arcRestError?.error != null)
                                throw new Exception(arcRestError?.error.code + " - " + arcRestError?.error.message + "\n" + traceConfigUrl);

                            string globalids = string.Empty;
                            JSONMappings.TraceConfigurationJSONMapping parsedJson = JsonConvert.DeserializeObject<JSONMappings.TraceConfigurationJSONMapping>(json);
                            for (int i = 0; i < parsedJson.traceConfigurations.Length; i++)
                            {
                                //globalids needed for GP Tool
                                globalids += parsedJson.traceConfigurations[i].globalId + ";";

                                CSVLayout rec = new CSVLayout()
                                {
                                    Name = Common.EncloseStringInDoubleQuotes(Convert.ToString(parsedJson.traceConfigurations[i].name)),
                                    Description = Common.EncloseStringInDoubleQuotes(Convert.ToString(parsedJson.traceConfigurations[i].description)),
                                    Creator = Convert.ToString(parsedJson.traceConfigurations[i].creator),
                                    CreationDate = Convert.ToString(Common.ConvertEpochTimeToReadableDate(parsedJson.traceConfigurations[i].creationDate))
                                };
                                csvLayoutList.Add(rec);
                            }

                            await CallGpToolAsync(unLayer, globalids, outputFile);
                        }
                        else  // File Geodatabase or Database connection
                        {
                            //Get table definition for Trace Configuration table:  UN_<datasetid>_TraceConfigurations 
                            //  Example in file GDB:  UN_5_TraceConfigurations
                            TableDefinition traceConfigDefinition = geodatabase.GetDefinitions<TableDefinition>().FirstOrDefault(x => x.GetName().Contains("TraceConfigurations"));
                            using (Table table = geodatabase.OpenDataset<Table>(traceConfigDefinition.GetName()))
                            {
                                QueryFilter queryFilter = new QueryFilter
                                {
                                    SubFields = "GLOBALID, NAME, DESCRIPTION, CREATOR, CREATIONDATE",
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
                                                CreationDate = Convert.ToString(row["CREATIONDATE"])
                                            };
                                            csvLayoutList.Add(rec);
                                        }
                                    }
                                }
                                await CallGpToolAsync(unLayer, globalids, outputFile);
                            }
                        }

                        //Write body of CSV
                        foreach (CSVLayout row in csvLayoutList.OrderBy(x => x.Name))
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


        private static string GetTraceConfigurationQueryUrl(UtilityNetworkLayer unLayer, string token)
        {
            string url = Common.GetURLOfUtilityNetworkLayer(unLayer, token);

            //Tweak the UN Layer URL to point to the TraceConfigurations Query endpoint
            return url.Replace("FeatureServer", "UtilityNetworkServer/traceConfigurations/query");

            //CIMDataConnection dataConn = unLayer.GetDataConnection();
            //if (dataConn is CIMStandardDataConnection stDataConn)
            //{
            //    //<WorkspaceConnectionString>URL=https://webAdaptor/server/rest/services/ElectricUN/FeatureServer</WorkspaceConnectionString>
            //    //<WorkspaceConnectionString>URL=https://webAdaptor/server/rest/services/ElectricUN/FeatureServer;VERSION=sde.default;...</WorkspaceConnectionString>
            //    url = stDataConn.WorkspaceConnectionString.Split('=')[1];
            //    int pos = url.IndexOf(";");
            //    if (pos > 0)  // if the URL contains VERSION details, strip that off.
            //        url = url.Substring(0, pos);

            //    url = url.Replace("FeatureServer", "UtilityNetworkServer/traceConfigurations/query");
            //    url = $"{url}?f=json&token={token}";
            //}
            //return url;
        }

        private static async Task CallGpToolAsync(UtilityNetworkLayer unLayer, string globalids, string outputFile)
        {
            if (string.IsNullOrEmpty(globalids))
                return;

            //https://pro.arcgis.com/en/pro-app/latest/tool-reference/utility-networks/export-trace-configurations.htm
            string traceConfigJsonFullPath = outputFile.Replace(".csv", ".json");
            IReadOnlyList<string> gpArgs = Geoprocessing.MakeValueArray(unLayer, globalids, traceConfigJsonFullPath);
            await Geoprocessing.ExecuteToolAsync("un.ExportTraceConfigurations", gpArgs);
        }

        private class CSVLayout
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Creator { get; set; }
            public string CreationDate { get; set; }
        }
    }
}