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
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
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
                //If Subtype Group layers are in the map, will have multiple layers pointing to same source featureclass
                //Populate Dictionary of distinct featureclasses and tables
                Dictionary<string, TableAndDataSource> tablesDict = GeoprocessingPrepHelper.BuildDictionaryOfDistinctObjectsInMap();

                //Execute GP for each table in the dictionary
                foreach (KeyValuePair<string, TableAndDataSource> pair in tablesDict)
                {
                    TableAndDataSource tableAndDataSource = pair.Value;

                    //Get gdb path to specific object
                    string pathToObject = GeoprocessingPrepHelper.BuildPathForObject(tableAndDataSource);

                    //Strip off database name and owner (if exists)
                    string objectName = Common.StripDatabaseOwnerAndSchema(pair.Key);

                    //build output CSV file name
                    string attrRuleOutputFile = Common.BuildCsvName($"AttributeRules_{objectName}", tableAndDataSource.DataSourceName);

                    //arcpy.management.ExportAttributeRules("DHC Line", r"C:\temp\DHCLine_AR_rules.CSV")
                    IReadOnlyList<string> attrRuleArgs = Geoprocessing.MakeValueArray(pathToObject, attrRuleOutputFile);
                    var result = await Geoprocessing.ExecuteToolAsync("management.ExportAttributeRules", attrRuleArgs);
                }

                //Now delete any files that were generated but didn't have any Attribute Rules assigned
                GeoprocessingPrepHelper.DeleteEmptyFiles("_AttributeRules");

            });
        }

    }
}