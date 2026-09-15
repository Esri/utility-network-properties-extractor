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
using ArcGIS.Core.Data.UtilityNetwork.Telecom;
using ArcGIS.Core.Data.UtilityNetwork.Trace;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using static System.Data.Odbc.ODBC32;
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

                            //Telecom Domain Network section
                            if (utilityNetworkDataSourceInMap.UtilityNetwork.HasTelecomNetwork) {

                                IReadOnlyList<DomainNetwork> domainNetworks = utilityNetworkDataSourceInMap.UtilityNetwork.GetDefinition().GetDomainNetworks();
                                foreach (DomainNetwork domainNetwork in domainNetworks)
                                {
                                    if (domainNetwork is TelecomDomainNetwork tdn)
                                    {
                                        //Color Sets
                                        CSVColorSets emptyColorSetsRec = new CSVColorSets();
                                        properties = Common.GetPropertiesOfClass(emptyColorSetsRec);
                                        columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                                        sw.WriteLine(columnHeader);

                                        List<CSVColorSets> csvColorSets = new List<CSVColorSets>();
                                        ColorSets(tdn, ref csvColorSets);
                                        foreach (CSVColorSets row in csvColorSets)
                                        {
                                            output = Common.ExtractClassValuesToString(row, properties);
                                            sw.WriteLine(output);
                                        }

                                        //Color Schemes
                                        CSVColorSchemes emptyColorSchemesRec = new CSVColorSchemes();
                                        properties = Common.GetPropertiesOfClass(emptyColorSchemesRec);
                                        columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                                        sw.WriteLine(columnHeader);

                                        List<CSVColorSchemes> csvColorSchemes = new List<CSVColorSchemes>();
                                        ColorSchemes(tdn, ref csvColorSchemes);
                                        foreach (CSVColorSchemes row in csvColorSchemes)
                                        {
                                            output = Common.ExtractClassValuesToString(row, properties);
                                            sw.WriteLine(output);
                                        }

                                        //Circuit Properties
                                        CSVCircuitProperties emptyCircuitPropRec = new CSVCircuitProperties();
                                        properties = Common.GetPropertiesOfClass(emptyCircuitPropRec);
                                        columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                                        sw.WriteLine(columnHeader);

                                        List<CSVCircuitProperties> csvCircuitProps = new List<CSVCircuitProperties>();
                                        CircuitProperties(tdn, ref csvCircuitProps);
                                        foreach (CSVCircuitProperties row in csvCircuitProps)
                                        {
                                            output = Common.ExtractClassValuesToString(row, properties);
                                            sw.WriteLine(output);
                                        }


                                        //Divide Policy
                                        CSVDividePolicy emptyDivideRec = new CSVDividePolicy();
                                        properties = Common.GetPropertiesOfClass(emptyDivideRec);
                                        columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                                        sw.WriteLine(columnHeader);

                                        List<CSVDividePolicy> csvDividePolicy = new List<CSVDividePolicy>();
                                        DividePolicy(tdn, ref csvDividePolicy);
                                        foreach (CSVDividePolicy row in csvDividePolicy)
                                        {
                                            output = Common.ExtractClassValuesToString(row, properties);
                                            sw.WriteLine(output);
                                        }

                                        //Combine Policy
                                        CSVCombinePolicy emptyCombineRec = new CSVCombinePolicy();
                                        properties = Common.GetPropertiesOfClass(emptyCombineRec);
                                        columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                                        sw.WriteLine(columnHeader);

                                        List<CSVCombinePolicy> csvCombinePolicy = new List<CSVCombinePolicy>();
                                        CombinePolicy(tdn, ref csvCombinePolicy);
                                        foreach (CSVCombinePolicy row in csvCombinePolicy)
                                        {
                                            output = Common.ExtractClassValuesToString(row, properties);
                                            sw.WriteLine(output);
                                        }

                                        //Wavelengths
                                        CSVWavelength emptyWavelengthRec = new CSVWavelength();
                                        properties = Common.GetPropertiesOfClass(emptyWavelengthRec);
                                        columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                                        sw.WriteLine(columnHeader);

                                        List<CSVWavelength> csvWavelengths = new List<CSVWavelength>();
                                        Wavelengths(tdn, ref csvWavelengths);
                                        foreach (CSVWavelength row in csvWavelengths)
                                        {
                                            output = Common.ExtractClassValuesToString(row, properties);
                                            sw.WriteLine(output);
                                        }

                                    }
                                }
                            }

                            sw.Flush();
                            sw.Close();

                        }
                    }
                }
            });
        }

        private static void CircuitProperties(TelecomDomainNetwork tdn, ref List<CSVCircuitProperties> csvCircuitProps)
        {
            // Circuit Properties
            CircuitProperties circuitProperties = tdn.CircuitProperties;

            CSVCircuitProperties rec = new CSVCircuitProperties() { Property = "Share Circuit Locations", Value = circuitProperties.ShareCircuitLocations.ToString() };
            csvCircuitProps.Add(rec);

            rec = new CSVCircuitProperties() { Property = "Import Circuits As Clean", Value = circuitProperties.ImportCircuitAsClean.ToString() };
            csvCircuitProps.Add(rec);
           
            // Trace Configurations
            TraceConfiguration traceConfiguration = tdn.TraceConfiguration;

            rec = new CSVCircuitProperties() { Property = "Trace Configuration", Descriptor = "Num Paths", Value = circuitProperties.NumPaths.ToString() };
            csvCircuitProps.Add(rec);

            rec = new CSVCircuitProperties() { Property = "Trace Configuration", Descriptor = "Max Hops", Value = circuitProperties.MaxHops.ToString() };
            csvCircuitProps.Add(rec);

            rec = new CSVCircuitProperties() { Property = "Trace Configuration", Descriptor = "Include Containers", Value = traceConfiguration.IncludeContainers.ToString() };
            csvCircuitProps.Add(rec);

            rec = new CSVCircuitProperties() { Property = "Trace Configuration", Descriptor = "Include Content", Value = traceConfiguration.IncludeContent.ToString() };
            csvCircuitProps.Add(rec);

            rec = new CSVCircuitProperties() { Property = "Trace Configuration", Descriptor = "Include Structures", Value = traceConfiguration.IncludeStructures.ToString() };
            csvCircuitProps.Add(rec);

            rec = new CSVCircuitProperties() { Property = "Trace Configuration", Descriptor = "Include Barrier Features", Value = traceConfiguration.IncludeBarriersWithResults.ToString() };
            csvCircuitProps.Add(rec);

            rec = new CSVCircuitProperties() { Property = "Trace Configuration", Descriptor = "Validate Locatability", Value = traceConfiguration.ValidateLocatability.ToString() };
            csvCircuitProps.Add(rec);

            rec = new CSVCircuitProperties() { Property = "Trace Configuration", Descriptor = "Apply Traversability", Value = traceConfiguration.Traversability.Scope.ToString() };
            csvCircuitProps.Add(rec);

            // Function Barriers
            IReadOnlyList<FunctionBarrier> functionBarriers = traceConfiguration.Traversability.FunctionBarriers;
            foreach (FunctionBarrier functionBarrier in functionBarriers)
            {
                rec = new CSVCircuitProperties() { Property = "Trace Configuration", Descriptor = "Function Barrier", Value = functionBarrier.ToString() };
                csvCircuitProps.Add(rec);
            }

            // Propagators
            IReadOnlyList<Propagator> propagatorList = traceConfiguration.Propagators;
            foreach (Propagator propagator in propagatorList)
            {
                rec = new CSVCircuitProperties() { Property = "Trace Configuration", Descriptor = "Propagator", Value = propagator.ToString() };
                csvCircuitProps.Add(rec);   
            }

            CSVCircuitProperties emptyRec = new CSVCircuitProperties();
            csvCircuitProps.Add(emptyRec);
        }

        private static void ColorSets(TelecomDomainNetwork tdn, ref List<CSVColorSets> csvColorSets)
        {
            IReadOnlyList<ColorSet> colorSets = tdn.ColorSets;
            foreach (ColorSet colorSet in colorSets)
            {
                foreach (ColorCode colorCode in colorSet.ColorCodes)
                {
                    CSVColorSets rec = new CSVColorSets()
                    {
                        ColorSetName = colorSet.Name,
                        ColorCodeId = colorCode.ID.ToString(),
                        ColorCodeName = colorCode.Name,
                        ColorCodeLabel = colorCode.Label,
                    };

                    if (colorCode.HexCodes != null)
                        rec.HexCode = Common.EncloseStringInDoubleQuotes(string.Join(", ", colorCode.HexCodes));

                    csvColorSets.Add(rec);
                }   
            }

            CSVColorSets emptyRec = new CSVColorSets();
            csvColorSets.Add(emptyRec);
        }

        private static void ColorSchemes(TelecomDomainNetwork tdn, ref List<CSVColorSchemes> csvColorSchemes)
        {
            IReadOnlyList<ColorScheme> colorSchemes = tdn.ColorSchemes;
            foreach (ColorScheme colorScheme in colorSchemes)
            {
                int i = 0;
                IReadOnlyList<ColorSchemeGroup> colorSchemeGroups = colorScheme.Groups;
                foreach (ColorSchemeGroup group in colorSchemeGroups)
                {
                    i += 1;

                    CSVColorSchemes rec = new CSVColorSchemes();

                    if (i == 1) // This aligns with how the Pro UI displays this information.
                    {
                        rec.ColorSchemeName = colorScheme.Name;
                        rec.ColorSchemeId = colorScheme.ID.ToString();
                        rec.Levels = colorSchemeGroups.Count.ToString();
                        rec.GroupDelimeter = colorScheme.GroupDelimiter;
                    }

                    IReadOnlyList<ColorCode> colorCodes = group.ColorCodes;
                    rec.Labels = Common.EncloseStringInDoubleQuotes(string.Join(", ", colorCodes.Select(c => c.Label)));
                    rec.GroupName = group.Name;
                    rec.GroupLevels = group.Level.ToString();
                    rec.Capacity = Common.EncloseStringInDoubleQuotes(string.Join(",", group.Capacity));
                    rec.Delimeter = group.Delimiter;

                    csvColorSchemes.Add(rec);
                }
            }

            CSVColorSchemes emptyRec = new CSVColorSchemes();
            csvColorSchemes.Add(emptyRec);
        }

        private static void CombinePolicy(TelecomDomainNetwork tdn, ref List<CSVCombinePolicy> csvCombinePolicy)
        {
            IReadOnlyList<CombinePolicy> combinePolicies = tdn.CombinePolicies;
            foreach (CombinePolicy policy in combinePolicies)
            {
                CSVCombinePolicy rec = new CSVCombinePolicy()
                {
                    ClassName = policy.NetworkSource.Name,
                    FieldName = policy.FieldName,
                    Policy = policy.Policy.ToString()
                };

                csvCombinePolicy.Add(rec);
            }

            CSVCombinePolicy emptyRec = new CSVCombinePolicy();
            csvCombinePolicy.Add(emptyRec);
        }

        private static void DividePolicy(TelecomDomainNetwork tdn, ref List<CSVDividePolicy> csvDividePolicy)
        {
            IReadOnlyList<DividePolicy> dividePolicies = tdn.DividePolicies;
            foreach (DividePolicy policy in dividePolicies)
            {
                CSVDividePolicy rec = new CSVDividePolicy()
                {
                    ClassName = policy.NetworkSource.Name,
                    FieldName = policy.FieldName,
                    Policy = policy.Policy.ToString()                   
                };

                csvDividePolicy.Add(rec);
            }

            CSVDividePolicy emptyRec = new CSVDividePolicy();
            csvDividePolicy.Add(emptyRec);
        }

        private static void Wavelengths(TelecomDomainNetwork tdn, ref List<CSVWavelength> csvWavelengths)
        {
            IReadOnlyList<WavelengthScheme> wavelengthSchemes= tdn.WavelengthSchemes;
            foreach (WavelengthScheme scheme in wavelengthSchemes)
            {
                foreach (Wavelength wavelength in scheme.Wavelengths)
                {

                    CSVWavelength rec = new CSVWavelength()
                    {
                        SchemeName = scheme.Name,
                        ID = wavelength.ID.ToString(),
                        Name = wavelength.Name,
                        Length = Convert.ToString(wavelength.Length)
                    };

                    csvWavelengths.Add(rec);
                }
            }

            CSVWavelength emptyRec = new CSVWavelength();
            csvWavelengths.Add(emptyRec);
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
                };

                if (domainNetwork is TelecomDomainNetwork tdn)
                    networkRec.DomainNetworkType = "Telecom";

                else
                {
                    networkRec.DomainNetworkType = "Traditional";
                    networkRec.TierDefinition = domainNetwork.TierDefinition.ToString();
                    networkRec.SubnetworkControllerType = domainNetwork.SubnetworkControllerType.ToString();
                }

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

            // Add 2 empty records to make reading the CSV layout easier
            CSVLayoutDomainNetworks rec = new CSVLayoutDomainNetworks();
            myDomainNetworksCSVList.Add(rec);
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

                    IReadOnlyList<Propagator> propagatorList = tier.GetTraceConfiguration().Propagators;
                    foreach (Propagator propagator in propagatorList)
                    {
                        //Propagator examples
                        //  E:Phases Propagated[E:Phases Substitution] BitwiseAndIncludesAny I,II,III or ABC or abc phasesenergized

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
                            Descriptor = "Propagators",
                            Value = Common.EncloseStringInDoubleQuotes(propagator.NetworkAttribute.Name + substitutionAttribute + propagator.PropagatorFunction + propagator.Operator + " " + propagatorValue + " " + propagator.PersistedField?.Name)
                        };
                        tierInfoCSVList.Add(rec);
                    }

                    for (int i = 0; i < tier.GetTraceConfiguration().Functions.Count; i++)
                    {
                        rec = new CSVLayoutTierInfo()
                        {
                            TierName = tier.Name,
                            Descriptor = "Summaries",
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

            CSVLayoutTierInfo emptyRec = new CSVLayoutTierInfo();
            tierInfoCSVList.Add(emptyRec);
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
            public string DomainNetworkType { get; set; }
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

        private class CSVCircuitProperties
        {
            public string CircuitProperties { get; set; }
            public string Property { get; set; }
            public string Descriptor { get; set; }
            public string Value { get; set; }

        }


        private class CSVColorSets
        {
            public string ColorSets { get; set; }
            public string ColorSetName { get; set; }
            public string ColorCodeId { get; set; }
            public string ColorCodeName { get; set; }
            public string ColorCodeLabel { get; set; }
            public string HexCode { get; set; }
        }

        private class CSVColorSchemes
        {
            public string ColorSchemes { get; set; }
            public string ColorSchemeName { get; set; }
            public string ColorSchemeId { get; set; }
            public string GroupDelimeter { get; set; }
            public string Levels { get; set; }
            public string GroupLevels { get; set; }
            public string GroupName { get; set; }
            public string Labels { get; set; }
            public string Capacity { get; set; }
            public string Delimeter { get; set; }

        }

        private class CSVDividePolicy
        {
            public string TelecomObjectDividePolicy { get; set; }
            public string ClassName { get; set; }
            public string FieldName { get; set; }
            public string Policy { get; set; }
        }

        private class CSVCombinePolicy
        {
            public string TelecomObjectCombinePolicy { get; set; }
            public string ClassName { get; set; }
            public string FieldName { get; set; }
            public string Policy { get; set; }
        }

        private class CSVWavelength
        {
            public string WavelengthSchemes { get; set; }
            public string SchemeName { get; set; }
            public string ID { get; set; }
            public string Name { get; set; }
            public string Length { get; set; }
        }
    }
}