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
    internal class FieldSettingsInMapImportButton : Button
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
                DialogResult dialogResult = MessageBox.Show("Apply map field settings from " + Environment.NewLine + Environment.NewLine +
                     csvToProcess + Environment.NewLine + Environment.NewLine +
                    "To Map: " + MapView.Active.Map,
                    "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (dialogResult == DialogResult.Yes)
                {
                    DateTime startTime = DateTime.Now;

                    //Process the file
                    ImportFieldSettingsFromCsvAsync(csvToProcess, startTime);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Import Field Settings From CSV");
            }
        }

        public static Task ImportFieldSettingsFromCsvAsync(string csvToProcess, DateTime startTime)
        {
            return QueuedTask.Run(() =>
            {
                //Get all settings from CSV File
                List<CSVLayout> allFieldSettingsInCSVList = ReadCSV(csvToProcess);
                if (allFieldSettingsInCSVList.Count == 0)
                {
                    MessageBox.Show("No map field settings found in CSV file: " + Environment.NewLine +
                       csvToProcess, "Import Field Map Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    return;
                }

                MapView.Active.DrawingPaused = true;
                bool cancelled = false;

                using (ProgressDialog progress = new ProgressDialog("Processing", "Canceled", (uint)allFieldSettingsInCSVList.Count, false))
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
                        IEnumerable<CSVLayout> distinctGroupings = allFieldSettingsInCSVList.GroupBy(m => new { m.ClassName, m.LayerName }).Select(m => m.FirstOrDefault()).ToList();
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

                            IEnumerable<CSVLayout> layerFieldSettingsInCSVList = allFieldSettingsInCSVList.Where(x => x.ClassName == grouping.ClassName && x.LayerName == grouping.LayerName);
                            UpdateFieldSettings(layerFieldSettingsInCSVList, basicFeatureLayerList, standaloneTablesList);
                        }
                    }, cps.Progressor);
                }

                if (cancelled)
                    return;

                //When this block of code was in OnClick method, the messagebox appeared for a second and was gone.  No idea why.
                DateTime endTime = DateTime.Now;
                string timeDifference = Common.DetermineTimeDifference(startTime, endTime);

                MapView.Active.DrawingPaused = false;

                MessageBox.Show("You MUST save the Pro Project for the field setting changes to persist!\n\n Duration: " + timeDifference, "Fields Settings were imported", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        private static void UpdateFieldSettings(IEnumerable<CSVLayout> layerFieldSettingsInCSVList, List<BasicFeatureLayer> featureLayerList, IReadOnlyList<StandaloneTable> standaloneTablesList)
        {
            try
            {
                bool applyChanges = false;
                CSVLayout firstRecord = layerFieldSettingsInCSVList.FirstOrDefault();
                foreach (BasicFeatureLayer basicFeatureLayer in featureLayerList)
                {
                    using (Table table = basicFeatureLayer.GetTable())
                    {
                        if (table.GetName() == firstRecord.ClassName && basicFeatureLayer.Name == firstRecord.LayerName)
                        {
                            //Found the layer to update
                            List<FieldDescription> fieldDescList = basicFeatureLayer.GetFieldDescriptions();
                            applyChanges = SetFieldDescriptions(layerFieldSettingsInCSVList, fieldDescList);

                            if (applyChanges)
                                basicFeatureLayer.SetFieldDescriptions(fieldDescList);

                            return;
                        }
                    }
                }

                //if made it here, the CSV entry is either a table or isn't in the map
                foreach (StandaloneTable standaloneTable in standaloneTablesList)
                {
                    if (standaloneTable.GetTable().GetName() == firstRecord.ClassName && standaloneTable.Name == firstRecord.LayerName)
                    {
                        //Found the table to update
                        List<FieldDescription> fieldDescList = standaloneTable.GetFieldDescriptions();
                        applyChanges = SetFieldDescriptions(layerFieldSettingsInCSVList, fieldDescList);

                        if (applyChanges)
                            standaloneTable.SetFieldDescriptions(fieldDescList);

                        return;
                    }
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "UpdateFieldSettings");
            }
        }

        private static bool SetFieldDescriptions(IEnumerable<CSVLayout> layerFieldSettingsInCSVList, List<FieldDescription> fieldDescList)
        {
            bool applyChanges = false;

            foreach (FieldDescription fieldDesc in fieldDescList)
            {
                CSVLayout csvRecord = layerFieldSettingsInCSVList.Where(x => x.FieldName == fieldDesc.Name).FirstOrDefault();
                if (csvRecord != null)
                {
                    fieldDesc.IsVisible = csvRecord.Visible;
                    fieldDesc.IsReadOnly = csvRecord.ReadOnly;
                    fieldDesc.IsHighlighted = csvRecord.Highlighted;
                    fieldDesc.Alias = csvRecord.FieldAlias;

                    applyChanges = true;  // found at least 1 field in this layer to be updated from the CSV.
                }
            }

            return applyChanges;
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
                            if (parts[0] == Common.FieldSettingsClassNameHeader)
                                startHere = true;
                        }
                        else
                        {
                            CSVLayout rec = new CSVLayout
                            {
                                ClassName = parts[0],
                                LayerName = parts[1],
                                FieldName = parts[2],
                                Visible = Convert.ToBoolean(parts[3]),
                                ReadOnly = Convert.ToBoolean(parts[4]),
                                Highlighted = Convert.ToBoolean(parts[5]),
                                FieldAlias = parts[6]
                            };

                            //Some field alias descriptions will have double quotes around them because they contain commas.  The quotes need to be stripped out
                            //  Ex:  StructureJunction,Unknown,height,TRUE,FALSE,TRUE,"height: Height, Marker Height, Platform Height, Pole Height"
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
                Title = "Select Field Mapping Import CSV",
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
            public string FieldName { get; set; }
            public string FieldAlias { get; set; }
            public bool Visible { get; set; }
            public bool ReadOnly { get; set; }
            public bool Highlighted { get; set; }
        }
    }
}
