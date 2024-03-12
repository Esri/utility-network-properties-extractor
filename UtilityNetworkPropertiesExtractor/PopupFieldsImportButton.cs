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
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Button = ArcGIS.Desktop.Framework.Contracts.Button;
using MessageBox = System.Windows.Forms.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    /// <summary>
    /// Reads in a CSV file and updates these Popup settings in the map.
    /// 1.  Popup Order
    /// 2.  Visible
    /// </summary>
    /// 
    internal class PopupFieldsImportButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                //Prompt user to pick the CSV to import
                string csvToProcess = FilePicker();
                if (string.IsNullOrEmpty(csvToProcess))
                    return;

                //Confirm before processing file
                DialogResult dialogResult = MessageBox.Show("Apply popup field settings from " + Environment.NewLine + Environment.NewLine +
                     csvToProcess + Environment.NewLine + Environment.NewLine +
                    "To Map: " + MapView.Active.Map,
                    "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (dialogResult == DialogResult.Yes)
                {
                    DateTime startTime = DateTime.Now;

                    //Process the file
                    ImportPopupFieldsFromCsvAsync(csvToProcess, startTime);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Import Popup Fields From CSV");
            }
        }

        public static Task ImportPopupFieldsFromCsvAsync(string csvToProcess, DateTime startTime)
        {
            return QueuedTask.Run(() =>
            {
                //Get all settings from CSV File
                List<CSVLayout> allPopupFieldSettingsInCSVList = ReadCSV(csvToProcess);
                if (allPopupFieldSettingsInCSVList.Count == 0)
                {
                    MessageBox.Show("No popup field settings found in CSV file: " + Environment.NewLine +
                       csvToProcess, "Import Popup Fields Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    return;
                }

                MapView.Active.DrawingPaused = true;
                bool cancelled = false;

                using (ProgressDialog progress = new ProgressDialog("Processing", "Canceled", (uint)allPopupFieldSettingsInCSVList.Count, false))
                {
                    //Get layers and standalone tables in the map
                    List<BasicFeatureLayer> basicFeatureLayerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<BasicFeatureLayer>().ToList();
                    IReadOnlyList<StandaloneTable> standaloneTablesList = MapView.Active.Map.StandaloneTables;
                    uint tocCount = (uint)basicFeatureLayerList.Count + (uint)standaloneTablesList.Count;

                    CancelableProgressorSource cps = new CancelableProgressorSource(progress) { Max = tocCount };
                    string progressMessage;

                    QueuedTask.Run(() =>
                    {
                        //Get Distinct LayerName and ClassName combintations.  The layer name may not be unique.  
                        //Ex:  In Subtype Group Layers, the Unknown layer exists in every Domain Network Class.  
                        IEnumerable<CSVLayout> distinctGroupings = allPopupFieldSettingsInCSVList.GroupBy(m => new { m.ClassName, m.LayerName }).Select(m => m.FirstOrDefault()).ToList();
                        foreach (CSVLayout grouping in distinctGroupings)
                        {
                            //if user clicks the cancel button, stop processing.
                            if (cps.Progressor.CancellationToken.IsCancellationRequested)
                            {
                                cancelled = true;
                                break;
                            }

                            progressMessage = "Processing layer, " + grouping.LayerName + " (" + grouping.ClassName + ") --> " + cps.Progressor.Value + " of " + tocCount;
                            cps.Progressor.Value += 1;
                            cps.Progressor.Status = (cps.Progressor.Value * 100 / cps.Progressor.Max) + @"% Completed";
                            cps.Progressor.Message = progressMessage;

                            //For each layer, order the fields based the attribute:  Field Order
                            IEnumerable<CSVLayout> popupFieldSettingsInCSVList = allPopupFieldSettingsInCSVList.Where(x => x.ClassName == grouping.ClassName && x.LayerName == grouping.LayerName).OrderBy(x => x.PopupOrder);
                            UpdatePopupFieldSettings(popupFieldSettingsInCSVList, basicFeatureLayerList, standaloneTablesList);
                        }
                    }, cps.Progressor);
                }

                if (cancelled)
                    return;

                //When this block of code was in OnClick method, the messagebox appeared for a second and was gone.  No idea why.
                DateTime endTime = DateTime.Now;
                string timeDifference = Common.DetermineTimeDifference(startTime, endTime);

                MapView.Active.DrawingPaused = false;

                MessageBox.Show("You MUST save the Pro Project for the popup field setting changes to persist!\n\n Duration: " + timeDifference, "Popup Field settings were imported", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        private static void UpdatePopupFieldSettings(IEnumerable<CSVLayout> popupFieldSettingsInCSVList, List<BasicFeatureLayer> featureLayerList, IReadOnlyList<StandaloneTable> standaloneTablesList)
        {
            try
            {
                CSVLayout firstRecord = popupFieldSettingsInCSVList.FirstOrDefault();
                foreach (BasicFeatureLayer basicFeatureLayer in featureLayerList)
                {
                    using (Table table = basicFeatureLayer.GetTable())
                    {
                        if (table == null)  // broken datasource
                            continue;

                        if (table.GetName() == firstRecord.ClassName && basicFeatureLayer.Name == firstRecord.LayerName)
                        {
                            //Found the layer to update
                            CIMFeatureLayer cimFeatureLayerDef = basicFeatureLayer.GetDefinition() as CIMFeatureLayer;

                            //Update popup field order with values from CSV
                            CIMPopupInfo newPopupInfo = SetPopupFieldSettings(popupFieldSettingsInCSVList, cimFeatureLayerDef.PopupInfo, cimFeatureLayerDef.FeatureTable.DisplayField);
                            if (newPopupInfo != null)
                            {
                                cimFeatureLayerDef.PopupInfo = newPopupInfo;
                                basicFeatureLayer.SetDefinition(cimFeatureLayerDef);
                            }
                            return;
                        }
                    }
                }

                //if made it here, the CSV entry is either a table or isn't in the map
                foreach (StandaloneTable standaloneTable in standaloneTablesList)
                {
                    using (Table table = standaloneTable.GetTable())
                    {
                        if (table == null)  // broken datasource
                            continue;

                        if (table.GetName() == firstRecord.ClassName && standaloneTable.Name == firstRecord.LayerName)
                        {
                            //Found the table to update
                            CIMStandaloneTable cimStandaloneDef = standaloneTable.GetDefinition();

                            //Update popup field order with values from CSV
                            CIMPopupInfo newPopupInfo = SetPopupFieldSettings(popupFieldSettingsInCSVList, cimStandaloneDef.PopupInfo, cimStandaloneDef.DisplayField);
                            if (newPopupInfo != null)
                            {
                                cimStandaloneDef.PopupInfo = newPopupInfo;
                                standaloneTable.SetDefinition(cimStandaloneDef);
                            }
                            return;
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "UpdatePopupFieldSettings");
            }
        }

        private static CIMPopupInfo SetPopupFieldSettings(IEnumerable<CSVLayout> layerFieldSettingsInCSVList, CIMPopupInfo cimPopupInfo, string primaryDisplayField)
        {
            List<string> popupOrderList = new List<string>();
            foreach (CSVLayout rec in layerFieldSettingsInCSVList.Where(x => x.Visible is true))
                popupOrderList.Add(rec.FieldName);

            // If the CIM doesn't contain entries for the Popup, create it.
            //  Set the fileds to be those defined in the CSV as visible.
            if (cimPopupInfo == null)
            {
                CustomPopupDefinition popup = new CustomPopupDefinition()
                {
                    Title = CustomPopupDefinition.FormatTitle(CustomPopupDefinition.FormatFieldName(primaryDisplayField)),
                    TableMediaInfo = new TableMediaInfo(popupOrderList),
                    //OtherMediaInfos = {new AttachmentsMediaInfo()
                    //    {
                    //        AttachmentDisplayType = AttachmentDisplayType.PreviewFirst,
                    //        Title = "Attachment(s)"
                    //    } 
                    //}
                };

                return popup.CreatePopupInfo();
            }
            else  // Popup info found in the CIM.   Set the fileds to be those defined in the CSV as visible.
            {
                CIMMediaInfo[] cimMediaInfos = cimPopupInfo.MediaInfos;
                for (int j = 0; j < cimMediaInfos.Length; j++)
                {
                    if (cimMediaInfos[j] is CIMTableMediaInfo cimTableMediaInfo)
                    {
                        cimTableMediaInfo.Fields = popupOrderList.ToArray();
                        cimTableMediaInfo.UseLayerFields = false;
                        break;
                    }
                }
                return cimPopupInfo;
            }
        }

        private static List<CSVLayout> ReadCSV(string csvToProcess)
        {
            List<CSVLayout> csvList = new List<CSVLayout>();

            try
            {
                string line;
                bool startHere = false;
                Regex CSVParser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");

                using (StreamReader reader = new StreamReader(csvToProcess))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = CSVParser.Split(line);

                        //Skip over header info
                        if (startHere == false)
                        {
                            if (parts[0] == Common.FieldSettingsClassNameHeader.Replace(" ", ""))
                                startHere = true;
                        }
                        else
                        {
                            //Check if Field Order in CSV is an integer value.
                            //If can't be parsed, the fieldOrder value is set to 0 which will put it at the top of the attribute list.
                            //This will make easy to identify it's field order value is bad.
                            int.TryParse(parts[5], out int popupOrder);

                            CSVLayout rec = new CSVLayout
                            {
                                ClassName = parts[0],
                                LayerName = parts[1],
                                Subtype = parts[2],
                                FieldName = parts[3],
                                FieldAlias = parts[4],
                                PopupOrder = popupOrder,
                                Visible = Convert.ToBoolean(parts[6]),
                            };

                            //Some layer names will have double quotes around them because they contain commas.  The quotes need to be stripped out.
                            //  Ex:  "ElectricJunction - De-Energized, Traceable"
                            if (rec.LayerName.Contains("\""))
                                rec.LayerName = rec.LayerName.Substring(1, rec.LayerName.Length - 2);

                            //Some field alias descriptions will have double quotes around them because they contain commas.  The quotes need to be stripped out.
                            //  Ex:  "height: Height, Marker Height, Platform Height, Pole Height"
                            if (rec.FieldAlias.Contains("\""))
                                rec.FieldAlias = rec.FieldAlias.Substring(1, rec.FieldAlias.Length - 2);

                            csvList.Add(rec);
                        }
                    }
                }

                return csvList;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ReadCSV", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return csvList;
            }
        }

        private static string FilePicker()
        {
            //Specify a name to show in the filter dropdown combo box - otherwise the name will show as "Default"
            BrowseProjectFilter csvFilter = BrowseProjectFilter.GetFilter("esri_browseDialogFilters_browseFiles");
            csvFilter.FileExtension = "*.csv";//restrict to specific extensions as needed
            csvFilter.BrowsingFilesMode = true;
            csvFilter.Name = "CSV Files (*.csv)";

            //Use the filter with the OpenItemDialog
            OpenItemDialog dlg = new OpenItemDialog()
            {
                BrowseFilter = csvFilter,
                Title = "Select Popup Field Import CSV",
                InitialLocation = Common.ExtractFilePath,
            };

            //show the dialog and retrieve the selection if there was one
            if (!dlg.ShowDialog().Value)
                return string.Empty;

            Item item = dlg.Items.First();
            return item.Path;
        }

        private class CSVLayout
        {
            public string ClassName { get; set; }
            public string LayerName { get; set; }
            public string Subtype { get; set; }
            public string FieldName { get; set; }
            public string FieldAlias { get; set; }
            public int PopupOrder { get; set; }
            public bool Visible { get; set; }
        }
    }
}