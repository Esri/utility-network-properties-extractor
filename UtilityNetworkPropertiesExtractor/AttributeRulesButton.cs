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
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class AttributeRulesButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Attribute Rule CSV(s) to:\n" + Common.ExtractFilePath); ;

            try
            {               
                progDlg.Show();

                await ExtractAttributeRulesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Attribute Rules");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static async Task ExtractAttributeRulesAsync()
        {
            await QueuedTask.Run(async () =>
            {
                string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string attrRuleFileName = string.Empty;

                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer firstFeatureLayer);

                //Only call this if there's an active mapview, otherwise we get a null reference exception
                if (utilityNetwork == null && MapView.Active != null)
                    firstFeatureLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().First();

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, firstFeatureLayer);
                Common.CreateOutputDirectory();

                Dictionary<string, Table> tablesDict = new Dictionary<string, Table>();

                //If Subtype Group layers are in the map, will have multiple layers pointing to same source featureclass
                //Populate Dictionary of distinct featureclasses
                IEnumerable<FeatureLayer> featureLayerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>();
                foreach (FeatureLayer featureLayer in featureLayerList)
                {
                    Table table = Common.GetTableFromFeatureLayer(featureLayer);
                    string tableName = table.GetName();

                    if (!tablesDict.ContainsKey(tableName))
                        tablesDict.Add(tableName, table);
                }

                //Standalone Tables
                IReadOnlyList<StandaloneTable> standaloneTableList = MapView.Active.Map.StandaloneTables;
                foreach (StandaloneTable standaloneTable in standaloneTableList)
                {
                    Table table = standaloneTable.GetTable();
                    string tableName = table.GetName();

                    if (!tablesDict.ContainsKey(tableName))
                        tablesDict.Add(tableName, table);
                }

                //Execute GP for each table in the dictionary
                foreach (KeyValuePair<string, Table> pair in tablesDict)
                {
                    string fcName = pair.Key;
                    int pos = pair.Key.LastIndexOf(".");

                    if (pos != -1) // strip off schema and owner of Featureclass Name (if exists).  Ex:  meh.unadmin.ElectricDevice
                        fcName = pair.Key.Substring(pos + 1);

                    attrRuleFileName = string.Format("{0}_{1}_AttributeRules_{2}.csv", dateFormatted, reportHeaderInfo.ProProjectName, fcName);
                    string attrRuleoutputFile = Path.Combine(Common.ExtractFilePath, attrRuleFileName);
                    string pathToTable = pair.Key;
                    IReadOnlyList<string> attrRuleArgs;

                    using (Datastore datastore = pair.Value.GetDatastore())
                    {
                        if (datastore is UnknownDatastore)
                            continue;

                        Uri uri = datastore.GetPath();
                        if (uri.AbsoluteUri.ToLower().Contains("https")) // can't extract Attibute Rules when layer's source is a FeatuerService
                            continue;

                        //Using the absolute path for local files results in escape characters for things like spaces, which the GP tool can't handle for input/output of local files.
                        var datastorePath = uri.LocalPath;

                        FeatureClass featureclass = pair.Value as FeatureClass;
                        FeatureDataset featureDataset = null;

                        if (featureclass != null)
                            featureDataset = featureclass.GetFeatureDataset();

                        if (featureDataset == null)
                        {
                            //<path to connfile>.sde/meh.unadmin.featureclass
                            pathToTable = string.Format("{0}\\{1}", datastorePath, pair.Value.GetName());
                        }
                        else
                        {
                            //<path to connfile>.sde/meh.unadmin.Electric\meh.unadmin.ElectricDevice
                            string featureDatasetName = featureclass.GetFeatureDataset().GetName();
                            pathToTable = string.Format("{0}\\{1}\\{2}", datastorePath, featureDatasetName, pair.Value.GetName());
                        }

                        //arcpy.management.ExportAttributeRules("DHC Line", r"C:\temp\DHCLine_AR_rules.CSV")
                        pathToTable = pathToTable.Replace("\\", "/");
                        attrRuleArgs = Geoprocessing.MakeValueArray(pathToTable, attrRuleoutputFile);
                        var result = await Geoprocessing.ExecuteToolAsync("management.ExportAttributeRules", attrRuleArgs);
                    }
                }

                //Delete files that only have 1 line (header) which means 0 Attribute Rules are assigned
                DirectoryInfo directoryInfo = new DirectoryInfo(Common.ExtractFilePath);
                List<FileInfo> blankFiles = directoryInfo.GetFiles().Where(f => f.Extension == ".csv" && f.Name.Contains("_AttributeRules")).ToList();
                foreach (FileInfo bf in blankFiles)
                {
                    string[] lines = File.ReadAllLines(bf.FullName);
                    int cnt = lines.Count();

                    if (cnt == 1)
                        bf.Delete();
                }

                //Delete the .xml files that are genereated by the GP tool
                List<FileInfo> deleteableFiles = directoryInfo.GetFiles().Where(f => f.Extension == ".xml" && f.Name.Contains("_AttributeRules")).ToList();
                foreach (FileInfo file in deleteableFiles)
                    file.Delete();

                FileInfo[] schemaIniFile = directoryInfo.GetFiles("schema.ini");
                foreach (FileInfo schemaIni in schemaIniFile)
                    schemaIni.Delete();
            });
        }
    }
}
