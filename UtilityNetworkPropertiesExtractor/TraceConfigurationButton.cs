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
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class TraceConfigurationButton : Button
    {
        private static string _fileName = string.Empty;

        protected async override void OnClick()
        {
            try
            {
                await ExtractTraceConfigurationAsync(true);
                MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Trace Configuration");
            }
        }

        public static Task ExtractTraceConfigurationAsync(bool showNoUtilityNetworkPrompt)
        {
            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayer);
                if (utilityNetwork == null)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("Utility Network not found in the active map", "Extract Trace Configuration", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayer);

                using (Geodatabase geodatabase = featureLayer.GetTable().GetDatastore() as Geodatabase)
                {
                    Common.CreateOutputDirectory();
                    string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    _fileName = string.Format("{0}_{1}_TraceConfiguration.csv", dateFormatted, reportHeaderInfo.ProProjectName);
                    string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                    using (StreamWriter sw = new StreamWriter(outputFile))
                    {
                        //Header information
                        UtilityNetworkDefinition utilityNetworkDefinition = utilityNetwork.GetDefinition();
                        Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "Trace Configuration");

                        if (Convert.ToInt32(reportHeaderInfo.UtiltyNetworkSchemaVersion) < 5)
                        {
                            sw.WriteLine("Trace Configuration was introduced at Utility Network Version 5");
                            return;
                        }

                        //Get all properties defined in the class.  This will be used to generate the CSV file
                        CSVLayout emptyRec = new CSVLayout();
                        PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                        //Write column headers based on properties in the class
                        string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                        sw.WriteLine(columnHeader);

                        List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                        //Get table definition for Trace Configuration table:  UN_<datasetid>_TraceConfigurations 
                        //  Example in file GDB:  UN_5_TraceConfigurations
                        TableDefinition traceConfigDefinition = geodatabase.GetDefinitions<TableDefinition>().FirstOrDefault(x => x.GetName().Contains("TraceConfigurations"));
                        using (Table table = geodatabase.OpenDataset<Table>(traceConfigDefinition.GetName()))
                        {
                            QueryFilter queryFilter = new QueryFilter
                            {
                                SubFields = "NAME, DESCRIPTION, CREATOR, CREATIONDATE, LASTMODIFIEDDATE, TAGS",
                                PostfixClause = "ORDER BY NAME"
                            };

                            using (RowCursor rowCursor = table.Search(queryFilter, false))
                            {
                                while (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        CSVLayout rec = new CSVLayout()
                                        {
                                            Name = Common.EncloseStringInDoubleQuotes(Convert.ToString(row["NAME"])),
                                            Description = Common.EncloseStringInDoubleQuotes(Convert.ToString(row["DESCRIPTION"])),
                                            Creator = Convert.ToString(row["CREATOR"]),
                                            CreationDate = Convert.ToString(row["CREATIONDATE"]),
                                            LastModifiedDate = Convert.ToString(row["LASTMODIFIEDDATE"]),
                                            Tags = Common.EncloseStringInDoubleQuotes(Convert.ToString(row["TAGS"]).Replace("\"", ""))
                                        };
                                        csvLayoutList.Add(rec);
                                    }
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
            public string Name { get; set; }
            public string Description { get; set; }
            public string Creator { get; set; }
            public string CreationDate { get; set; }
            public string LastModifiedDate { get; set; }

            public string Tags { get; set; }
        }
    }
}