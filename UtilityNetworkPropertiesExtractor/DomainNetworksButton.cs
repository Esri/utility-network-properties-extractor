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
using ArcGIS.Core.Data.UtilityNetwork.Trace;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class DomainNetworksButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Domain Networks to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractDomainNetworksAsync(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Domain Networks");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractDomainNetworksAsync(bool showNoUtilityNetworkPrompt)
        {
            return QueuedTask.Run(() =>
            {
                List<UtilityNetworkDataSourceInMap> utilityNetworkDataSourceInMapList = DataSourcesInMapHelper.GetUtilityNetworkDataSourcesInMap();
                if (utilityNetworkDataSourceInMapList.Count == 0)
                {
                    if (showNoUtilityNetworkPrompt)
                        MessageBox.Show("A Utility Network was not found in the active map", "Extract Domain Groups", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                foreach (UtilityNetworkDataSourceInMap utilityNetworkDataSourceInMap in utilityNetworkDataSourceInMapList)
                {
                    using (Geodatabase geodatabase = utilityNetworkDataSourceInMap.Geodatabase)
                    {
                        string outputFile = Common.BuildCsvName("DomainNetworks", utilityNetworkDataSourceInMap.Name);
                        using (StreamWriter sw = new StreamWriter(outputFile))
                        {
                            string output = string.Empty;

                            //Header information
                            UtilityNetworkDefinition utilityNetworkDefinition = utilityNetworkDataSourceInMap.UtilityNetwork.GetDefinition();
                            Common.WriteHeaderInfoForUtilityNetwork(sw, utilityNetworkDataSourceInMap, "Domain Networks");

                            //Network Topology section
                            CSVLayoutNetworkTopology emptyNetworkRec = new CSVLayoutNetworkTopology();
                            PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyNetworkRec);
                            string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                            sw.WriteLine(columnHeader);

                            List<CSVLayoutNetworkTopology> csvLayoutNetworkTopoList = new List<CSVLayoutNetworkTopology>();
                            NetworkTopologyInfo(utilityNetworkDataSourceInMap.UtilityNetwork, ref csvLayoutNetworkTopoList);
                            foreach (CSVLayoutNetworkTopology row in csvLayoutNetworkTopoList)
                            {
                                output = Common.ExtractClassValuesToString(row, properties);
                                sw.WriteLine(output);
                            }

                            //Domain Networks section
                            CSVLayoutDomainNetworks emptyRec = new CSVLayoutDomainNetworks();
                            properties = Common.GetPropertiesOfClass(emptyRec);
                            columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                            sw.WriteLine(columnHeader);

                            List<CSVLayoutDomainNetworks> csvLayoutDomainNetworksList = new List<CSVLayoutDomainNetworks>();
                            IReadOnlyList<DomainNetwork> domainNetworksList = utilityNetworkDefinition.GetDomainNetworks();
                            DomainNetworks(utilityNetworkDataSourceInMap, domainNetworksList, ref csvLayoutDomainNetworksList);
                            foreach (CSVLayoutDomainNetworks row in csvLayoutDomainNetworksList)
                            {
                                output = Common.ExtractClassValuesToString(row, properties);
                                sw.WriteLine(output);
                            }

                            //Tier Section
                            CSVLayoutTierInfo emptyTierRec = new CSVLayoutTierInfo();
                            properties = Common.GetPropertiesOfClass(emptyTierRec);
                            columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                            sw.WriteLine(columnHeader);

                            List<CSVLayoutTierInfo> csvLayoutTierInfo = new List<CSVLayoutTierInfo>();
                            TierInfo(utilityNetworkDataSourceInMap, domainNetworksList, ref csvLayoutTierInfo);
                            foreach (CSVLayoutTierInfo row in csvLayoutTierInfo)
                            {
                                output = Common.ExtractClassValuesToString(row, properties);
                                sw.WriteLine(output);
                            }

                            sw.Flush();
                            sw.Close();

                        }
                    }
                }
            });
        }

        private static void NetworkTopologyInfo(UtilityNetwork utilityNetwork, ref List<CSVLayoutNetworkTopology> csvLayoutNetworkTopoList)
        {
            //Build List of Network Topology Properties
            UtilityNetworkState utilityNetworkState = utilityNetwork.GetState();

            CSVLayoutNetworkTopology rec = new CSVLayoutNetworkTopology() 
            { 
                Property = "Is Enabled", 
                Value = utilityNetworkState.IsNetworkTopologyEnabled.ToString()
            };
            csvLayoutNetworkTopoList.Add(rec);

            rec = new CSVLayoutNetworkTopology() 
            { 
                Property = "Dirty Area Count", 
                Value = GetErrorCount(utilityNetwork, SystemTableType.DirtyAreas).ToString() 
            };
            csvLayoutNetworkTopoList.Add(rec);

            rec = new CSVLayoutNetworkTopology() 
            { 
                Property = "Last Full Validate Time", 
                Value = utilityNetworkState.LastConsistentMoment.ToString()
            };
            csvLayoutNetworkTopoList.Add(rec);

            rec = new CSVLayoutNetworkTopology();
            csvLayoutNetworkTopoList.Add(rec);
        }

        private static long GetErrorCount(UtilityNetwork utilityNetwork, SystemTableType systemTableType)
        {
            Table table = utilityNetwork.GetSystemTable(systemTableType);
            return table.GetCount();
        }

        private static void DomainNetworks(UtilityNetworkDataSourceInMap utilityNetworkDataSourceInMap, IReadOnlyList<DomainNetwork> domainNetworksList, ref List<CSVLayoutDomainNetworks> myDomainNetworksCSVList)
        {
            string tierGroupName = string.Empty;
            string updPolicyForContainers = string.Empty;
            string updPolicyForStructures = string.Empty;

            foreach (DomainNetwork domainNetwork in domainNetworksList)
            {
                CSVLayoutDomainNetworks networkRec = new CSVLayoutDomainNetworks()
                {
                    DomainNetworkID = domainNetwork.ID.ToString(),
                    DomainName = domainNetwork.Name,
                    Alias = domainNetwork.Alias,
                    TierDefinition = domainNetwork.TierDefinition.ToString(),
                    SubnetworkControllerType = domainNetwork.SubnetworkControllerType.ToString()
                };

                myDomainNetworksCSVList.Add(networkRec);

                foreach (Tier tier in domainNetwork.Tiers)
                {
                    //These properties didn't exist before UN version 4
                    if (Convert.ToInt32(utilityNetworkDataSourceInMap.SchemaVersion) >= 4)
                    {
                        tierGroupName = tier.TierGroup?.Name;
                        updPolicyForContainers = tier.HasUpdateSubnetworkPolicy(UpdateSubnetworkPolicy.Containers).ToString();
                        updPolicyForStructures = tier.HasUpdateSubnetworkPolicy(UpdateSubnetworkPolicy.Structures).ToString();
                    }

                    networkRec = new CSVLayoutDomainNetworks()
                    {
                        TierRank = tier.Rank.ToString(),
                        TierName = tier.Name,
                        TierGroup = tierGroupName,
                        SubnetworkFieldName = tier.SubnetworkFieldName,
                        TopologyType = tier.TopologyType.ToString(),
                        SupportDisjointSubnetworks = tier.IsDisjointSubnetworkSupported.ToString(),
                        EditModeInDefault = tier.GetEditModeForUpdateSubnetwork(VersionSpecification.DefaultVersion).ToString(),
                        EditModeInNamedVersion = tier.GetEditModeForUpdateSubnetwork(VersionSpecification.NamedVersion).ToString(),
                        UpdateSubnetworkContainers = updPolicyForContainers,
                        UpdateSubnetworkStructures = updPolicyForStructures,
                    };

                    myDomainNetworksCSVList.Add(networkRec);
                }
            }

            CSVLayoutDomainNetworks rec = new CSVLayoutDomainNetworks();
            myDomainNetworksCSVList.Add(rec);
        }

        private static void TierInfo(UtilityNetworkDataSourceInMap utilityNetworkDataSourceInMap, IReadOnlyList<DomainNetwork> domainNetworksList, ref List<CSVLayoutTierInfo> tierInfoCSVList)
        {

            foreach (DomainNetwork domainNetwork in domainNetworksList)
            {
                foreach (Tier tier in domainNetwork.Tiers)
                {
                    CSVLayoutTierInfo emptyTierRec = new CSVLayoutTierInfo()
                    {
                        TierName = tier.Name
                    };

                    CSVLayoutTierInfo rec = new CSVLayoutTierInfo()
                    {
                        TierRank = tier.Rank.ToString(),
                        TierName = tier.Name
                    };
                    tierInfoCSVList.Add(rec);

                    //Trace Configuration
                    rec = new CSVLayoutTierInfo()
                    {
                        TierName = tier.Name,
                        Property = "Trace Configuration",
                        Descriptor = "Property",
                        Value = "Value"
                    };
                    tierInfoCSVList.Add(rec);

                    rec = new CSVLayoutTierInfo()
                    {
                        TierName = tier.Name,
                        Descriptor = "Condition Barriers",
                        Value = tier.GetTraceConfiguration().Traversability.Barriers?.ToString(),
                    };
                    tierInfoCSVList.Add(rec);

                    rec = new CSVLayoutTierInfo()
                    {
                        TierName = tier.Name,
                        Descriptor = "Apply Traversibiilty To",
                        Value = tier.GetTraceConfiguration().Traversability.Scope.ToString()
                    };
                    tierInfoCSVList.Add(rec);

                    rec = new CSVLayoutTierInfo()
                    {
                        TierName = tier.Name,
                        Descriptor = "Propagators",
                    };
                    tierInfoCSVList.Add(rec);

                    IReadOnlyList<Propagator> propagatorList = tier.GetTraceConfiguration().Propagators;
                    foreach (Propagator propagator in propagatorList)
                    {
                        //Propagator examples
                        //1.  Phases Current[phasessub] BitwiseAndIncludesAny ABCN phasesenergized
                        //2.  Phases Current BitwiseAndIncludesAny ABCN 
                        //3.  nomvoltage MaxLessThanEqual 250000 curvoltage

                        string propagatorValue = propagator.Value.ToString();
                        CodedValueDomain cvd = propagator.NetworkAttribute.Domain as CodedValueDomain;
                        if (cvd != null)
                            propagatorValue = Common.GetCodedValueDomainValue(cvd, propagatorValue);

                        string substitutionAttribute = " ";
                        if (propagator.SubstitutionAttribute != null)
                            substitutionAttribute = "[" + propagator.SubstitutionAttribute.Name + "] ";

                        rec = new CSVLayoutTierInfo()
                        {
                            TierName = tier.Name,
                            Value = Common.EncloseStringInDoubleQuotes(propagator.NetworkAttribute.Name + substitutionAttribute + propagator.PropagatorFunction + propagator.Operator + " " + propagatorValue + " " + propagator.PersistedField?.Name)
                        };
                        tierInfoCSVList.Add(rec);
                    }

                    rec = new CSVLayoutTierInfo()
                    {
                        TierName = tier.Name,
                        Descriptor = "Summaries",
                    };
                    tierInfoCSVList.Add(rec);

                    for (int i = 0; i < tier.GetTraceConfiguration().Functions.Count; i++)
                    {
                        rec = new CSVLayoutTierInfo()
                        {
                            TierName = tier.Name,
                            Value = tier.GetTraceConfiguration().Functions[i].ToString() + " " + tier.GetTraceConfiguration().Functions[i].Condition?.ToString() + " " + tier.GetTraceConfiguration().Functions[i].PersistedField?.Name
                        };
                        tierInfoCSVList.Add(rec);
                    }
                    tierInfoCSVList.Add(emptyTierRec);
                    //End Trace Configuration

                    //Subnet Controllers
                    IReadOnlyList<AssetType> controllersList = tier.ValidSubnetworkControllers;
                    rec = new CSVLayoutTierInfo()
                    {
                        TierName = tier.Name,
                        Property = "Valid Subnetwork Controllers - " + controllersList.Count,
                        Descriptor = "Asset Group",
                        Value = "Asset Type"
                    };
                    tierInfoCSVList.Add(rec);

                    foreach (AssetType controller in controllersList)
                    {
                        rec = new CSVLayoutTierInfo()
                        {
                            TierName = tier.Name,
                            Descriptor = controller.AssetGroup.Name,
                            Value = controller.Name
                        };
                        tierInfoCSVList.Add(rec);
                    }
                    tierInfoCSVList.Add(emptyTierRec);

                    //Devices
                    IReadOnlyList<AssetType> devicesList = tier.ValidDevices;
                    rec = new CSVLayoutTierInfo()
                    {
                        TierName = tier.Name,
                        Property = "Valid Devices - " + devicesList.Count,
                        Descriptor = "Asset Group",
                        Value = "Asset Type"
                    };
                    tierInfoCSVList.Add(rec);

                    foreach (AssetType device in devicesList)
                    {
                        rec = new CSVLayoutTierInfo()
                        {
                            TierName = tier.Name,
                            Descriptor = device.AssetGroup.Name,
                            Value = device.Name
                        };
                        tierInfoCSVList.Add(rec);
                    }
                    tierInfoCSVList.Add(emptyTierRec);

                    //Lines
                    IReadOnlyList<AssetType> lineList = tier.ValidLines;
                    rec = new CSVLayoutTierInfo()
                    {
                        TierName = tier.Name,
                        Property = "Valid Lines - " + lineList.Count,
                        Descriptor = "Asset Group",
                        Value = "Asset Type"
                    };
                    tierInfoCSVList.Add(rec);

                    foreach (AssetType line in lineList)
                    {
                        rec = new CSVLayoutTierInfo()
                        {
                            TierName = tier.Name,
                            Descriptor = line.AssetGroup.Name,
                            Value = line.Name
                        };
                        tierInfoCSVList.Add(rec);
                    }
                    tierInfoCSVList.Add(emptyTierRec);

                    //Subnet Line
                    IReadOnlyList<AssetType> subnetlineList = tier.ValidSubnetworkLines;
                    rec = new CSVLayoutTierInfo()
                    {
                        TierName = tier.Name,
                        Property = "Aggregrated Lines for SubnetLine Feature - " + subnetlineList.Count,
                        Descriptor = "Asset Group",
                        Value = "Asset Type"
                    };
                    tierInfoCSVList.Add(rec);

                    foreach (AssetType subnetline in subnetlineList)
                    {
                        rec = new CSVLayoutTierInfo()
                        {
                            TierName = tier.Name,
                            Descriptor = subnetline.AssetGroup.Name,
                            Value = subnetline.Name
                        };
                        tierInfoCSVList.Add(rec);
                    }
                    tierInfoCSVList.Add(emptyTierRec);

                    //Junctions
                    IReadOnlyList<AssetType> junctionsList = tier.ValidJunctions;
                    rec = new CSVLayoutTierInfo()
                    {
                        TierName = tier.Name,
                        Property = "Valid Junctions - " + junctionsList.Count,
                        Descriptor = "Asset Group",
                        Value = "Asset Type"
                    };
                    tierInfoCSVList.Add(rec);

                    foreach (AssetType junction in junctionsList)
                    {
                        rec = new CSVLayoutTierInfo()
                        {
                            TierName = tier.Name,
                            Descriptor = junction.AssetGroup.Name,
                            Value = junction.Name
                        };
                        tierInfoCSVList.Add(rec);
                    }
                    tierInfoCSVList.Add(emptyTierRec);

                    if (Convert.ToInt32(utilityNetworkDataSourceInMap.SchemaVersion) >= 4)
                    {
                        //Junction Objects
                        IReadOnlyList<AssetType> junctionObjectList = tier.ValidJunctionObjects;
                        rec = new CSVLayoutTierInfo()
                        {
                            TierName = tier.Name,
                            Property = "Valid Junction Object - " + junctionObjectList.Count,
                            Descriptor = "Asset Group",
                            Value = "Asset Type"
                        };
                        tierInfoCSVList.Add(rec);

                        foreach (AssetType junctionObject in junctionObjectList)
                        {
                            rec = new CSVLayoutTierInfo()
                            {
                                TierName = tier.Name,
                                Descriptor = junctionObject.AssetGroup.Name,
                                Value = junctionObject.Name
                            };
                            tierInfoCSVList.Add(rec);
                        }
                        tierInfoCSVList.Add(emptyTierRec);

                        //Edge Objects
                        IReadOnlyList<AssetType> edgeObjectList = tier.ValidEdgeObjects;
                        rec = new CSVLayoutTierInfo()
                        {
                            TierName = tier.Name,
                            Property = "Valid Edge Object - " + edgeObjectList.Count,
                            Descriptor = "Asset Group",
                            Value = "Asset Type"
                        };
                        tierInfoCSVList.Add(rec);

                        foreach (AssetType edgeObject in edgeObjectList)
                        {
                            rec = new CSVLayoutTierInfo()
                            {
                                TierName = tier.Name,
                                Descriptor = edgeObject.AssetGroup.Name,
                                Value = edgeObject.Name
                            };
                            tierInfoCSVList.Add(rec);
                        }
                        tierInfoCSVList.Add(emptyTierRec);
                    }

                    //Diagrams
                    IReadOnlyList<string> diagramTemplatesList = tier.GetDiagramTemplateNames();
                    rec = new CSVLayoutTierInfo()
                    {
                        TierName = tier.Name,
                        Property = "Subnetwork Diagram Templates - " + diagramTemplatesList.Count
                    };
                    tierInfoCSVList.Add(rec);

                    foreach (string diagram in diagramTemplatesList)
                    {
                        rec = new CSVLayoutTierInfo()
                        {
                            TierName = tier.Name,
                            Descriptor = diagram
                        };
                        tierInfoCSVList.Add(rec);
                    }
                    tierInfoCSVList.Add(emptyTierRec);
                }
            }
        }

        private class CSVLayoutNetworkTopology
        {
            public string NetworkTopology { get; set; }
            public string Property { get; set; }
            public string Value { get; set; }
        }

        private class CSVLayoutDomainNetworks
        {
            public string DomainNetworkID { get; set; }
            public string DomainName { get; set; }
            public string Alias { get; set; }
            public string TierDefinition { get; set; }
            public string SubnetworkControllerType { get; set; }
            public string TierRank { get; set; }
            public string TierName { get; set; }
            public string TierGroup { get; set; }
            public string SubnetworkFieldName { get; set; }
            public string TopologyType { get; set; }
            public string SupportDisjointSubnetworks { get; set; }
            public string UpdateSubnetworkContainers { get; set; }
            public string UpdateSubnetworkStructures { get; set; }
            public string EditModeInDefault { get; set; }
            public string EditModeInNamedVersion { get; set; }
        }

        private class CSVLayoutTierInfo
        {
            public string TierRank { get; set; }
            public string TierName { get; set; }
            public string Property { get; set; }
            public string Descriptor { get; set; }
            public string Value { get; set; }
        }
    }
}