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
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
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
        protected override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Project Items to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                ExtractProjectItem();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Project Items");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static void ExtractProjectItem()
        {
            Common.CreateOutputDirectory();

            string outputFile = Common.BuildCsvNameContainingMapName("ProjectItems");
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

                    csvLayoutList.Add(rec);
                }

                //Write body of report
                foreach (CSVLayout row in csvLayoutList.OrderBy(x => x.ItemType).ThenBy(x => x.ItemName))
                {
                    string output = Common.ExtractClassValuesToString(row, properties);
                    sw.WriteLine(output);
                }

                sw.Flush();
                sw.Close();
            }
        }

        private class CSVLayout
        {
            public string ItemType { get; set; }
            public string ItemName { get; set; }
            public string Description { get; set; }
        }
    }
}