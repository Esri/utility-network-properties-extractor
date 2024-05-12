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
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Windows.Markup;

namespace UtilityNetworkPropertiesExtractor
{
    public static class Common
    {
        private const string Delimiter = ",";
        public static string ExtractFilePath;
        private const string ExtractFileRootPath = @"C:\temp\ProSdk_CSV\";

        //Two Field Setting buttons are writing/reading the same files.  Constants used to ensure that a code change to 1 button doesn't break the other
        public const string FieldSettingsClassNameHeader = "Class Name";      

        public static DateTime ConvertEpochTimeToReadableDate(long epoch)
        {
            DateTimeOffset dateTimeOffSet = DateTimeOffset.FromUnixTimeMilliseconds(epoch);
            return dateTimeOffSet.DateTime;
        }

        public static void CreateOutputDirectory()
        {
            ExtractFilePath = GetExtractFilePath();

            if (!Directory.Exists(ExtractFilePath))
                Directory.CreateDirectory(ExtractFilePath);
        }

        public static string DetermineTimeDifference(DateTime startTime, DateTime endTime)
        {
            TimeSpan traceTime = endTime.Subtract(startTime);
            return string.Format("{0}:{1}:{2}", traceTime.Hours, traceTime.Minutes, traceTime.Seconds);
        }

        public static string ExtractClassValuesToString<T>(T rec, PropertyInfo[] properties)
        {
            return properties.Select(n => n.GetValue(rec, null)).Select(n => n == null ? string.Empty : n.ToString()).Aggregate((a, b) => a + Delimiter + b);
        }

        public static string ExtractClassPropertyNamesToString(PropertyInfo[] properties)
        {
            return properties.Select(n => n.Name).Aggregate((a, b) => a + Delimiter + b);
        }

        public static string EncloseStringInDoubleQuotes(string value)
        {
            return "\"" + value + "\"";
        }

        public static string GetCodedValueDomainValue(CodedValueDomain cvd, string code)
        {
            string retVal = string.Empty;

            FieldType fieldType = cvd.GetFieldType();
            switch (fieldType)
            {
                case FieldType.SmallInteger:
                case FieldType.Integer:
                    retVal = cvd.GetName(Convert.ToInt32(code)).ToString();
                    break;
                case FieldType.Double:
                    retVal = cvd.GetName(Convert.ToDouble(code)).ToString();
                    break;
                case FieldType.Single:
                    retVal = cvd.GetName(Convert.ToSingle(code)).ToString();
                    break;
                case FieldType.String:
                    retVal = cvd.GetName(code).ToString();
                    break;
                default:
                    break;
            }

            return retVal;
        }

        public static int GetCountOfTablesInGroupLayers()
        {
            int cnt = 0;
            List<GroupLayer> groupLayerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<GroupLayer>().ToList();
            foreach (GroupLayer groupLayer in groupLayerList)
            {
                if (groupLayer.StandaloneTables.Count > 0)
                    cnt += groupLayer.StandaloneTables.Count;
            }

            return cnt;
        }

        public static string GetExtractFilePath()
        {
            return ExtractFileRootPath + GetProProjectName() + "\\" + GetActiveMapName();
        }

        public static string GetLayerTypeDescription(Layer layer)
        {
            string retVal;

            if (layer is FeatureLayer)
                retVal = "Feature Layer";
            else if (layer is GroupLayer)
                retVal = "Group Layer";
            else if (layer is SubtypeGroupLayer)
                retVal = "Subtype Group Layer";
            else if (layer is AnnotationLayer)
                retVal = "Annotation Layer";
            else if (layer is AnnotationSubLayer)
                retVal = "Annotation Sub Layer";
            else if (layer is DimensionLayer)
                retVal = "Dimension Layer";
            else if (layer is UtilityNetworkLayer)
                retVal = "Utility Network Layer";
            else if (layer is TiledServiceLayer)
                retVal = "Tiled Service Layer";
            else if (layer is VectorTileLayer)
                retVal = "Vector Tile Layer";
            else if (layer is GraphicsLayer)
                retVal = "Graphics Layer";
            else if (layer is ImageServiceLayer)
                retVal = "Image Service Layer";
            else if (layer.MapLayerType == MapLayerType.BasemapBackground)
                retVal = "Basemap";
            else
                retVal = "Undefined in this Add-In";

            return retVal;
        }

