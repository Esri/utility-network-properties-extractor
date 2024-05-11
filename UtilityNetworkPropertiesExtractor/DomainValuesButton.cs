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
    internal class DomainValuesButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Domain Values to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractDomainValuesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Domain Values");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractDomainValuesAsync()
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
                            int i = 0;
                            
                            string outputFile = Common.ConstructCsvFileName("DomainValues", dataSourceInMap.NameForCSV);                       
                            using (StreamWriter sw = new StreamWriter(outputFile))
                            {
                                //Header information
                                Common.WriteHeaderInfoForGeodatabase(sw, dataSourceInMap, "Domains");
                                sw.WriteLine("Number of Domains," + geodatabase.GetDomains().Count);
                                sw.WriteLine();

                                //Get all properties defined in the class.  This will be used to generate the CSV file
                                CSVLayout emptyRec = new CSVLayout();
                                PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                                //Write column headers based on properties in the class
                                string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                                sw.WriteLine(columnHeader);

                                List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                                IEnumerable<Domain> domainsList = geodatabase.GetDomains().OrderBy(x => x.GetName());
                                foreach (Domain domain in domainsList)
                                {
                                    i += 1;
                                    CSVLayout rec = new CSVLayout()
                                    {
                                        ID = i.ToString(),
                                        DomainName = domain.GetName(),
                                        FieldType = domain.GetFieldType().ToString(),
                                        Description = Common.EncloseStringInDoubleQuotes(domain.GetDescription()),
                                        SplitPolicy = domain.SplitPolicy.ToString(),
                                        MergePolicy = domain.MergePolicy.ToString()
                                    };

                                    if (domain is RangeDomain rangeDomain)
                                    {
                                        //Excel was formatting range of "3 - 15" as a date.  Added double dash so that it would appear in Excel as a string
                                        rec.Range = Common.EncloseStringInDoubleQuotes(rangeDomain.GetMinValue().ToString() + " -- " + rangeDomain.GetMaxValue().ToString());
                                    }

                                    csvLayoutList.Add(rec);

                                    //Write domain values on individual lines
                                    if (domain is CodedValueDomain codedValueDomain)
                                    {
                                        SortedList<object, string> codedValuePairs = codedValueDomain.GetCodedValuePairs();
                                        foreach (var pair in codedValuePairs)
                                        {
                                            rec = new CSVLayout()
                                            {
                                                DomainName = domain.GetName(),
                                                Code = pair.Key.ToString(),
                                                // Value = Common.EncloseStringInDoubleQuotes(pair.Value)
                                                Value = Common.EncloseStringInDoubleQuotes(pair.Value.Replace("\"", "\"\""))
                                            };
                                            csvLayoutList.Add(rec);
                                        }
                                    }
                                }

                                //Write body of CSV
                                foreach (CSVLayout row in csvLayoutList)
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

        private class CSVLayout
        {
            public string ID { get; set; }
            public string DomainName { get; set; }
            public string FieldType { get; set; }
            public string Code { get; set; }
            public string Value { get; set; }
            public string Range { get; set; }
            public string SplitPolicy { get; set; }
            public string MergePolicy { get; set; }
            public string Description { get; set; }
        }
    }
}