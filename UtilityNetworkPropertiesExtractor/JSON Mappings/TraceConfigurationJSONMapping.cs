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
namespace UtilityNetworkPropertiesExtractor.JSONMappings
{
    public class TraceConfigurationJSONMapping
    {
        public Traceconfiguration[] traceConfigurations { get; set; }
        public bool success { get; set; }
    }

    public class Traceconfiguration
    {
        public string name { get; set; }
        public string description { get; set; }
        public string traceType { get; set; }
        public Traceconfiguration1 traceConfiguration { get; set; }
        public object[] resultTypes { get; set; }
        public string minNumStartingPoints { get; set; }
        public long creationDate { get; set; }
        public string[] tags { get; set; }
        public string creator { get; set; }
        public string globalId { get; set; }
    }

    public class Traceconfiguration1
    {
        public bool includeContainers { get; set; }
        public bool includeContent { get; set; }
        public bool includeStructures { get; set; }
        public bool includeBarriers { get; set; }
        public bool validateConsistency { get; set; }
        public bool validateLocatability { get; set; }
        public bool includeIsolated { get; set; }
        public bool ignoreBarriersAtStartingPoints { get; set; }
        public bool includeUpToFirstSpatialContainer { get; set; }
        public bool allowIndeterminateFlow { get; set; }
        public string domainNetworkName { get; set; }
        public string tierName { get; set; }
        public string targetTierName { get; set; }
        public string subnetworkName { get; set; }
        public string diagramTemplateName { get; set; }
        public string shortestPathNetworkAttributeName { get; set; }
        public string filterBitsetNetworkAttributeName { get; set; }
        public string traversabilityScope { get; set; }
        public Conditionbarrier[] conditionBarriers { get; set; }
        public object[] functionBarriers { get; set; }
        public string arcadeExpressionBarrier { get; set; }
        public Filterbarrier[] filterBarriers { get; set; }
        public object[] filterFunctionBarriers { get; set; }
        public string filterScope { get; set; }
        public object[] functions { get; set; }
        public Nearestneighbor nearestNeighbor { get; set; }
        public Outputfilter[] outputFilters { get; set; }
        public Outputcondition[] outputConditions { get; set; }
        public object[] propagators { get; set; }
    }

    public class Nearestneighbor
    {
        public int count { get; set; }
        public string costNetworkAttributeName { get; set; }
        public object[] nearestCategories { get; set; }
        public object[] nearestAssets { get; set; }
    }

    public class Conditionbarrier
    {
        public string name { get; set; }
        public string type { get; set; }
        public string _operator { get; set; }
        public object value { get; set; }
        public bool combineUsingOr { get; set; }
        public bool isSpecificValue { get; set; }
    }

    public class Filterbarrier
    {
        public string name { get; set; }
        public string type { get; set; }
        public string _operator { get; set; }
        public string value { get; set; }
        public bool combineUsingOr { get; set; }
        public bool isSpecificValue { get; set; }
    }

    public class Outputfilter
    {
        public int networkSourceId { get; set; }
        public int assetGroupCode { get; set; }
        public int assetTypeCode { get; set; }
    }

    public class Outputcondition
    {
        public string name { get; set; }
        public string type { get; set; }
        public string _operator { get; set; }
        public string value { get; set; }
        public bool combineUsingOr { get; set; }
        public bool isSpecificValue { get; set; }
    }
} 