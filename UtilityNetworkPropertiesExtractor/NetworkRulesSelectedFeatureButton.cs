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
using ArcGIS.Desktop.Editing.Attributes;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class NetworkRulesSelectedFeatureButton : Button
    {
        protected async override void OnClick()
        {
            try
            {
                await ExtractNetworkRulesForFeatureAsync(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Network Rules");
            }
        }

        public static Task ExtractNetworkRulesForFeatureAsync(bool showNoUtilityNetworkPrompt)
        {
            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayerInUn);
                if (utilityNetwork == null)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("Utility Network not found in the active map", "Network Rules Selected Feature", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                UtilityNetworkDefinition utilityNetworkDefinition = utilityNetwork.GetDefinition();
                IReadOnlyList<NetworkSource> networkSourceList = utilityNetworkDefinition.GetNetworkSources();

                Dictionary<MapMember, List<long>> selectedFeatures = MapView.Active.Map.GetSelection();

                // get the first layer and its corresponding selected feature OIDs
                KeyValuePair<MapMember, List<long>> firstSelectionSet = selectedFeatures.First();

                // create an instance of the inspector class.  load the selected features into the inspector using a list of object IDs
                Inspector inspector = new Inspector();
                inspector.Load(firstSelectionSet.Key, firstSelectionSet.Value);

                if (inspector.HasAttributes && inspector.Count(a => a.FieldName.ToUpper() == "ASSETGROUP") > 0)
                {
                    int assetGroup = Convert.ToInt32(inspector["ASSETGROUP"]);
                    int assetType = Convert.ToInt32(inspector["ASSETTYPE"]);

                    Layer layer = firstSelectionSet.Key as Layer;

                    string fcName = GetNameFromLayer(firstSelectionSet.Key as Layer);
                    int? networkSourceID = networkSourceList.FirstOrDefault(source => source.Name == fcName)?.ID;
                    if (!networkSourceID.HasValue)
                    {
                        MessageBox.Show($"Couldn't find network source with name: {fcName}");
                        return;
                    }

                    int i = 1;
                    bool gotRule = false;
                    int idx = -1;
                    string message = string.Empty;
                    string titleBlock = string.Empty;

                    List<CSVLayoutRules> csvLayoutList = new List<CSVLayoutRules>();

                    IOrderedEnumerable<Rule> rulesList = utilityNetworkDefinition.GetRules().OrderBy(x => x.ID);
                    //IReadOnlyList<Rule> rulesList = utilityNetworkDefinition.GetRules();
                    foreach (Rule rule in rulesList)
                    {
                        IReadOnlyList<RuleElement> ruleElementList = rule.RuleElements;
                        //fromnetworksourceid = 12 and fromassetgroup = 9 and fromassettype = 100
                        if (ruleElementList[0].NetworkSource.ID == networkSourceID &&
                            ruleElementList[0].AssetGroup.Code == assetGroup &&
                            ruleElementList[0].AssetType.Code == assetType)
                        {
                            gotRule = true;
                            idx = 1;
                        }
                        else
                        {
                            //tonetworksourceid = 12 and toassetgroup = 9 and toassettype = 100
                            if (ruleElementList[1].NetworkSource.ID == networkSourceID &&
                                ruleElementList[1].AssetGroup.Code == assetGroup &&
                                ruleElementList[1].AssetType.Code == assetType)
                            {
                                gotRule = true;
                                idx = 0;
                            }
                        }

                        if (gotRule)
                        {
                            //i++;
                            //message += i + ".\t" + ruleElementList[idx].NetworkSource.Name + "\t" + ruleElementList[idx].AssetGroup.Name + "\t" + ruleElementList[idx].AssetType.Name + "\t" + ruleElementList[idx].Terminal?.Name + "\t" + rule.Type.ToString() + "\n";

                            CSVLayoutRules csvLayoutRules = new CSVLayoutRules()
                            { 
                                Nbr = i++.ToString() ,
                                RuleType = rule.Type.ToString(),
                                ClassName = ruleElementList[idx].NetworkSource.Name,
                                AssetGroup = ruleElementList[idx].AssetGroup.Name,
                                AssetType = ruleElementList[idx].AssetType.Name,
                                Terminal = ruleElementList[idx].Terminal?.Name
                            };

                            csvLayoutList.Add(csvLayoutRules);

                            if (string.IsNullOrEmpty(titleBlock))
                            {
                                int otherIdx = 0;
                                if (idx == 0)
                                    otherIdx = 1;

                                titleBlock = $"UN Rules for:  {ruleElementList[otherIdx].AssetGroup.Name} - {ruleElementList[otherIdx].AssetType.Name} {firstSelectionSet.Value.FirstOrDefault()}";
                            }
                            gotRule = false;
                        }
                    }

                    CSVLayoutRules emptyCountRec = new CSVLayoutRules();
                    PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyCountRec);

                    var distinctValues = csvLayoutList.Select(o => new { o.RuleType }).Distinct().OrderBy(o => o.RuleType);
                    foreach (var distinctVals in distinctValues)
                    {
                        message += distinctVals.RuleType + "\n";
                        foreach (CSVLayoutRules row in csvLayoutList.Where(x => x.RuleType == distinctVals.RuleType))
                        {
                            message += "\t" + 
                                       row.Nbr + "," +
                                       row.ClassName + "," +
                                       row.AssetGroup + "," +
                                       row.AssetType + "," +
                                       row.Terminal + "\n";
                        }
                    }

                    MessageBox.Show(message, titleBlock);
                
                    //This message box has a scrollbar but the form width doesn't expand
                    //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(message, titleBlock);
                }
                else
                    MessageBox.Show(firstSelectionSet.Key.Name + " - " + firstSelectionSet.Value.FirstOrDefault() + " No inspector values");
            });
        }
        

        private static string GetNameFromLayer(Layer layer)
        {
            CIMDataConnection dataConn = layer.GetDataConnection();
            string datasetName = null;
            if (dataConn is CIMFeatureDatasetDataConnection fdDataConn)
                datasetName = fdDataConn.Dataset;
            else if (dataConn is CIMStandardDataConnection stDataConn)
                datasetName = GetNameFromStdDataConnection(stDataConn);
            else
                MessageBox.Show($"The data connection is not a supported type: {dataConn.GetType()}");

            return datasetName;
        }

        /// <summary>
        /// Gets the name of the layer from the feature server using the given data connection
        /// </summary>
        /// <param name="stDataConn"></param>
        /// <returns></returns>
        private static string GetNameFromStdDataConnection(CIMStandardDataConnection stDataConn)
        {
            string[] splitConnectionStr = stDataConn.WorkspaceConnectionString.Split(';');
            string urlParam = splitConnectionStr?.FirstOrDefault(val => val.Contains("URL"));
            string url = urlParam?.Split('=')[1];
            if (url == null)
            {
                MessageBox.Show($"The connection string does not contain the URL... {stDataConn.WorkspaceConnectionString}");
                return null;
            }

            Uri connectionUri = new Uri(url);
            Uri portalUri = new Uri($"{connectionUri.Scheme}://{connectionUri.Host}/portal");
            ArcGISPortal unPortal = ArcGISPortalManager.Current.GetPortal(portalUri) ?? ArcGISPortalManager.Current.GetActivePortal();
            if (unPortal == null)
                throw new Exception($"Portal hosting the utility network was not found ({portalUri})... Please add the portal and log in.");

            string token = unPortal.GetToken();
            url = $"{url}/{stDataConn.Dataset}?f=json&token={token}";
            EsriHttpClient esriHttpClient = new EsriHttpClient();
            EsriHttpResponseMessage response = null;
            try
            {
                response = esriHttpClient.Get(url);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                throw e;
            }

            string json = response?.Content?.ReadAsStringAsync()?.Result;
            if (json == null)
            {
                MessageBox.Show("Failed to get data from feature layer URL");
                return null;
            }

            JSONMappings.ArcRestError error = JsonConvert.DeserializeObject<JSONMappings.ArcRestError>(json);
            if (error?.error != null)
            {
                if (error.error.message.Contains("Token"))
                {
                    throw new Exception("The service could not be accessed. Please login to the portal hosting the service and set it as your active portal.");
                }

                MessageBox.Show($"Error in response: {json}");
                throw new Exception(json);
            }

            JObject serviceMetadata = JObject.Parse(json);
            if (!serviceMetadata.ContainsKey("name"))
            {
                MessageBox.Show($"Couldn't find name value in response\nURL: {url}\nResponse: {json}");
                return null;
            }

            JToken name = serviceMetadata.GetValue("name");
            if (name.Type != JTokenType.String)
            {
                MessageBox.Show($"Name property in JSON response was not a string value.\nName: {name}");
                return null;
            }

            return name.ToString();
        }

        private class CSVLayoutRules
        {
            public string Nbr { get; set; }
            public string RuleType { get; set; }
            public string ClassName { get; set; }
            public string AssetGroup { get; set; }
            public string AssetType { get; set; }
            public string Terminal { get; set; }
            //public string ViaClassName { get; set; }
            //public string ViaAssetGroup { get; set; }
            //public string ViaAssetType { get; set; }
            //public string ViaTerminal { get; set; }
        }
    }
}