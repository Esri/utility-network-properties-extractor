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
    internal class VersionInfoButton : Button
    {
        private static string _fileName = string.Empty;

        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Version Info to:\n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();

                await ExtractVersionInfoAsync(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Version Info");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractVersionInfoAsync(bool showErrorPrompt)
        {
            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayer);
                if (utilityNetwork == null)
                    featureLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().First();

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayer);

                using (Geodatabase geodatabase = featureLayer.GetTable().GetDatastore() as Geodatabase)
                {
                    if (!geodatabase.IsVersioningSupported())
                    {
                        if (showErrorPrompt)
                            throw new Exception("Versioning is not supported in a " + reportHeaderInfo.SourceType);

                        return;
                    }

                    Common.CreateOutputDirectory();
                    string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    _fileName = string.Format("{0}_{1}_Versions.csv", dateFormatted, reportHeaderInfo.ProProjectName);
                    string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                    using (StreamWriter sw = new StreamWriter(outputFile))
                    {
                        //Header information
                        UtilityNetworkDefinition utilityNetworkDefinition = null;
                        if (utilityNetwork != null)
                            utilityNetworkDefinition = utilityNetwork.GetDefinition();

                        Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "Versions");

                        List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                        //Get all properties defined in the class.  This will be used to generate the CSV file
                        CSVLayout emptyRec = new CSVLayout();
                        PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                        using (VersionManager versionManager = geodatabase.GetVersionManager())
                        {
                            string owner = string.Empty;
                            string name = string.Empty;
                            string parentOwner = string.Empty;
                            string parentName = string.Empty;

                            IReadOnlyList<ArcGIS.Core.Data.Version> versionList = versionManager.GetVersions();

                            sw.WriteLine("Versioning Type," + versionManager.GetVersioningType().ToString());
                            sw.WriteLine("Version Count," + versionList.Count);
                            sw.WriteLine();

                            //Write column headers based on properties in the class
                            string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                            sw.WriteLine(columnHeader); ;

                            int i = 0;
                            foreach (ArcGIS.Core.Data.Version version in versionList)
                            {
                                //Parse version
                                ParseVersion(version, out owner, out name);

                                //Parse Parent (if exists)
                                if (!string.IsNullOrEmpty(version.GetParent()?.GetName()))
                                    ParseVersion(version.GetParent(), out parentOwner, out parentName);

                                i++;

                                CSVLayout rec = new CSVLayout()
                                {
                                    ID = i.ToString(),
                                    Name = name,
                                    Owner = owner,
                                    ParentName = parentName,
                                    ParentOwner = parentOwner,
                                    Description = Common.EncloseStringInDoubleQuotes(version.GetDescription()),
                                    Access = version.GetAccessType().ToString(),
                                    Created = version.GetCreatedDate().ToString(),
                                    Modified = version.GetModifiedDate().ToString()
                                };

                                if (version.GetName().ToUpper() != "SDE.DEFAULT")
                                    rec.HasConflicts = version.HasConflicts().ToString();

                                csvLayoutList.Add(rec);

                                //if traditional versioning, list out child versions
                                if (versionManager.GetVersioningType() == VersionType.Traditional)
                                {
                                    IReadOnlyList<ArcGIS.Core.Data.Version> childList = version.GetChildren();
                                    foreach (ArcGIS.Core.Data.Version child in childList)
                                    {
                                        CSVLayout childRec = new CSVLayout()
                                        {
                                            ChildVersions = child.GetName()
                                        };

                                        csvLayoutList.Add(childRec);
                                    }
                                }

                                parentOwner = string.Empty;
                                parentName = string.Empty;
                            }
                        }

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

        private static void ParseVersion(ArcGIS.Core.Data.Version version, out string owner, out string name)
        {
            int pos = version.GetName().LastIndexOf(".");
            owner = version.GetName().Substring(0, pos);
            name = version.GetName().Substring(pos + 1);
        }

        private class CSVLayout
        {
            public string ID { get; set; }
            public string Name { get; set; }
            public string Owner { get; set; }
            public string ParentName { get; set; }
            public string ParentOwner { get; set; }
            public string Description { get; set; }
            public string Access { get; set; }
            public string HasConflicts { get; set; }
            public string Created { get; set; }
            public string Modified { get; set; }
            public string ChildVersions { get; set; }
        }
    }
}