        public static string GetActiveMapName()
        {
            //Strip out illegal character for file name
            string mapName = Path.GetInvalidPathChars().Aggregate(MapView.Active.Map.Name, (current, c) => current.Replace(c.ToString(), string.Empty));
            return mapName.Replace(",", "").Replace("'", "").Replace("\"", "");
        }

        public static string GetProProjectName()
        {
            Project currProject = Project.Current;
            return currProject.Name.Substring(0, currProject.Name.LastIndexOf("."));
        }

        private static string GetProVersion()
        {
            Assembly assembly = Assembly.GetEntryAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return $"{fvi.ProductMajorPart}.{fvi.ProductMinorPart}.{fvi.ProductBuildPart}";
        }

        public static PropertyInfo[] GetPropertiesOfClass<T>(T cls)
        {
            return typeof(T).GetProperties();
        }

        public static string GetScaleValueText(double scale)
        {
            if (scale == 0)
                return "None";  // In Pro, when there is no scale set, the value is <None>.  Thru the SDK, it is 0.
            else
                return scale.ToString();
        }

        public static Table GetTableFromFeatureLayer(FeatureLayer featureLayer)
        {
            Table table = featureLayer.GetTable();
            if (table is FeatureClass)
                return table;

            return null;
        }

        public static string AppendTokenToUrl(string url, string token)
        {
            return $"{url}?f=json&token={token}";
        }

        public static UtilityNetwork GetUtilityNetwork(out FeatureLayer featureLayer)
        {
            featureLayer = null;
            UtilityNetwork utilityNetwork = null;

            if (MapView.Active == null)
                return utilityNetwork;

            IEnumerable<Layer> layers = MapView.Active.Map.GetLayersAsFlattenedList();
            foreach (Layer layer in layers)
            {
                utilityNetwork = GetUtilityNetwork(layer, out featureLayer);
                if (utilityNetwork != null)
                    break;
            }
            return utilityNetwork;
        }

        private static UtilityNetwork GetUtilityNetwork(Layer layer, out FeatureLayer featureLayer)
        {
            featureLayer = null;
            UtilityNetwork utilityNetwork = null;

            if (layer is UtilityNetworkLayer utilityNetworkLayer)
            {
                utilityNetwork = utilityNetworkLayer.GetUtilityNetwork();

                CompositeLayer compositeLayer = layer as CompositeLayer;
                featureLayer = compositeLayer.Layers.First() as FeatureLayer;
            }

            else if (layer is SubtypeGroupLayer)
            {
                CompositeLayer compositeLayer = layer as CompositeLayer;
                utilityNetwork = GetUtilityNetwork(compositeLayer.Layers.First(), out featureLayer);
            }

            else if (layer is FeatureLayer)
            {
                featureLayer = layer as FeatureLayer;
                using (FeatureClass featureClass = featureLayer.GetFeatureClass())
                {
                    if (featureClass != null) // invalid layers won't have a featureclass value
                    {
                        if (featureClass.IsControllerDatasetSupported())
                        {
                            IReadOnlyList<Dataset> controllerDatasets = new List<Dataset>();
                            controllerDatasets = featureClass.GetControllerDatasets();
                            foreach (Dataset controllerDataset in controllerDatasets)
                            {
                                if (controllerDataset is UtilityNetwork)
                                    utilityNetwork = controllerDataset as UtilityNetwork;
                                else
                                    controllerDataset.Dispose();
                            }
                        }
                    }
                }
            }
            return utilityNetwork;
        }

