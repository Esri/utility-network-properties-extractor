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
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
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
        private static string _fileName = string.Empty;
        private static bool _fileGenerated = false;

        protected async override void OnClick()
        {
            try
            {
                await ExtractUNFeatureServiceInfo(true);
                if (_fileGenerated)
                    MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract UN FeatureService Information");
            }
        }

        public static Task ExtractUNFeatureServiceInfo(bool showNoUtilityNetworkPrompt)
        {
            _fileGenerated = false;

            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayer);
                if (utilityNetwork == null)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("Utility Network not found in the active map", "Extract UN FeatureService Info", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayer);
                if (reportHeaderInfo.SourceType != Common.DatastoreTypeDescriptions.FeatureService)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("The Utility Network layer is NOT from a FeatureService", "Extract UN FeatureService Info", MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;
                }

                Common.CreateOutputDirectory();
                string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _fileName = string.Format("{0}_{1}_UNFeatureServiceInfo.csv", dateFormatted, reportHeaderInfo.ProProjectName);
                string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                using (StreamWriter sw = new StreamWriter(outputFile))
                {
                    //Header information
                    UtilityNetworkDefinition utilityNetworkDefinition = utilityNetwork.GetDefinition();
                    Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "Utility Network FeatureService Information");
                    UtilityNetworkLayer unLayer = Common.FindTheUtilityNetworkLayer();

                    ArcGISPortal portal = ArcGISPortalManager.Current.GetActivePortal();
                    if (portal == null)
                        throw new Exception("You must be logged into portal to extract the Utility Network FeatureService Info");

                    string unFeatureServiceURL = GetUNFeatureServiceURL(unLayer, portal.GetToken());
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
                        new CSVLayout() { Title = "Has Versioned Data", Value = parsedJson.hasVersionedData.ToString() },
                        new CSVLayout() { Title = "Max Record Count", Value = parsedJson.maxRecordCount.ToString() },
                        new CSVLayout() { Title = "Supported Query Formats", Value = parsedJson.supportedQueryFormats.ToString() },
                        new CSVLayout() { Title = "Supports Query Data Elements", Value = parsedJson.supportsQueryDataElements.ToString() }
                    };

                    if (parsedJson.layers.Length > 0)
                    {
                        csvLayoutList.Add(emptyRec);
                        csvLayoutList.Add(new CSVLayout() { Title = "Layers" });
                    }

                    for (int i = 0; i < parsedJson.layers.Length; i++)
                    {
                        CSVLayout rec = new CSVLayout() { Value = $"{parsedJson.layers[i].name} ( {parsedJson.layers[i].id} )" };
                        csvLayoutList.Add(rec);
                    }

                    if (parsedJson.tables.Length > 0)
                    {
                        csvLayoutList.Add(emptyRec);
                        csvLayoutList.Add(new CSVLayout() { Title = "Tables" });
                    }

                    for (int i = 0; i < parsedJson.tables.Length; i++)
                    {
                        CSVLayout rec = new CSVLayout() { Value = $"{parsedJson.tables[i].name} ( {parsedJson.tables[i].id} )" };
                        csvLayoutList.Add(rec);
                    }

                    csvLayoutList.Add(emptyRec);
                    csvLayoutList.Add(new CSVLayout() { Title = "Service Item ID", Value = parsedJson.serviceItemId });
                    csvLayoutList.Add(new CSVLayout() { Title = "Spatial Reference", Value = $"{parsedJson.spatialReference.wkid} ({parsedJson.spatialReference.latestWkid})" });
                    csvLayoutList.Add(emptyRec);
                    csvLayoutList.Add(new CSVLayout() { Title = "Initial Extent" });
                    csvLayoutList.Add(new CSVLayout() { Value = $"XMin: {parsedJson.initialExtent.xmin}" });
                    csvLayoutList.Add(new CSVLayout() { Value = $"YMin: {parsedJson.initialExtent.ymin}" });
                    csvLayoutList.Add(new CSVLayout() { Value = $"XMax: {parsedJson.initialExtent.xmax}" });
                    csvLayoutList.Add(new CSVLayout() { Value = $"XMax: {parsedJson.initialExtent.ymax}" });
                    csvLayoutList.Add(new CSVLayout() { Value = $"Spatial Reference: {parsedJson.initialExtent.spatialReference.wkid} ({parsedJson.initialExtent.spatialReference.latestWkid})" });
                    csvLayoutList.Add(emptyRec);
                    csvLayoutList.Add(new CSVLayout() { Title = "Full Extent" });
                    csvLayoutList.Add(new CSVLayout() { Value = $"XMin: {parsedJson.fullExtent.xmin}" });
                    csvLayoutList.Add(new CSVLayout() { Value = $"YMin: {parsedJson.fullExtent.ymin}" });
                    csvLayoutList.Add(new CSVLayout() { Value = $"XMax: {parsedJson.fullExtent.xmax}" });
                    csvLayoutList.Add(new CSVLayout() { Value = $"XMax: {parsedJson.fullExtent.ymax}" });
                    csvLayoutList.Add(new CSVLayout() { Value = $"Spatial Reference: {parsedJson.fullExtent.spatialReference.wkid} ({parsedJson.fullExtent.spatialReference.latestWkid})" });
                    csvLayoutList.Add(emptyRec);
                    csvLayoutList.Add(new CSVLayout() { Title = "Units", Value = parsedJson.units });
                    csvLayoutList.Add(new CSVLayout() { Title = "Enable Z Defaults", Value = parsedJson.enableZDefaults.ToString() });
                    csvLayoutList.Add(new CSVLayout() { Title = "Supports ApplyEdits With Global Ids", Value = parsedJson.supportsApplyEditsWithGlobalIds.ToString() });
                    csvLayoutList.Add(new CSVLayout() { Title = "Support True Curves ", Value = parsedJson.supportsTrueCurve.ToString() });
                    csvLayoutList.Add(new CSVLayout() { Title = "Only Allow TrueCurve Updates By TrueCurveClients", Value = parsedJson.onlyAllowTrueCurveUpdatesByTrueCurveClients.ToString() });
                    csvLayoutList.Add(new CSVLayout() { Title = "Supports Return Service Edits Option", Value = parsedJson.supportsReturnServiceEditsOption.ToString() });
                    csvLayoutList.Add(new CSVLayout() { Title = "Supports Dynamic Layers", Value = parsedJson.supportsDynamicLayers.ToString() });

                    //Write body of CSV
                    PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);
                    foreach (CSVLayout row in csvLayoutList)
                    {
                        string output = Common.ExtractClassValuesToString(row, properties);
                        sw.WriteLine(output);
                    }

                    sw.Flush();
                    sw.Close();
                    _fileGenerated = true;
                }
            });
        }


        private static string GetUNFeatureServiceURL(UtilityNetworkLayer unLayer, string token)
        {
            string url = string.Empty;
            CIMDataConnection dataConn = unLayer.GetDataConnection();
            if (dataConn is CIMStandardDataConnection stDataConn)
            {
                //<WorkspaceConnectionString>URL=https://webAdaptor/server/rest/services/ElectricUN/FeatureServer</WorkspaceConnectionString>
                //<WorkspaceConnectionString>URL=https://webAdaptor/server/rest/services/ElectricUN/FeatureServer;VERSION=sde.default;...</WorkspaceConnectionString>
                url = stDataConn.WorkspaceConnectionString.Split('=')[1];
                int pos = url.IndexOf(";");
                if (pos > 0)  // if the URL contains VERSION details, strip that off.
                    url = url.Substring(0, pos);

                url = $"{url}?f=json&token={token}";
            }
            return url;
        }

        private class CSVLayout
        {
            public string Title { get; set; }
            public string Value { get; set; }
        }
    }
}