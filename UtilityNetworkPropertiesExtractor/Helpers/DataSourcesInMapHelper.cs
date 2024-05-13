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
using ArcGIS.Desktop.Mapping;
using System.Collections.Generic;
using System.Linq;

namespace UtilityNetworkPropertiesExtractor
{
    internal class DataSourcesInMapHelper
    {
        public static List<DataSourceInMap> GetDataSourcesInMap()
        {
            List<DataSourceInMap> dataSourceInMapsList = new List<DataSourceInMap>();
            List<string> serviceNameList = new List<string>();

            List<FeatureLayer> featureLayers = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
            foreach (FeatureLayer featureLayer in featureLayers)
            {
                DataSourceInMap dataSourceInMap = GetDataSourceFromMapMember(featureLayer);
                if (!serviceNameList.Contains(dataSourceInMap.URI))
                {
                    serviceNameList.Add(dataSourceInMap.URI);
                    dataSourceInMap.Geodatabase = featureLayer?.GetTable().GetDatastore() as Geodatabase;
                    dataSourceInMapsList.Add(dataSourceInMap);
                }
            }

            List<StandaloneTable> standaloneTables = MapView.Active.Map.GetStandaloneTablesAsFlattenedList().ToList();
            foreach (StandaloneTable tb in standaloneTables)
            {
                DataSourceInMap dataSourceInMap = GetDataSourceFromMapMember(tb);
                if (!serviceNameList.Contains(dataSourceInMap.URI))
                {
                    serviceNameList.Add(dataSourceInMap.URI);
                    dataSourceInMap.Geodatabase = tb?.GetTable().GetDatastore() as Geodatabase;
                    dataSourceInMapsList.Add(dataSourceInMap);
                }
            }

            return dataSourceInMapsList;
        }

        public static List<UtilityNetworkDataSourceInMap> GetUtilityNetworkDataSourcesInMap()
        {
            List<UtilityNetworkDataSourceInMap> utilityNetworksInMapsList = new List<UtilityNetworkDataSourceInMap>();

            List<UtilityNetworkLayer> utillityNetworkLayerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<UtilityNetworkLayer>().ToList();
            foreach (UtilityNetworkLayer utilityNetworkLayer in utillityNetworkLayerList)
            {
                DataSourceInMap dataSourceInMap = GetDataSourceFromMapMember(utilityNetworkLayer);
                if (dataSourceInMap != null)
                {
                    CompositeLayer compositeLayer = utilityNetworkLayer;
                    FeatureLayer featureLayer = compositeLayer.Layers.First() as FeatureLayer;
                    UtilityNetwork un = utilityNetworkLayer.GetUtilityNetwork();

                    UtilityNetworkDataSourceInMap utilityNetworkInMap = new UtilityNetworkDataSourceInMap()
                    {
                        UtilityNetwork = un,
                        UtilityNetworkLayer = utilityNetworkLayer,
                        SchemaVersion = un.GetDefinition().GetSchemaVersion(),
                        WorkspaceFactory = dataSourceInMap.WorkspaceFactory,
                        URI = dataSourceInMap.URI,
                        Geodatabase = featureLayer?.GetTable().GetDatastore() as Geodatabase,
                        NameForCSV = dataSourceInMap.NameForCSV
                    };

                    utilityNetworksInMapsList.Add(utilityNetworkInMap);
                }
            }
            return utilityNetworksInMapsList;
        }

        public static DataSourceInMap GetDataSourceOfLayerForDatabaseGPToolUsage(MapMember mapMember)
        {
            DataSourceInMap dataSourceInMap = GetDataSourceFromMapMember(mapMember);

            //GP Tools like "Export Attribute Rules" and "Export Contingent Values" can only be run against a DB connection.
            if (dataSourceInMap.WorkspaceFactory == WorkspaceFactory.SDE.ToString() ||
                dataSourceInMap.WorkspaceFactory == WorkspaceFactory.SQLite.ToString() ||
                dataSourceInMap.WorkspaceFactory == WorkspaceFactory.FileGDB.ToString())
            {
                return dataSourceInMap;
            }
            else 
                return null; 
        }

