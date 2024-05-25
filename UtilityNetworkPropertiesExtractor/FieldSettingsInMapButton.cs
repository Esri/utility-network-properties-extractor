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
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Field Settings in Map to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractFieldSettingsInMapAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Field Settings in Map");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractFieldSettingsInMapAsync()
        {
            return QueuedTask.Run(() =>
            {
                string outputFile = Common.BuildCsvNameContainingMapName("FieldSettingsInMap");
                using (StreamWriter sw = new StreamWriter(outputFile))
                {
                    //Header information
                    Common.WriteHeaderInfoForMap(sw, "Field Settings in Map");
                    
                    IReadOnlyList<BasicFeatureLayer> basicFeatureLayerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<BasicFeatureLayer>().ToList();
                    IReadOnlyList<StandaloneTable> standaloneTableList = MapView.Active.Map.StandaloneTables;

                    sw.WriteLine("Layers," + basicFeatureLayerList.Count());
                    sw.WriteLine("Standalone Tables," + standaloneTableList.Count());
                    sw.WriteLine("");
                    sw.WriteLine("Note,Column headers with an * are the editable field settings");
                    sw.WriteLine();
                    sw.WriteLine(Common.FieldSettingsClassNameHeader + ",Layer Name,Subtype Value,Field Name,Field Order*,Visible*,Read-Only*,Highlight*,Field Alias*");

                    //Basic Feature Layers in the map
                    string subtypeValue = string.Empty;                   
                    foreach (BasicFeatureLayer basicFeatureLayer in basicFeatureLayerList)
                    {
                        using (Table table = basicFeatureLayer.GetTable())
                        {
                            if (table == null) // broken datasource.  Don't add to the csv
                                continue;

                            subtypeValue = string.Empty;
                            if (basicFeatureLayer is FeatureLayer fLayer)
                            {
                                if (fLayer.IsSubtypeLayer)
                                    subtypeValue = fLayer.SubtypeValue.ToString();
                            }
                            List<FieldDescription> fieldDescList = basicFeatureLayer.GetFieldDescriptions();
                            WriteFieldSettings(sw, basicFeatureLayer.Name, table.GetName(), subtypeValue, fieldDescList);
                        }
                    }

                    //Standalone Tables in the map
                    foreach (StandaloneTable standaloneTable in standaloneTableList)
                    {
                        using (Table table = standaloneTable.GetTable())
                        {
                            if (table == null) // broken datasource.  Don't add to the csv
                                continue;

                            List<FieldDescription> fieldDescList = standaloneTable.GetFieldDescriptions();
                            WriteFieldSettings(sw, standaloneTable.Name, table.GetName(), string.Empty, fieldDescList);
                        }
                    }

                    sw.Flush();
                    sw.Close();
                }
            });
        }

        private static void WriteFieldSettings(StreamWriter sw, string tocName, string className, string subtype, List<FieldDescription> fieldDescList)
        {
            int fieldOrder = 0;
            foreach (FieldDescription fieldDesc in fieldDescList)
            {
                fieldOrder += 1;
                sw.WriteLine(className + "," + Common.EncloseStringInDoubleQuotes(tocName) + "," + subtype + "," + fieldDesc.Name + "," + fieldOrder + "," + fieldDesc.IsVisible + "," + fieldDesc.IsReadOnly + "," + fieldDesc.IsHighlighted + "," + Common.EncloseStringInDoubleQuotes(fieldDesc.Alias));
            }
        }
    }
}