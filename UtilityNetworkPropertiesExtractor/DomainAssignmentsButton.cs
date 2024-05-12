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
    internal class DomainAssignmentsButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Domain Assignments to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractDomainAssignmentsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Domain Assignments");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractDomainAssignmentsAsync()
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
                            string outputFile = Common.CreateCsvFile("DomainAssignments", dataSourceInMap.NameForCSV);
                            using (StreamWriter sw = new StreamWriter(outputFile))
                            {
                                //Header information
                                Common.WriteHeaderInfoForGeodatabase(sw, dataSourceInMap, "Domain Assignments");

                                //Get all properties defined in the class.  This will be used to generate the CSV file
                                CSVLayout emptyRec = new CSVLayout();
                                PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                                //Write column headers based on properties in the class
                                string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                                sw.WriteLine(columnHeader);

                                List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                                IReadOnlyList<FeatureClassDefinition> featureClassDefinitions = geodatabase.GetDefinitions<FeatureClassDefinition>();
                                foreach (FeatureClassDefinition fcDefinition in featureClassDefinitions)
                                {
                                    try
                                    {
                                        IReadOnlyList<Field> listOfFields = fcDefinition.GetFields();
                                        IReadOnlyList<Subtype> subtypes = fcDefinition.GetSubtypes();

                                        if (subtypes.Count != 0)
                                        {
                                            foreach (Subtype subtype in subtypes)
                                                BuildDomainAssignments(fcDefinition, subtype, listOfFields, ref csvLayoutList);
                                        }
                                        else
                                            BuildDomainAssignments(fcDefinition, null, listOfFields, ref csvLayoutList);
                                    }
                                    catch (Exception ex)
                                    {
                                        if (ex.HResult != -2146233088) // No database permissions to perform the operation.
                                            MessageBox.Show(ex.Message);
                                    }
                                }

                                IReadOnlyList<TableDefinition> tableDefinitions = geodatabase.GetDefinitions<TableDefinition>();
                                foreach (TableDefinition tableDefinition in tableDefinitions)
                                {
                                    try
                                    {
                                        IReadOnlyList<Field> listOfFields = tableDefinition.GetFields();
                                        IReadOnlyList<Subtype> subtypes = tableDefinition.GetSubtypes();

                                        if (subtypes.Count != 0)
                                        {
                                            foreach (Subtype subtype in subtypes)
                                                BuildDomainAssignments(tableDefinition, subtype, listOfFields, ref csvLayoutList);
                                        }
                                        else
                                            BuildDomainAssignments(tableDefinition, null, listOfFields, ref csvLayoutList);
                                    }
                                    catch (Exception ex)
                                    {
                                        if (ex.HResult != -2146233088) // No database permissions to perform the operation.
                                            MessageBox.Show(ex.Message);
                                    }
                                }

                                //Write body of CSV
                                foreach (CSVLayout row in csvLayoutList.OrderBy(x => x.Domain))
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

        private static void BuildDomainAssignments(TableDefinition tableDefinition, Subtype subtype, IReadOnlyList<Field> listOfFields, ref List<CSVLayout> domainAssignmentsCSVList)
        {
            string defaultCode = string.Empty;
            string defaultValue = string.Empty;

            foreach (Field field in listOfFields)
            {
                Domain domain = field.GetDomain(subtype);
                if (domain != null)
                {
                    if (domain is CodedValueDomain)
                    {
                        CodedValueDomain cvd = domain as CodedValueDomain;
                        if (field.GetDefaultValue(subtype) != null)  //check first if Subtype has default value
                        {
                            defaultCode = field.GetDefaultValue(subtype).ToString();
                            defaultValue = cvd.GetName(field.GetDefaultValue(subtype));
                        }
                        else if (field.HasDefaultValue)
                        {
                            defaultCode = field.GetDefaultValue().ToString();
                            defaultValue = cvd.GetName(field.GetDefaultValue());
                        }
                    }

                    CSVLayout rec = new CSVLayout()
                    {
                        ClassName = tableDefinition.GetName(),
                        FieldName = field.Name,
                        FieldType = field.FieldType.ToString(),
                        Domain = domain.GetName(),
                        DefaultCode = defaultCode,
                        DefaultValue = Common.EncloseStringInDoubleQuotes(defaultValue)
                    };

                    if (subtype != null)
                    {
                        rec.SubtypeCode = subtype.GetCode().ToString();
                        rec.Subtype = subtype.GetName();
                    }
                    domainAssignmentsCSVList.Add(rec);

                    defaultCode = string.Empty;
                    defaultValue = string.Empty;
                }
            }
        }

        private class CSVLayout
        {
            public string Domain { get; set; }
            public string ClassName { get; set; }
            public string SubtypeCode { get; set; }
            public string Subtype { get; set; }
            public string FieldName { get; set; }
            public string FieldType { get; set; }
            public string DefaultCode { get; set; }
            public string DefaultValue { get; set; }
        }
    }
}