        public static EsriHttpResponseMessage QueryRestPointUsingGet(string url)
        {
            EsriHttpClient esriHttpClient = new();
            EsriHttpResponseMessage response;
            try
            {
                response = esriHttpClient.Get(url);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception)
            { 
                throw;
            }
            return response;
        }
                
        public static string CreateCsvFileContainingMapName(string reportTitle)
        {
            return CreateFile(reportTitle, string.Empty, "csv", true);
        }

        public static string CreateCsvFile(string reportTitle, string dataSourceName)
        {
            return CreateFile(reportTitle, dataSourceName, "csv");
        }

        public static string CreateTextFile(string reportTitle, string dataSourceName)
        {
            return CreateFile(reportTitle, dataSourceName, "txt");
        }

        private static string CreateFile(string reportTitle, string dataSourceName, string extension, bool mapCentric = false)
        {
            string outputFile;
            string fileName;
            string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            if (string.IsNullOrEmpty(dataSourceName)) // datasource could be: FeatureService, fGDB, mGDB (Sqlite), SDE connection
            {
                if (mapCentric)
                    fileName = $"{dateFormatted}_{GetActiveMapName()}_{reportTitle}.{extension}";
                else
                    fileName = $"{dateFormatted}_{reportTitle}.{extension}";

                outputFile = Path.Combine(ExtractFilePath, fileName);
            }
            else
            {
                //create new folder using the datasource name
                string newFolder = Path.Combine(ExtractFilePath, dataSourceName);

                if (!Directory.Exists(newFolder))
                    Directory.CreateDirectory(newFolder);

                fileName = $"{dateFormatted}_{dataSourceName}_{reportTitle}.{extension}";
                outputFile = Path.Combine(newFolder, fileName);
            }
                                       
            return outputFile;
        }

        public static void WriteHeaderInfoForMap(StreamWriter sw, string reportTitle)
        {
            sw.WriteLine(DateTime.Now + "," + reportTitle);
            sw.WriteLine();
            sw.WriteLine("Project," + Project.Current.Path);
            sw.WriteLine("Map," + GetActiveMapName());
            sw.WriteLine();
        }

        public static void WriteHeaderInfoForGeodatabase(StreamWriter sw, DataSourceInMap dataSourceInMap, string reportTitle)
        {
            sw.WriteLine(DateTime.Now + "," + reportTitle);
            sw.WriteLine();
            sw.WriteLine(dataSourceInMap.WorkspaceFactory + "," + dataSourceInMap.URI);           
            IReadOnlyList<UtilityNetworkDefinition> utilityNetworkDefinitionList = dataSourceInMap.Geodatabase.GetDefinitions<UtilityNetworkDefinition>();
            if (utilityNetworkDefinitionList.Count > 0)
                WriteUtilityNetworkInfo(sw, utilityNetworkDefinitionList.FirstOrDefault());
            sw.WriteLine("ArcGIS Pro Version," + GetProVersion());
            sw.WriteLine();
        }

        public static void WriteHeaderInfoForUtilityNetwork(StreamWriter sw, UtilityNetworkDataSourceInMap utilityNetworkDataSourceInMap, string reportTitle)
        {
            sw.WriteLine(DateTime.Now + "," + reportTitle);
            sw.WriteLine();
            sw.WriteLine(utilityNetworkDataSourceInMap.WorkspaceFactory + "," + utilityNetworkDataSourceInMap.URI);
            WriteUtilityNetworkInfo(sw, utilityNetworkDataSourceInMap.UtilityNetwork.GetDefinition());
            sw.WriteLine("ArcGIS Pro Version," + GetProVersion());
            sw.WriteLine();
        }

        private static void WriteUtilityNetworkInfo(StreamWriter sw, UtilityNetworkDefinition utilityNetworkDefinition)
        {
            sw.WriteLine("Utility Network Name," + utilityNetworkDefinition.GetName());
            sw.WriteLine("Utility Network Release," + utilityNetworkDefinition.GetSchemaVersion());
        }
    }
}