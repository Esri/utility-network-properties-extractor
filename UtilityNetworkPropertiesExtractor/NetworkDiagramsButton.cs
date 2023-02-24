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
using ArcGIS.Core.Data.NetworkDiagrams;
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
    internal class NetworkDiagramsButton : Button
    {
        private static string _fileName = string.Empty;
        private static bool _fileGenerated = false;

        protected async override void OnClick()
        {
            try
            {
                await ExtractNetworkDiagramsAsync(true);
                if (_fileGenerated)
                    MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Network Diagram Info");
            }
        }

        public static Task ExtractNetworkDiagramsAsync(bool showNoUtilityNetworkPrompt)
        {
            _fileGenerated = false;

            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayerInUn);
                if (utilityNetwork == null)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("Utility Network not found in the active map", "Extract Network Diagram Info", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayerInUn);
                Common.CreateOutputDirectory();

                string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _fileName = string.Format("{0}_{1}_NetworkDiagramInfo.csv", dateFormatted, reportHeaderInfo.MapName);
                string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                using (StreamWriter sw = new StreamWriter(outputFile))
                {
                    //Header information
                    UtilityNetworkDefinition utilityNetworkDefinition = utilityNetwork.GetDefinition();
                    Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "Network Diagram Info");

                    sw.WriteLine("DiagramTemplates,Name");
                    DiagramManager diagramManager = utilityNetwork.GetDiagramManager();
                    IEnumerable<DiagramTemplate> diagramTemplateList = diagramManager.GetDiagramTemplates().OrderBy(x => x.Name);
                    foreach (DiagramTemplate diagramTemplate in diagramTemplateList)
                        sw.WriteLine("," + diagramTemplate.Name);

                    sw.WriteLine();

                    List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                    //Get all properties defined in the class.  This will be used to generate the CSV file
                    CSVLayout emptyRec = new CSVLayout();
                    PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                    //Write column headers based on properties in the class
                    string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                    sw.WriteLine(columnHeader);

                    IReadOnlyList<NetworkDiagram> networkDiagramList = diagramManager.GetNetworkDiagrams();
                    foreach (NetworkDiagram diagram in networkDiagramList)
                    {
                        NetworkDiagramInfo diagramInfo = diagram.GetDiagramInfo();

                        CSVLayout rec = new CSVLayout
                        {
                            Name = diagram.Name,
                            CreatedTime = diagramInfo.CreationDate.ToString(),
                            LastModifiedTime = diagramInfo.LastUpdateDate.ToString(),
                            DiagramStorage = diagramInfo.IsStored.ToString(),
                            IsSystem = diagramInfo.IsSystem.ToString(),
                            ExtendDiagram = diagramInfo.CanExtend.ToString(),
                            Description = diagramInfo.Tag
                        };
                        csvLayoutList.Add(rec);
                    }

                    foreach (CSVLayout row in csvLayoutList.OrderBy(x => x.Name))
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
            public string NetworkDiagrams { get; set; }
            public string Name { get; set; }
            public string CreatedTime { get; set; }
            public string LastModifiedTime { get; set; }
            public string DiagramStorage { get; set; }
            public string IsSystem { get; set; }
            public string ExtendDiagram { get; set; }
            public string Description { get; set; }
        }
    }
}