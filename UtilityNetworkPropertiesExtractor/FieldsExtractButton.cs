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
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class FieldsExtractButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Fields Info to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractFieldsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extracting Fields Info");
            }
            finally
            {
                progDlg.Dispose();
            }
        }
        public static Task ExtractFieldsAsync()
        {
            return QueuedTask.Run(() =>
            {
                List<DataSourceInMap> dataSourceInMapList = DataSourcesInMapHelper.GetDataSourcesInMap();
                foreach (DataSourceInMap dataSourceInMap in dataSourceInMapList)
                {
                    if (dataSourceInMap.WorkspaceFactory != WorkspaceFactory.Shapefile.ToString())
                    {
                        using (Geodatabase geodatabase = dataSourceInMap.Geodatabase)
                        {
                            string outputFile = Common.ConstructCsvFileName("Fields", dataSourceInMap.NameForCSV);
                            using (StreamWriter sw = new StreamWriter(outputFile))
                            {
                                //Header information
                                Common.WriteHeaderInfoForGeodatabase(sw, dataSourceInMap, "Fields");

                                //Get all properties defined in the class.  This will be used to generate the CSV file
                                CSVLayout emptyRec = new CSVLayout();
                                PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                                //Write column headers based on properties in the class
                                string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                                sw.WriteLine(columnHeader);

                                List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                                //Featureclasses
                                IReadOnlyList<FeatureClassDefinition> featureClassDefinitions = geodatabase.GetDefinitions<FeatureClassDefinition>();
                                foreach (FeatureClassDefinition fcDefinition in featureClassDefinitions)
                                {
                                    try
                                    {
                                        IReadOnlyList<Field> fieldsList = fcDefinition.GetFields();
                                        IReadOnlyList<Subtype> subtypesList = fcDefinition.GetSubtypes();

                                        if (subtypesList.Count != 0)
                                        {
                                            //process each subtype in the featureclasss
                                            foreach (Subtype subtype in subtypesList)
                                                BuildFieldInfo(fcDefinition, subtype, fieldsList, ref csvLayoutList);
                                        }
                                        else
                                            BuildFieldInfo(fcDefinition, null, fieldsList, ref csvLayoutList);
                                    }
                                    catch (Exception ex)
                                    {
                                        if (ex.HResult != -2146233088) // No database permissions to perform the operation.
                                            MessageBox.Show(ex.Message);
                                    }
                                }

                                //Tables
                                IReadOnlyList<TableDefinition> tableDefinitions = geodatabase.GetDefinitions<TableDefinition>();
                                foreach (TableDefinition tableDefinition in tableDefinitions)
                                {
                                    try
                                    {
                                        IReadOnlyList<Field> fieldsList = tableDefinition.GetFields();
                                        IReadOnlyList<Subtype> subtypesList = tableDefinition.GetSubtypes();

                                        if (subtypesList.Count != 0)
                                        {
                                            //process each subtype in the table
                                            foreach (Subtype subtype in subtypesList)
                                                BuildFieldInfo(tableDefinition, subtype, fieldsList, ref csvLayoutList);
                                        }
                                        else
                                            BuildFieldInfo(tableDefinition, null, fieldsList, ref csvLayoutList);
                                    }
                                    catch (Exception ex)
                                    {
                                        if (ex.HResult != -2146233088) // No database permissions to perform the operation.
                                            MessageBox.Show(ex.Message);
                                    }
                                }

                                //Write body of report
                                foreach (CSVLayout row in csvLayoutList.OrderBy(x => x.ClassName))
                                {
                                    string output = Common.ExtractClassValuesToString(row, properties);
                                    sw.WriteLine(output);
                                }

                                sw.Flush();
                                sw.Close();
                            }
                        }
                    }
                }
            });
        }
        private static void BuildFieldInfo(TableDefinition tableDefinition, Subtype subtype, IReadOnlyList<Field> fieldsList, ref List<CSVLayout> csvLayoutList)
        {
            string defaultCode;
            string domainName = string.Empty;
            string defaultValue = string.Empty;
            string rangeValue = string.Empty;
            int? lengthOfStringField;
            int colPos = 0;

            foreach (Field field in fieldsList)
            {
                defaultCode = field.GetDefaultValue(subtype)?.ToString();

                Domain domain = field.GetDomain(subtype);
                if (domain != null)
                {
                    domainName = domain.GetName();
                    if (domain is CodedValueDomain codedValueDomain)
                    {
                        if (!string.IsNullOrEmpty(defaultCode))
                            defaultValue = Common.GetCodedValueDomainValue(codedValueDomain, defaultCode);
                    }
                    else if (domain is RangeDomain rangeDomain)
                    {
                        //Excel was formatting range of "3 - 15" as a date.  Added double dash so that it would appear in Excel as a string
                        rangeValue = rangeDomain.GetMinValue() + " -- " + rangeDomain.GetMaxValue();
                    }
                }

                if (field.FieldType == FieldType.String)
                    lengthOfStringField = field.Length;
                else
                    lengthOfStringField = null;

                colPos += 1;

                CSVLayout rec = new CSVLayout()
                {
                    ClassName = tableDefinition.GetName(),
                    Pos = colPos.ToString(),
                    FieldName = field.Name,
                    Alias = Common.EncloseStringInDoubleQuotes(field.AliasName),
                    FieldType = field.FieldType.ToString(),
                    FieldLength = lengthOfStringField.ToString(),
                    Scale = field.Scale.ToString(),
                    Precision = field.Precision.ToString(),
                    IsNullable = field.IsNullable.ToString(),
                    IsRequired = field.IsRequired.ToString(),
                    IsEditable = field.IsEditable.ToString(),
                    IsDomainFixed = field.IsDomainFixed.ToString(),
                    Domain = domainName,
                    DefaultCode = defaultCode,
                    DefaultValue = Common.EncloseStringInDoubleQuotes(defaultValue),
                    Range = Common.EncloseStringInDoubleQuotes(rangeValue)
                };

                if (subtype != null)
                {
                    rec.SubtypeCode = subtype.GetCode().ToString();
                    rec.Subtype = subtype.GetName();
                }

                csvLayoutList.Add(rec);

                domainName = string.Empty;
                defaultCode = string.Empty;
                defaultValue = string.Empty;
                rangeValue = string.Empty;
            }
        }
        private class CSVLayout
        {
            public string ClassName { get; set; }
            public string SubtypeCode { get; set; }
            public string Subtype { get; set; }
            public string Pos { get; set; }
            public string FieldName { get; set; }
            public string Alias { get; set; }
            public string FieldType { get; set; }
            public string FieldLength { get; set; }
            public string Precision { get; set; }
            public string Scale { get; set; }
            public string IsNullable { get; set; }
            public string IsRequired { get; set; }
            public string IsEditable { get; set; }
            public string IsDomainFixed { get; set; }
            public string Domain { get; set; }
            public string DefaultCode { get; set; }
            public string DefaultValue { get; set; }
            public string Range { get; set; }
        }
    }
}