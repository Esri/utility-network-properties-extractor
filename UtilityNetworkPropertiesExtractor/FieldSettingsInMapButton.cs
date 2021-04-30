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
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UtilityNetworkPropertiesExtractor
{
    internal class FieldSettingsInMapButton : Button
    {
        private static string _fileName = string.Empty;

        protected async override void OnClick()
        {
            try
            {
                await ExtractFieldSettingsInMapAsync();
                MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Field Settings in Map");
            }
        }

        public static Task ExtractFieldSettingsInMapAsync()
        {
            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayer);
                if (utilityNetwork == null)
                    featureLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().First();

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayer);
                Common.CreateOutputDirectory();

                string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _fileName = string.Format("{0}_{1}_FieldSettingsInMap.csv", dateFormatted, reportHeaderInfo.ProProjectName);
                string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                using (StreamWriter sw = new StreamWriter(outputFile))
                {
                    //Header information
                    UtilityNetworkDefinition utilityNetworkDefinition = null;
                    if (utilityNetwork != null)
                        utilityNetworkDefinition = utilityNetwork.GetDefinition();

                    Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "Field Settings in Map");
                    sw.WriteLine("Map," + MapView.Active.Map.Name);
                    sw.WriteLine("Note,Column headers with an * are the editable field settings");
                    sw.WriteLine();

                    sw.WriteLine(Common.FieldSettingsClassNameHeader + ",Layer Name,Field Name,Visible*,Read-Only*,Highlight*,Field Alias*");

                    //Basic Feature Layers in the map
                    IReadOnlyList<BasicFeatureLayer> basicFeatureLayerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<BasicFeatureLayer>().ToList();
                    foreach (BasicFeatureLayer basicFeatureLayer in basicFeatureLayerList)
                    {
                        using (Table table = basicFeatureLayer.GetTable())
                        {
                            List<FieldDescription> fieldDescList = basicFeatureLayer.GetFieldDescriptions();
                            WriteFieldSettings(sw, basicFeatureLayer.Name, table.GetName(), fieldDescList);
                        }
                    }

                    //Standalone Tables in the map
                    IReadOnlyList<StandaloneTable> standaloneTableList = MapView.Active.Map.StandaloneTables;
                    foreach (StandaloneTable standaloneTable in standaloneTableList)
                    {
                        List<FieldDescription> fieldDescList = standaloneTable.GetFieldDescriptions();
                        WriteFieldSettings(sw, standaloneTable.Name, standaloneTable.GetTable().GetName(), fieldDescList);
                    }

                    sw.Flush();
                    sw.Close();
                }
            });
        }

        private static void WriteFieldSettings(StreamWriter sw, string tocName, string className, List<FieldDescription> fieldDescList)
        {
            foreach (FieldDescription fieldDesc in fieldDescList)
                sw.WriteLine(className + "," + tocName + "," + fieldDesc.Name + "," + fieldDesc.IsVisible + "," + fieldDesc.IsReadOnly + "," + fieldDesc.IsHighlighted + "," + Common.EncloseStringInDoubleQuotes(fieldDesc.Alias));
        }
    }
}
