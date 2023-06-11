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
    /// <summary>
    /// Reads in a CSV file and updates these field settings in the Web Map.
    /// 1.  Visible
    /// 2.  ReadOnly
    /// 3.  Highlighted 
    /// 4.  Alias
    /// 5.  Field Order
    /// </summary>
    
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

                            //For each layer, order the fields based the attribute:  Field Order
                            IEnumerable<CSVLayout> layerFieldSettingsInCSVList = allFieldSettingsInCSVList.Where(x => x.ClassName == grouping.ClassName && x.LayerName == grouping.LayerName).OrderBy(x => x.FieldOrder);
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
                CSVLayout firstRecord = layerFieldSettingsInCSVList.FirstOrDefault();
                foreach (BasicFeatureLayer basicFeatureLayer in featureLayerList)
                {
                    using (Table table = basicFeatureLayer.GetTable())
                    {
                        if (table == null)  // broken datasource
                            continue;

                        if (table.GetName() == firstRecord.ClassName && basicFeatureLayer.Name == firstRecord.LayerName)
                        {
                            //Found the layer to update;  get list of existing field descriptions
                            List<FieldDescription> fieldDescList = basicFeatureLayer.GetFieldDescriptions();

                            //Update field descriptions with values from CSV
                            List<FieldDescription> newFieldOrderList = SetFieldDescriptions(layerFieldSettingsInCSVList, fieldDescList);
                            if (newFieldOrderList.Count > 0)
                                basicFeatureLayer.SetFieldDescriptions(newFieldOrderList);

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
                            //Found the table to update; get list of existing field descriptions
                            List<FieldDescription> fieldDescList = standaloneTable.GetFieldDescriptions();

                            //Update field descriptions with values from CSV
                            List<FieldDescription> newFieldOrderList = SetFieldDescriptions(layerFieldSettingsInCSVList, fieldDescList);
                            if (newFieldOrderList.Count > 0)
                                standaloneTable.SetFieldDescriptions(newFieldOrderList);

                            return;
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "UpdateFieldSettings");
            }
        }
                
        private static List<FieldDescription> SetFieldDescriptions(IEnumerable<CSVLayout> layerFieldSettingsInCSVList, List<FieldDescription> fieldDescList)
        {
            //To update the layer's field order, you need to create a new list of FieldDescriptions
            List<FieldDescription> newFieldOrderList = new List<FieldDescription>();

            foreach(CSVLayout csvRecord in layerFieldSettingsInCSVList)
            {
                FieldDescription fieldDescription = fieldDescList.Where(x => x.Name == csvRecord.FieldName).FirstOrDefault();
                if (fieldDescription != null)
                {
                    fieldDescription.IsVisible = csvRecord.Visible;
                    fieldDescription.IsReadOnly = csvRecord.ReadOnly;
                    fieldDescription.IsHighlighted = csvRecord.Highlighted;
                    fieldDescription.Alias = csvRecord.FieldAlias;

                    newFieldOrderList.Add(fieldDescription);
                }
            }

            return newFieldOrderList;
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
                            //Check if Field Order in CSV is an integer value.
                            //If can't be parsed, the fieldOrder value is set to 0 which will put it at the top of the attribute list.
                            //This will make easy to identify it's field order value is bad.
                            int.TryParse(parts[4], out int fieldOrder);
                            
                            CSVLayout rec = new CSVLayout
                            {
                                ClassName = parts[0],
                                LayerName = parts[1],
                                SubtypeValue = parts[2],
                                FieldName = parts[3],
                                FieldOrder = fieldOrder,
                                Visible = Convert.ToBoolean(parts[5]),
                                ReadOnly = Convert.ToBoolean(parts[6]),
                                Highlighted = Convert.ToBoolean(parts[7]),
                                FieldAlias = parts[8]
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
            public string SubtypeValue { get; set; }
            public string FieldName { get; set; }
            public int FieldOrder { get; set; }
            public string FieldAlias { get; set; }
            public bool Visible { get; set; }
            public bool ReadOnly { get; set; }
            public bool Highlighted { get; set; }
        }
    }
}