using ArcGIS.Core.Data;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UtilityNetworkPropertiesExtractor
{
    internal class GeoprocessingPrepHelper
    {
        public static Dictionary<string, TableAndDataSource> BuildDictionaryOfDistinctObjectsInMap()
        {
            Dictionary<string, TableAndDataSource> tablesDict = new Dictionary<string, TableAndDataSource>();

            //If Subtype Group layers are in the map, will have multiple layers pointing to same source featureclass
            //Populate Dictionary of distinct featureclasses
            IReadOnlyList<FeatureLayer> featureLayerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
            foreach (FeatureLayer featureLayer in featureLayerList)
            {
                Table table = featureLayer.GetTable();
                if (!tablesDict.ContainsKey(table.GetName()))
                {
                    DataSourceInMap dataSourcesInMap = DataSourcesInMapHelper.GetDataSourceOfLayerForDatabaseGPToolUsage(featureLayer);
                    if (dataSourcesInMap != null)
                    {
                        TableAndDataSource tableAndDataSource = new TableAndDataSource()
                        {
                            DataSourceName = dataSourcesInMap.Name,
                            Table = table
                        };

                        tablesDict.Add(table.GetName(), tableAndDataSource);
                    }
                }
            }

            //Standalone Tables
            IReadOnlyList<StandaloneTable> standaloneTableList = MapView.Active.Map.StandaloneTables;
            foreach (StandaloneTable standaloneTable in standaloneTableList)
            {
                Table table = standaloneTable.GetTable();
                if (!tablesDict.ContainsKey(table.GetName()))
                {
                    DataSourceInMap dataSourcesInMap = DataSourcesInMapHelper.GetDataSourceOfLayerForDatabaseGPToolUsage(standaloneTable);
                    if (dataSourcesInMap != null)
                    {
                        TableAndDataSource tableAndDataSource = new TableAndDataSource()
                        {
                            DataSourceName = dataSourcesInMap.Name,
                            Table = table
                        };

                        tablesDict.Add(table.GetName(), tableAndDataSource);
                    }
                }
            }
        
            return tablesDict;
        }

        public static string BuildPathForObject(TableAndDataSource tableAndDataSource)
        {
            using (Datastore datastore = tableAndDataSource.Table.GetDatastore())
            {
                string pathToObject = string.Empty;

                if (datastore is UnknownDatastore)
                    return string.Empty;

                //Using the absolute path for local files results in escape characters for things like spaces, which the GP tool can't handle for input/output of local files.
                Uri uri = datastore.GetPath();
                string datastorePath = uri.LocalPath;

                FeatureClass featureclass = tableAndDataSource.Table as FeatureClass;
                FeatureDataset featureDataset = null;

                if (featureclass != null)
                    featureDataset = featureclass.GetFeatureDataset();

                if (featureDataset == null)
                {
                    //<path to connfile>.sde/meh.unadmin.featureclass
                    pathToObject = string.Format("{0}\\{1}", datastorePath, tableAndDataSource.Table.GetName());
                }
                else
                {
                    //<path to connfile>.sde/meh.unadmin.Electric\meh.unadmin.ElectricDevice
                    string featureDatasetName = featureclass.GetFeatureDataset().GetName();
                    pathToObject = string.Format("{0}\\{1}\\{2}", datastorePath, featureDatasetName, tableAndDataSource.Table.GetName());
                }

                pathToObject = pathToObject.Replace("\\", "/");
                return pathToObject;
            }
        }

        public static void DeleteEmptyFiles(string searchString)
        {
            //Loop through directories and delete "_ContingentValues" files that are empty
            string[] directories = Directory.GetDirectories(Common.ExtractFilePath);
            foreach (string directory in directories)
            {
                //Delete files that only have 1 line (header) which means 0 Contingent Values are assigned
                DirectoryInfo directoryInfo = new DirectoryInfo(directory);
                List<FileInfo> blankFiles = directoryInfo.GetFiles().Where(f => f.Extension == ".csv" && f.Name.Contains(searchString)).ToList();
                foreach (FileInfo bf in blankFiles)
                {
                    string[] lines = File.ReadAllLines(bf.FullName);
                    int cnt = lines.Count();

                    if (cnt == 1)
                        bf.Delete();
                }

                //Delete the .xml files that are genereated by the GP tool
                List<FileInfo> deleteableFiles = directoryInfo.GetFiles().Where(f => f.Extension == ".xml" && f.Name.Contains(searchString)).ToList();
                foreach (FileInfo file in deleteableFiles)
                    file.Delete();

                FileInfo[] schemaIniFile = directoryInfo.GetFiles("schema.ini");
                foreach (FileInfo schemaIni in schemaIniFile)
                    schemaIni.Delete();
            }
        }
    }

    public class TableAndDataSource
    {
        public Table Table { get; set; }
        public string DataSourceName { get; set; }
    }
}