        private static DataSourceInMap GetDataSourceFromMapMember(MapMember mapMember)
        {
            DataSourceInMap dataSourceInMap = new DataSourceInMap();
            Dictionary<string, string> connStringDict = new Dictionary<string, string>();

            // Build dictionary of values in the WorkspaceConnectionString entry from the mapMember's CIM
            CIMDataConnection dataConnection = mapMember.GetDataConnection();
            if (dataConnection is CIMStandardDataConnection stDataConn) // Represents a standard data connection, the most common data connection type.
            {
                //build dictionary of workspace connection information
                dataSourceInMap.WorkspaceFactory = stDataConn.WorkspaceFactory.ToString();
                string[] pairs = stDataConn.WorkspaceConnectionString.Split(";");
                foreach (var p in pairs)
                {
                    string[] kvp = p.Split("=");
                    connStringDict.Add(kvp[0], kvp[1]);
                }

                if (stDataConn.WorkspaceFactory == WorkspaceFactory.FeatureService)
                {
                    GetFeatureServiceInfo(connStringDict, ref dataSourceInMap);
                }
                else if (stDataConn.WorkspaceFactory == WorkspaceFactory.SDE)
                {
                    GetSdeInfo(connStringDict, ref dataSourceInMap);
                }
                else if (stDataConn.WorkspaceFactory == WorkspaceFactory.FileGDB)
                {
                    GetFileGdbInfo(connStringDict, ref dataSourceInMap);
                }
                else if (stDataConn.WorkspaceFactory == WorkspaceFactory.Shapefile)
                {
                    GetShapefileInfo(connStringDict, ref dataSourceInMap);
                }
            }
            else if (dataConnection is CIMFeatureDatasetDataConnection fdDataConn) // Represents a feature dataset data connection.
            {
                //build dictionary of workspace connection information
                dataSourceInMap.WorkspaceFactory = fdDataConn.WorkspaceFactory.ToString();
                string[] pairs = fdDataConn.WorkspaceConnectionString.Split(";");
                foreach (var p in pairs)
                {
                    var kvp = p.Split("=");
                    connStringDict.Add(kvp[0], kvp[1]);
                }

                if (fdDataConn.WorkspaceFactory == WorkspaceFactory.SQLite)
                {
                    GetSqlLiteInfo(connStringDict, ref dataSourceInMap);
                }

                else if (fdDataConn.WorkspaceFactory == WorkspaceFactory.SDE)
                {
                    GetSdeInfo(connStringDict, ref dataSourceInMap);
                }
                else if (fdDataConn.WorkspaceFactory == WorkspaceFactory.FileGDB)
                {
                    GetFileGdbInfo(connStringDict, ref dataSourceInMap);
                }
            }
            return dataSourceInMap;
        }

        private static void GetShapefileInfo(Dictionary<string, string> connStringDict, ref DataSourceInMap dataSourceInMap)
        {
            //<WorkspaceConnectionString>DATABASE=C:\Pro Project_8485e4\Folder Name</WorkspaceConnectionString>
            dataSourceInMap.URI = connStringDict["DATABASE"];
            int pos = dataSourceInMap.URI.LastIndexOf("\\");
            dataSourceInMap.NameForCSV = dataSourceInMap.URI.Substring(pos + 1);
        }
        
        private static void GetSqlLiteInfo(Dictionary<string, string> connStringDict, ref DataSourceInMap dataSourceInMap)
        {
            //< WorkspaceConnectionString >AUTHENTICATION_MODE=OSA;DATABASE=main;DB_CONNECTION_PROPERTIES=C:\EsriData\Mobile GDB\Electric UN.geodatabase;INSTANCE = sde:sqlite: C:\EsriData\Mobile GDB\Electric UN.geodatabase; IS_GEODATABASE = true; IS_NOSERVER = 0; SERVER = C:</ WorkspaceConnectionString >
            dataSourceInMap.URI = connStringDict["DB_CONNECTION_PROPERTIES"];

            //only want fGDB name with no extension
            string noExtension = dataSourceInMap.URI.Split(".")[0];
            int pos = noExtension.LastIndexOf("\\");
            dataSourceInMap.NameForCSV = noExtension.Substring(pos + 1);
        }

