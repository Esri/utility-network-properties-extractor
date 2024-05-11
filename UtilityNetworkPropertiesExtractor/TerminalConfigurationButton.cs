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
    internal class TerminalConfigurationButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Terminal Configuration to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractTerminalConfigurationAsync(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Terminal Configuration");
            }
            finally
            {
                progDlg.Dispose();
            }
        }
        public static Task ExtractTerminalConfigurationAsync(bool showNoUtilityNetworkPrompt)
        {
            return QueuedTask.Run(() =>
            {
                List<UtilityNetworkDataSourceInMap> utilityNetworkDataSourceInMapList = DataSourcesInMapHelper.GetUtilityNetworkDataSourcesInMap();
                if (utilityNetworkDataSourceInMapList.Count == 0)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("A Utility Network was not found in the active map", "Extract Terminal Configuration", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                foreach (UtilityNetworkDataSourceInMap utilityNetworkDataSourceInMap in utilityNetworkDataSourceInMapList)
                {
                    using (Geodatabase geodatabase = utilityNetworkDataSourceInMap.Geodatabase)
                    {
                        string outputFile = Common.ConstructCsvFileName("TerminalConfig", utilityNetworkDataSourceInMap.NameForCSV);
                        using (StreamWriter sw = new StreamWriter(outputFile))
                        {
                            //Header information
                            UtilityNetworkDefinition utilityNetworkDefinition = utilityNetworkDataSourceInMap.UtilityNetwork.GetDefinition();
                            Common.WriteHeaderInfoForUtilityNetwork(sw, utilityNetworkDataSourceInMap, "Terminal Configuration");

                            List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                            //Get all properties defined in the class.  This will be used to generate the CSV file
                            CSVLayout emptyRec = new CSVLayout();
                            PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                            //Write column headers based on properties in the class
                            string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                            sw.WriteLine(columnHeader);

                            IEnumerable<TerminalConfiguration> terminalConfigList = utilityNetworkDefinition.GetTerminalConfigurations().OrderBy(x => x.Name);
                            foreach (TerminalConfiguration terminalConfig in terminalConfigList)
                            {
                                CSVLayout rec = new CSVLayout()
                                {
                                    Name = terminalConfig.Name,
                                    DirectionalityModel = terminalConfig.Directionality.ToString()
                                };
                                csvLayoutList.Add(rec);

                                IReadOnlyList<Terminal> terminals = terminalConfig.Terminals;
                                foreach (Terminal terminal in terminals)
                                {
                                    rec = new CSVLayout()
                                    {
                                        ID = terminal.ID.ToString(),
                                        TerminalName = terminal.Name,
                                        UpstreamTerminal = terminal.IsUpstreamTerminal.ToString()
                                    };
                                    csvLayoutList.Add(rec);
                                }

                                // add blank line
                                csvLayoutList.Add(emptyRec);
                            }

                            //Write body
                            foreach (CSVLayout row in csvLayoutList)
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
            public string Name { get; set; }
            public string DirectionalityModel { get; set; }
            public string ID { get; set; }
            public string TerminalName { get; set; }
            public string UpstreamTerminal { get; set; }
        }
    }
} 