using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UtilityNetworkPropertiesExtractor
{
    internal class DataSourcesInMapHelper
    {
        public static List<DataSourceInMap> GetDataSourcesInMap()
        {
            List<DataSourceInMap> dataSourceInMapsList = new List<DataSourceInMap>();
            List<string> serviceNameList = new List<string>();

            var mesg = string.Empty;

            List<FeatureLayer> featureLayers = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
            foreach (FeatureLayer featureLayer in featureLayers)
            {
                DataSourceInMap dataSourceInMap = GetDataSourcesFromMapMember(featureLayer);
                if (!serviceNameList.Contains(dataSourceInMap.URI))
                {
                    serviceNameList.Add(dataSourceInMap.URI);
                    dataSourceInMap.Geodatabase = featureLayer?.GetTable().GetDatastore() as Geodatabase;
                    //mesg += dataSourceInMap.NameForCSV + "\n\n";

                    dataSourceInMapsList.Add(dataSourceInMap);
                }
            }

            List<StandaloneTable> standaloneTables = MapView.Active.Map.GetStandaloneTablesAsFlattenedList().ToList();
            foreach (StandaloneTable tb in standaloneTables)
            {
                DataSourceInMap dataSourceInMap = GetDataSourcesFromMapMember(tb);
                if (!serviceNameList.Contains(dataSourceInMap.URI))
                {
                    serviceNameList.Add(dataSourceInMap.URI);
                    dataSourceInMap.Geodatabase = tb?.GetTable().GetDatastore() as Geodatabase;
                    //mesg += dataSourceInMap.NameForCSV + "\n\n";
                    dataSourceInMapsList.Add(dataSourceInMap);
                }
            }

            return dataSourceInMapsList;
        }

        private static DataSourceInMap GetDataSourcesFromMapMember(MapMember mapMember)
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
            string serviceName = string.Empty;

            string url = connStringDict["URL"];
            int pos = url.IndexOf("/FeatureServer");
            if (pos > 0)
                url = url.Substring(0, pos);  // Removes /Featureserver and everything to the right of it

            //Service Name
            int lastSlashPos = url.LastIndexOf("/");
            if (lastSlashPos > 0)
                serviceName = url.Substring(lastSlashPos + 1);

            dataSourceInMap.URI = url;
            dataSourceInMap.NameForCSV = serviceName;
        }

        private static void GetFileGdbInfo(Dictionary<string, string> connStringDict, ref DataSourceInMap dataSourceInMap)
        {
            //<WorkspaceConnectionString>DATABASE=C:\Pro Project_8485e4\electricutilitynetworkfoundation.gdb</WorkspaceConnectionString>
            dataSourceInMap.URI = connStringDict["DATABASE"];

            //only want fGDB name with no extension
            string noExtension = dataSourceInMap.URI.Split(".")[0];
            int pos = noExtension.LastIndexOf("\\");
            dataSourceInMap.NameForCSV = noExtension.Substring(pos + 1);
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
}
