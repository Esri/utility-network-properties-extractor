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
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class ExtractAllButton : Button
    {
        protected async override void OnClick()
        {
            ProgressDialog progDlg = new ProgressDialog("Extracting to:\n" + Common.GetExtractFilePath());
            try
            {
                progDlg.Show();

                DateTime startTime = DateTime.Now;

                await AssetGroupsButton.ExtractAssetGroupsAsync(false);
                await NetworkAttributesButton.ExtractNetworkAttributesAsync(false);
                await NetworkCategoriesButton.ExtractNetworkCategoriesAsync(false);
                await NetworkRulesButton.ExtractNetworkRulesAsync(false);
                await NoNetworkRulesButton.ExtractNoNetworkRulesAsync(false);
                await DomainNetworksButton.ExtractDomainNetworksAsync(false);
                await TerminalConfigurationButton.ExtractTerminalConfigurationAsync(false);
                await TraceConfigurationButton.ExtractTraceConfigurationAsync(false);
                await DomainValuesButton.ExtractDomainValuesAsync();
                await DomainAssignmentsButton.ExtractDomainAssignmentsAsync();
                await DomainOrphansButton.ExtractOrphanDomainsAsync();
                await FieldsExtractButton.ExtractFieldsAsync();
                await LayerInfoButton.ExtractLayerInfoAsync();
                await VersionInfoButton.ExtractVersionInfoAsync(false);
                await AttributeRulesButton.ExtractAttributeRulesAsync();
                await ContingentValuesButton.ExtractContingentValuesAsync();
                await FieldSettingsInMapButton.ExtractFieldSettingsInMapAsync();
                await NetworkDiagramsButton.ExtractNetworkDiagramsAsync(false);
                await GdbObjectNamesButton.ExtractGdbObjectNamesAsync();
                await RelationshipClassButton.ExtractRelationshipClassesAsync();
                ProjectItemsButton.ExtractProjectItem();

                DateTime endTime = DateTime.Now;

                string timeDifference = Common.DetermineTimeDifference(startTime, endTime);

                MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "Duration: " + timeDifference, "CSV files were generated", System.Windows.MessageBoxButton.OK);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract All");
            }
            finally
            {
                progDlg.Dispose();
            }
        }
    }
}
