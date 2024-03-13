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
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class PopupFieldsButton : Button
    {
        private static string _fileName = string.Empty;

        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Popup Fields to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractPopupFieldsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Popup Fields");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractPopupFieldsAsync()
        {
            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayer);
                if (utilityNetwork == null)
                    featureLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().First();

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayer);
                Common.CreateOutputDirectory();

                string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _fileName = string.Format("{0}_{1}_PopupFields.csv", dateFormatted, reportHeaderInfo.MapName);
                string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                using (StreamWriter sw = new StreamWriter(outputFile))
                {
                    //Header information
                    UtilityNetworkDefinition utilityNetworkDefinition = null;
                    if (utilityNetwork != null)
                        utilityNetworkDefinition = utilityNetwork.GetDefinition();

                    Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "Popup Fields");

                    IReadOnlyList<BasicFeatureLayer> basicFeatureLayerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<BasicFeatureLayer>().ToList();
                    IReadOnlyList<StandaloneTable> standaloneTableList = MapView.Active.Map.StandaloneTables;

                    sw.WriteLine("Map," + Common.GetActiveMapName());
                    sw.WriteLine("Layers," + basicFeatureLayerList.Count());
                    sw.WriteLine("Standalone Tables," + standaloneTableList.Count());
                    sw.WriteLine("");
                    sw.WriteLine("Note,Column headers with __ are the editable popup field settings");
                    sw.WriteLine();

                    //Get all properties defined in the class.  This will be used to generate the CSV file
                    CSVLayout emptyRec = new CSVLayout();
                    PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                    //Write column headers based on properties in the class
                    string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                    sw.WriteLine(columnHeader);

                    List<CSVLayout> csvLayoutList = new List<CSVLayout>();

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


                            bool useLayerFields = false;
                            string[] fieldsInPopup = null;
                            //Get all fields on the layer
                            List<FieldDescription> fieldDescList = basicFeatureLayer.GetFieldDescriptions();
                            CIMFeatureLayer cimFeatureLayer = basicFeatureLayer.GetDefinition() as CIMFeatureLayer;

                            //Get Fields defined in the Popup
                            if (cimFeatureLayer != null)
                                fieldsInPopup = GetFieldsInPopup(cimFeatureLayer.PopupInfo, ref useLayerFields);

                            //Build the CSV file
                            BuildPopupFieldsList(table.GetName(), basicFeatureLayer.Name, subtypeValue, useLayerFields, fieldDescList, fieldsInPopup, ref csvLayoutList);
                        }
                    }

                    //Standalone Tables in the map
                    foreach (StandaloneTable standaloneTable in standaloneTableList)
                    {
                        using (Table table = standaloneTable.GetTable())
                        {
                            if (table == null) // broken datasource.  Don't add to the csv
                                continue;

                            bool useLayerFields = false;
                            string[] fieldsInPopup = null;

                            //Get all fields on the table
                            List<FieldDescription> fieldDescList = standaloneTable.GetFieldDescriptions();
                            CIMStandaloneTable cimStandaloneTable = standaloneTable.GetDefinition();

                            //Get Fields defined in the Popup
                            fieldsInPopup = GetFieldsInPopup(cimStandaloneTable.PopupInfo, ref useLayerFields);

                            //Build the CSV file
                            BuildPopupFieldsList(table.GetName(), standaloneTable.Name, subtypeValue, useLayerFields, fieldDescList, fieldsInPopup, ref csvLayoutList);
                        }
                    }

                    //Write body of report
                    foreach (CSVLayout row in csvLayoutList)
                    {
                        string output = Common.ExtractClassValuesToString(row, properties);
                        sw.WriteLine(output);
                    }

                    sw.Flush();
                    sw.Close();
                }
            });
        }


        private static void BuildPopupFieldsList(string className, string tocName, string subtype, bool useLayerFields, List<FieldDescription> fieldsList, string[] fieldsInPopup, ref List<CSVLayout> csvLayoutList)
        {
            int popupOrder = 0;

            //useLayerFields:  In Pro, this option is checked:  Use visible fields and Arcade Expressions
            //In either case, use the setting defined at the fields level
            if (useLayerFields || fieldsInPopup == null)
            {
                foreach (FieldDescription fieldDescription in fieldsList)
                {
                    if (fieldDescription.Type == FieldType.Geometry)  // exclude geometry fields from the popup
                        continue;

                    popupOrder += 1;
                    CSVLayout rec = buildRec(className, tocName, subtype, popupOrder, fieldDescription.Name, fieldDescription.Alias, fieldDescription.IsVisible);
                    csvLayoutList.Add(rec);
                }
            }
            else  // Popup is deifined with list of fields to display
            {
                if (fieldsInPopup != null)
                {
                    bool fieldVisibility = false;

                    foreach (string fieldInPopup in fieldsInPopup)
                    {
                        if (!string.IsNullOrEmpty(fieldInPopup))
                        {
                            //Make sure the "Field In Popup" actually exists on the layer/table.
                            //  I've seen instances where LRS (Linear Referencing) fields were added to the list if the Utility Nework Layer was in the map. 
                            FieldDescription fieldDescription = fieldsList.Where(x => x.Name == fieldInPopup).FirstOrDefault();
                            if (fieldDescription != null || fieldInPopup.Contains("expression/"))  // not in Popup Fields List.   Add it to CSV
                            {
                                fieldVisibility = true;

                                string fieldAlias;
                                if (fieldInPopup.Contains("expression/"))
                                    fieldAlias = string.Empty;
                                else
                                {
                                    if (fieldDescription.Type == FieldType.Geometry)
                                        continue;

                                    fieldAlias = fieldDescription.Alias;
                                }

                                popupOrder += 1;
                                CSVLayout rec = buildRec(className, tocName, subtype, popupOrder, fieldInPopup, fieldAlias, fieldVisibility);
                                csvLayoutList.Add(rec);
                            }
                        }
                    }
                    // Now need to list out the other fields not defined in the popup.
                    foreach (FieldDescription fieldDescription in fieldsList)
                    {
                        if (fieldDescription.Type == FieldType.Geometry)
                            continue;

                        string result = Array.Find(fieldsInPopup, x => x == fieldDescription.Name);
                        if (string.IsNullOrEmpty(result))  // not in Popup Fields List.   If want to add more fields to the popup, those fields need to be in the CSV.
                        {
                            popupOrder += 1;
                            CSVLayout rec = buildRec(className, tocName, subtype, popupOrder, fieldDescription.Name, fieldDescription.Alias, false);
                            csvLayoutList.Add(rec);
                        }
                    }
                }
            }
        }

        private static CSVLayout buildRec(string className, string tocName, string subtype, int popupOrder, string fieldName, string fieldAlias, bool visible)
        {
            return new CSVLayout()
            {
                ClassName = className,
                LayerName = Common.EncloseStringInDoubleQuotes(tocName),
                SubtypeValue = subtype,
                PopupOrder__ = popupOrder.ToString(),
                FieldName = fieldName,
                FieldAlias = Common.EncloseStringInDoubleQuotes(fieldAlias),
                Visible__ = visible.ToString()
            };
        }

        private static string[] GetFieldsInPopup(CIMPopupInfo cimPopupInfo, ref bool useLayerFields)
        {
            bool useLayerFieldsVal = true;
            string[] fields = null;

            if (cimPopupInfo != null)
            {
                //determine if expression is visible in popup
                CIMMediaInfo[] cimMediaInfos = cimPopupInfo.MediaInfos;
                for (int j = 0; j < cimMediaInfos.Length; j++)
                {
                    if (cimMediaInfos[j] is CIMTableMediaInfo cimTableMediaInfo)
                    {
                        fields = cimTableMediaInfo.Fields;
                        useLayerFieldsVal = cimTableMediaInfo.UseLayerFields;
                    }
                }
            }

            useLayerFields = useLayerFieldsVal;
            return fields;
        }

        private class CSVLayout
        {
            public string ClassName { get; set; }
            public string LayerName { get; set; }
            public string SubtypeValue { get; set; }
            public string FieldName { get; set; }
            public string FieldAlias { get; set; }
            public string PopupOrder__ { get; set; }
            public string Visible__ { get; set; }
        }
    }
}