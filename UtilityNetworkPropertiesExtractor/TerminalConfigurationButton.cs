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
    internal class TerminalConfigurationButton : Button
    {
        private static string _fileName = string.Empty;
        private static bool _fileGenerated = false;

        protected async override void OnClick()
        {
            try
            {
                await ExtractTerminalConfigurationAsync(true);
                if (_fileGenerated)
                    MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Terminal Configuration");
            }
        }
        public static Task ExtractTerminalConfigurationAsync(bool showNoUtilityNetworkPrompt)
        {
            _fileGenerated = false;

            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayerInUn);
                if (utilityNetwork == null)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("Utility Network not found in the active map", "Extract Terminal Configuration", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayerInUn);
                Common.CreateOutputDirectory();

                string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _fileName = string.Format("{0}_{1}_TerminalConfiguration.csv", dateFormatted, reportHeaderInfo.MapName);
                string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                using (StreamWriter sw = new StreamWriter(outputFile))
                {
                    //Header information
                    UtilityNetworkDefinition utilityNetworkDefinition = utilityNetwork.GetDefinition();
                    Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "Terminal Configuration");

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
                    _fileGenerated = true;
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