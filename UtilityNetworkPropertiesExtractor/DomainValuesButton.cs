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
    internal class DomainValuesButton : Button
    {
        private static string _fileName = string.Empty;

        protected async override void OnClick()
        {
            try
            {
                await ExtractDomainValuesAsync();
                MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Domain Values");
            }
        }

        public static Task ExtractDomainValuesAsync()
        {
            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayer);
                if (utilityNetwork == null)
                    featureLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().First();

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayer);

                using (Geodatabase geodatabase = featureLayer.GetTable().GetDatastore() as Geodatabase)
                {
                    Common.CreateOutputDirectory();
                    string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    _fileName = string.Format("{0}_{1}_DomainValues.csv", dateFormatted, reportHeaderInfo.ProProjectName);
                    string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                    int i = 0;
                    using (StreamWriter sw = new StreamWriter(outputFile))
                    {
                        //Header information
                        UtilityNetworkDefinition utilityNetworkDefinition = null;
                        if (utilityNetwork != null)
                            utilityNetworkDefinition = utilityNetwork.GetDefinition();

                        Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "Domain Values");
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
                                Description = Common.EncloseStringInDoubleQuotes(domain.GetDescription())
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
                                        Value = Common.EncloseStringInDoubleQuotes(pair.Value)
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
            public string Description { get; set; }
        }
    }
}
