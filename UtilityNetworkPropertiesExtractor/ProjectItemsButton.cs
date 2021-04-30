using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.GeoProcessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Button = ArcGIS.Desktop.Framework.Contracts.Button;
using MessageBox = System.Windows.Forms.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class ProjectItemsButton : Button
    {
        private static string _fileName = string.Empty;
        private static bool _fileGenerated = false;

        protected override void OnClick()
        {
            try
            {
                ExtractProjectItem();
                if (_fileGenerated)
                    MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Project Items");
            }
        }

        public static void ExtractProjectItem()
        {
            _fileGenerated = false;

            Common.CreateOutputDirectory();

            string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _fileName = string.Format("{0}_{1}_ProjectItems.csv", dateFormatted, Common.GetProProjectName());
            string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

            using (StreamWriter sw = new StreamWriter(outputFile))
            {
                sw.WriteLine(DateTime.Now + "," + "Project Items");
                sw.WriteLine();
                sw.WriteLine("Project," + Project.Current.Path);
                sw.WriteLine();

                List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                //Get all properties defined in the class.  This will be used to generate the CSV file
                CSVLayout emptyRec = new CSVLayout();
                PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                //Write column headers based on properties in the class
                string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                sw.WriteLine(columnHeader);

                IEnumerable<Item> items = Project.Current.GetItems<Item>();
                foreach (Item item in items)
                {
                    CSVLayout rec = new CSVLayout()
                    {
                        ItemType = item.Type,
                        ItemName = item.Name,
                        Description = Common.EncloseStringInDoubleQuotes(item.Description)

                    };

                    if (item is HistoryProjectItem hpi)
                    {
                        rec.ItemType += " History"; //In the CSV, split out GP History from GP (tools)
                        rec.TimeStamp = hpi.TimeStamp.ToString();  // Although obsolete, the timestamp value is still there and accurate.
                    }

                    csvLayoutList.Add(rec);
                }

                //Write body of report
                foreach (CSVLayout row in csvLayoutList.OrderBy(x => x.ItemType).ThenBy(x => x.TimeStamp).ThenBy(x => x.ItemName))
                {
                    string output = Common.ExtractClassValuesToString(row, properties);
                    sw.WriteLine(output);
                }

                sw.Flush();
                sw.Close();
                _fileGenerated = true;
            }
        }

        private class CSVLayout
        {
            public string ItemType { get; set; }
            public string ItemName { get; set; }
            public string Description { get; set; }
            public string TimeStamp { get; set; }
        }
    }
}