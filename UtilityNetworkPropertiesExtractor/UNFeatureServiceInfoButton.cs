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
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace UtilityNetworkPropertiesExtractor
{
    internal class UNFeatureServiceInfoButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting UN Featureservice Info to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractUNFeatureServiceInfo(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract UN Featureservice Info");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractUNFeatureServiceInfo(bool showNoUtilityNetworkPrompt)
        {
            return QueuedTask.Run(() =>
            {
                List<UtilityNetworkDataSourceInMap> utilityNetworkDataSourceInMapList = DataSourcesInMapHelper.GetUtilityNetworkDataSourcesInMap();
                if (utilityNetworkDataSourceInMapList.Count == 0)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("A Utility Network was not found in the active map", "Extract Asset Groups", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                foreach (UtilityNetworkDataSourceInMap utilityNetworkDataSourceInMap in utilityNetworkDataSourceInMapList)
                {
                    if (utilityNetworkDataSourceInMap.WorkspaceFactory != WorkspaceFactory.FeatureService.ToString())
                    {
                        if (showNoUtilityNetworkPrompt)
                            MessageBox.Show("The Utility Network layer is NOT from a FeatureService", "Extract UN FeatureService Info", MessageBoxButton.OK, MessageBoxImage.Warning);

                        continue;
                    }

                    using (Geodatabase geodatabase = utilityNetworkDataSourceInMap.Geodatabase)
                    { 
                        string outputFile = Common.CreateCsvFile("UNFeatureServiceInfo", utilityNetworkDataSourceInMap.NameForCSV);
                        using (StreamWriter sw = new StreamWriter(outputFile))
                        {
                            //Header information
                            UtilityNetworkDefinition utilityNetworkDefinition = utilityNetworkDataSourceInMap.UtilityNetwork.GetDefinition();
                            Common.WriteHeaderInfoForUtilityNetwork(sw, utilityNetworkDataSourceInMap, "Utility Network FeatureService Information");


                            ArcGISPortal portal = ArcGISPortalManager.Current.GetActivePortal();
                            if (portal == null)
                                throw new Exception("You must be logged into portal to extract the Utility Network FeatureService Info");

                            string unFeatureServiceURL = Common.AppendTokenToUrl(utilityNetworkDataSourceInMap.URI, portal.GetToken());
                            EsriHttpResponseMessage response = Common.QueryRestPointUsingGet(unFeatureServiceURL);

                            string json = response?.Content?.ReadAsStringAsync()?.Result;
                            if (json == null)
                                throw new Exception("Failed to get data from Utility Network Feature Service endpoint");

                            JSONMappings.ArcRestError arcRestError = JsonConvert.DeserializeObject<JSONMappings.ArcRestError>(json);
                            if (arcRestError?.error != null)
                                throw new Exception(arcRestError?.error.code + " - " + arcRestError?.error.message + "\n" + unFeatureServiceURL);

                            JSONMappings.FeatureServiceJSONMapping parsedJson = JsonConvert.DeserializeObject<JSONMappings.FeatureServiceJSONMapping>(json);

                            CSVLayout emptyRec = new CSVLayout();
                            List<CSVLayout> csvLayoutList = new List<CSVLayout>
                            {
                                new CSVLayout() { ColumnA = "Has Versioned Data", ColumnB = parsedJson.hasVersionedData.ToString() },
                                new CSVLayout() { ColumnA = "Max Record Count", ColumnB = parsedJson.maxRecordCount.ToString() },
                                new CSVLayout() { ColumnA = "Supported Query Formats", ColumnB = parsedJson.supportedQueryFormats.ToString() },
                                new CSVLayout() { ColumnA = "Supports Query Data Elements", ColumnB = parsedJson.supportsQueryDataElements.ToString() },
                                new CSVLayout() { ColumnA = "Capabilities", ColumnB = Common.EncloseStringInDoubleQuotes(parsedJson.capabilities.ToString()) }
                            };

                            if (parsedJson.layers.Length > 0)
                            {
                                csvLayoutList.Add(emptyRec);
                                csvLayoutList.Add(new CSVLayout() { ColumnA = "Layers", ColumnB = "Layer Name", ColumnC = "Layer ID" });

                                for (int i = 0; i < parsedJson.layers.Length; i++)
                                {
                                    CSVLayout rec = new CSVLayout() { ColumnB = parsedJson.layers[i].name, ColumnC = parsedJson.layers[i].id.ToString() };
                                    csvLayoutList.Add(rec);
                                }
                            }

                            if (parsedJson.tables.Length > 0)
                            {
                                csvLayoutList.Add(emptyRec);
                                csvLayoutList.Add(new CSVLayout() { ColumnA = "Tables", ColumnB = "Table Name", ColumnC = "Table ID" });

                                for (int i = 0; i < parsedJson.tables.Length; i++)
                                {
                                    CSVLayout rec = new CSVLayout() { ColumnB = parsedJson.tables[i].name, ColumnC = parsedJson.tables[i].id.ToString() };
                                    csvLayoutList.Add(rec);
                                }
                            }

                            csvLayoutList.Add(emptyRec);
                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Service Item ID", ColumnB = parsedJson.serviceItemId });
                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Spatial Reference", ColumnB = $"{parsedJson.spatialReference.wkid} ({parsedJson.spatialReference.latestWkid})" });
                            csvLayoutList.Add(emptyRec);
                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Initial Extent" });
                            csvLayoutList.Add(new CSVLayout() { ColumnB = "XMin", ColumnC = parsedJson.initialExtent.xmin.ToString() });
                            csvLayoutList.Add(new CSVLayout() { ColumnB = "YMin", ColumnC = parsedJson.initialExtent.ymin.ToString() });
                            csvLayoutList.Add(new CSVLayout() { ColumnB = "XMax", ColumnC = parsedJson.initialExtent.xmax.ToString() });
                            csvLayoutList.Add(new CSVLayout() { ColumnB = "XMax", ColumnC = parsedJson.initialExtent.ymax.ToString() });
                            csvLayoutList.Add(new CSVLayout() { ColumnB = "Spatial Reference", ColumnC = $"{parsedJson.initialExtent.spatialReference.wkid} ({parsedJson.initialExtent.spatialReference.latestWkid})" });
                            csvLayoutList.Add(emptyRec);
                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Full Extent" });
                            csvLayoutList.Add(new CSVLayout() { ColumnB = "XMin", ColumnC = parsedJson.fullExtent.xmin.ToString() });
                            csvLayoutList.Add(new CSVLayout() { ColumnB = "YMin", ColumnC = parsedJson.fullExtent.ymin.ToString() });
                            csvLayoutList.Add(new CSVLayout() { ColumnB = "XMax", ColumnC = parsedJson.fullExtent.xmax.ToString() });
                            csvLayoutList.Add(new CSVLayout() { ColumnB = "XMax", ColumnC = parsedJson.fullExtent.ymax.ToString() });
                            csvLayoutList.Add(new CSVLayout() { ColumnB = "Spatial Reference", ColumnC = $"{parsedJson.fullExtent.spatialReference.wkid} ({parsedJson.fullExtent.spatialReference.latestWkid})" });
                            csvLayoutList.Add(emptyRec);
                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Units", ColumnB = parsedJson.units });
                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Enable Z Defaults", ColumnB = parsedJson.enableZDefaults.ToString() });
                            csvLayoutList.Add(emptyRec);
                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Sync Capabilities" });
                            csvLayoutList.Add(new CSVLayout() { ColumnB = "Sync Enabled", ColumnC = parsedJson.syncEnabled.ToString() });
                            if (parsedJson.syncEnabled)
                            {
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Supports Registering Existing Data", ColumnC = parsedJson.syncCapabilities.supportsRegisteringExistingData.ToString() });
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Supports SyncDirection Control", ColumnC = parsedJson.syncCapabilities.supportsSyncDirectionControl.ToString() });
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Supports Per Layer Sync", ColumnC = parsedJson.syncCapabilities.supportsPerLayerSync.ToString() });
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Supports Per Replica Sync", ColumnC = parsedJson.syncCapabilities.supportsPerReplicaSync.ToString() });
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Supports Rollback On Failure", ColumnC = parsedJson.syncCapabilities.supportsRollbackOnFailure.ToString() });
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Supports Async", ColumnC = parsedJson.syncCapabilities.supportsAsync.ToString() });
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Supports Attachments Sync Direction", ColumnC = parsedJson.syncCapabilities.supportsAttachmentsSyncDirection.ToString() });
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Supports Sync Model None", ColumnC = parsedJson.syncCapabilities.supportsSyncModelNone.ToString() });
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Version Creation Rule", ColumnC = parsedJson.syncCapabilities.versionCreationRule.ToString() });
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Supported Sync Data Options", ColumnC = parsedJson.syncCapabilities.supportedSyncDataOptions.ToString() });
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Supports Bi Directional Sync For Server", ColumnC = parsedJson.syncCapabilities.supportsBiDirectionalSyncForServer.ToString() });
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Supports Version", ColumnC = parsedJson.syncCapabilities.advancedReplicasResourceCapabilities.supportsVersion.ToString() });
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Supports Return Version", ColumnC = parsedJson.syncCapabilities.advancedReplicasResourceCapabilities.supportsReturnVersion.ToString() });
                                csvLayoutList.Add(new CSVLayout() { ColumnB = "Supports Return Last Sync Date", ColumnC = parsedJson.syncCapabilities.advancedReplicasResourceCapabilities.supportsReturnLastSyncDate.ToString() });
                            }

                            csvLayoutList.Add(emptyRec);
                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Supports ApplyEdits With Global Ids", ColumnB = parsedJson.supportsApplyEditsWithGlobalIds.ToString() });
                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Support True Curves ", ColumnB = parsedJson.supportsTrueCurve.ToString() });
                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Only Allow TrueCurve Updates By TrueCurveClients", ColumnB = parsedJson.onlyAllowTrueCurveUpdatesByTrueCurveClients.ToString() });
                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Supports Return Service Edits Option", ColumnB = parsedJson.supportsReturnServiceEditsOption.ToString() });
                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Supports Dynamic Layers", ColumnB = parsedJson.supportsDynamicLayers.ToString() });

                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Enable Z Defaults", ColumnB = parsedJson.enableZDefaults.ToString() });
                            if (parsedJson.enableZDefaults)
                                csvLayoutList.Add(new CSVLayout() { ColumnA = "Z Default", ColumnB = parsedJson.zDefault.ToString() });

                            csvLayoutList.Add(new CSVLayout() { ColumnA = "Allow Update Without M Values", ColumnB = parsedJson.allowUpdateWithoutMValues.ToString() });

                            //Write body of CSV
                            PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);
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
            });
        }

        private class CSVLayout
        {
            public string ColumnA { get; set; }
            public string ColumnB { get; set; }
            public string ColumnC { get; set; }
        }
    }
}