        private static void GetFeatureServiceInfo(Dictionary<string, string> connStringDict, ref DataSourceInMap dataSourceInMap)
        {
            //<WorkspaceConnectionString> URL=https://webAdaptor/server/rest/services/ElectricUN/FeatureServer;VERSION=sde.default;... </WorkspaceConnectionString>
            
            string url = connStringDict["URL"];
            int pos = url.IndexOf(";");
            if (pos > 0)  // if the URL contains VERSION details, strip that off.
                url = url.Substring(0, pos);

            //Service Name
            string serviceName = string.Empty;

            int fsPos = url.LastIndexOf("/FeatureServer");
            string tempURL = url.Substring(0, fsPos);


            int lastSlashPos = tempURL.LastIndexOf("/");
            if (lastSlashPos > 0)
                serviceName = tempURL.Substring(lastSlashPos + 1);

            dataSourceInMap.URI = url;
            dataSourceInMap.NameForCSV = serviceName;
        }

        private static void GetFileGdbInfo(Dictionary<string, string> connStringDict, ref DataSourceInMap dataSourceInMap)
        {
            //<WorkspaceConnectionString>DATABASE=C:\Pro Project_8485e4\electricutilitynetworkfoundation.gdb</WorkspaceConnectionString>
            dataSourceInMap.URI = connStringDict["DATABASE"];

            //only want fGDB name with no extension
            int extensionPos = dataSourceInMap.URI.LastIndexOf(".");
            string tempURI = dataSourceInMap.URI.Substring(0, extensionPos);
            int pos = tempURI.LastIndexOf("\\");
            dataSourceInMap.NameForCSV = tempURI.Substring(pos + 1);
        }

        private static void GetSdeInfo(Dictionary<string, string> connStringDict, ref DataSourceInMap dataSourceInMap)
        {
            //<WorkspaceConnectionString> ENCRYPTED_PASSWORD_UTF8 = XXX; ENCRYPTED_PASSWORD = XXX; SERVER = localhost; INSTANCE = sde:postgresql: localhost; DBCLIENT = postgresql; DB_CONNECTION_PROPERTIES = localhost; DATABASE = naperville_electric; USER = gis; AUTHENTICATION_MODE = DBMS; BRANCH = sde.DEFAULT </ WorkspaceConnectionString >
            //< WorkspaceConnectionString > ENCRYPTED_PASSWORD_UTF8 = XXX; ENCRYPTED_PASSWORD = XXX; SERVER=serverName; INSTANCE=sde:sqlserver: serverName; DBCLIENT = sqlserver; DB_CONNECTION_PROPERTIES = server.com; DATABASE = naperville_gas; USER = gas; AUTHENTICATION_MODE = DBMS; BRANCH = sde.DEFAULT </ WorkspaceConnectionString >
            string dbConnProperties = connStringDict["DB_CONNECTION_PROPERTIES"];
            string databaseName = connStringDict["DATABASE"];
            dataSourceInMap.URI = $"{dbConnProperties}.{databaseName}";
            dataSourceInMap.NameForCSV = databaseName;
            return;
        }
    }

    public class DataSourceInMap
    {
        public string WorkspaceFactory { get; set; }
        public string URI { get; set; }
        public string NameForCSV { get; set; }
        public Geodatabase Geodatabase { get; set; }
    }
    public class UtilityNetworkDataSourceInMap : DataSourceInMap
    {
        public UtilityNetwork UtilityNetwork { get; set; }
        public UtilityNetworkLayer UtilityNetworkLayer { get; set; }
        public string SchemaVersion { get; set; }
    }
}
