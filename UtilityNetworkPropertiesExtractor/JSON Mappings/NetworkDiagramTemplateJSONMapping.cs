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
    public class NetworkDiagramTemplateJSONMapping
    {
        public Diagramtemplateinfo[] diagramTemplateInfos { get; set; }
    }

    public class Diagramtemplateinfo
    {
        public string name { get; set; }
        public string description { get; set; }
        public long creationDate { get; set; }
        public long lastUpdateDate { get; set; }
        public bool usedByATier { get; set; }
        public bool enableDiagramExtend { get; set; }
        public bool enableDiagramStorage { get; set; }
    }

}