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
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace UtilityNetworkPropertiesExtractor
{
    public static class Common
    {
        public static string ExtractFilePath;
        private const string _extractFileRootPath = @"C:\temp\ProSdk_CSV\";

        //Two Field Setting buttons are writing/reading the same files.  Constants used to ensure that a code change to 1 button doesn't break the other
        public const string FieldSettingsClassNameHeader = "Class Name";

        public const string Delimiter = ",";

        public static string GetExtractFilePath()
        {
            return _extractFileRootPath + GetProProjectName();
        }

        public static void CreateOutputDirectory()
        {
            ExtractFilePath = GetExtractFilePath();

            if (!Directory.Exists(ExtractFilePath))
                Directory.CreateDirectory(ExtractFilePath);
        }

        public static class DatastoreTypeDescriptions
        {
            public const string FeatureService = "FeatureService";
            public const string FileGDB = "File Geodatabase";
            public const string EnterpriseGDB = "Enterprise Geodatabase";
        }

        public static ReportHeaderInfo DetermineReportHeaderProperties(UtilityNetwork utilityNetwork, FeatureLayer featureLayer)
        {
            ReportHeaderInfo reportHeaderInfo = new ReportHeaderInfo
            {
                ProProjectName = GetProProjectName()
            };

            if (utilityNetwork == null && featureLayer == null)
                return reportHeaderInfo;

            reportHeaderInfo.FullPath = featureLayer.GetPath().AbsoluteUri;

            int pos;
            if (reportHeaderInfo.FullPath.Contains(@"/rest/"))
            {
                //Path is to a specific layer.  https://utilities-un.bd.esri.com/server/rest/services/Naperville1081/NapervilleUN_V34_1081/FeatureServer/6
                //Need to strip off all characters after FeatureServer
                string searchstring = "FeatureServer";
                pos = reportHeaderInfo.FullPath.IndexOf(searchstring);
                reportHeaderInfo.FullPath = featureLayer.GetPath().AbsoluteUri.Substring(0, pos + searchstring.Length);
                reportHeaderInfo.SourceType = DatastoreTypeDescriptions.FeatureService;
            }
            else if (reportHeaderInfo.FullPath.Contains(".gdb"))
            {
                reportHeaderInfo.SourceType = DatastoreTypeDescriptions.FileGDB;
                pos = featureLayer.GetPath().AbsoluteUri.IndexOf(".gdb");
                reportHeaderInfo.FullPath = featureLayer.GetPath().AbsoluteUri.Substring(0, pos + 4);
            }
            else if (reportHeaderInfo.FullPath.Contains(".sde"))
            {
                reportHeaderInfo.SourceType = DatastoreTypeDescriptions.EnterpriseGDB;
                pos = featureLayer.GetPath().AbsoluteUri.IndexOf(".sde");
                reportHeaderInfo.FullPath = featureLayer.GetPath().AbsoluteUri.Substring(0, pos + 4);
            }

            // Only applies if Utility Network is detected
            if (utilityNetwork != null)
            {
                reportHeaderInfo.UtilityNetworkName = utilityNetwork.GetName();
                reportHeaderInfo.UtiltyNetworkSchemaVersion = utilityNetwork.GetDefinition().GetSchemaVersion();
            }

            return reportHeaderInfo;
        }

        public static string DetermineTimeDifference(DateTime startTime, DateTime endTime)
        {
            TimeSpan traceTime = endTime.Subtract(startTime);
            return string.Format("{0}:{1}:{2}", traceTime.Hours, traceTime.Minutes, traceTime.Seconds);
        }

        public static string GetProProjectName()
        {
            Project currProject = Project.Current;
            return currProject.Name.Substring(0, currProject.Name.IndexOf("."));
        }

        public static PropertyInfo[] GetPropertiesOfClass<T>(T cls)
        {
            return typeof(T).GetProperties();
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

        public static Table GetTableFromFeatureLayer(FeatureLayer featureLayer)
        {
            Table table = featureLayer.GetTable();
            if (table is FeatureClass)
                return table;

            return null;
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
            return utilityNetwork;
        }

        public static void WriteHeaderInfo(StreamWriter sw, ReportHeaderInfo reportHeaderInfo, UtilityNetworkDefinition utilityNetworkDefinition, string reportTitle)
        {
            sw.WriteLine(DateTime.Now + "," + reportTitle);
            sw.WriteLine();
            sw.WriteLine(reportHeaderInfo.SourceType + "," + reportHeaderInfo.FullPath);
            if (utilityNetworkDefinition != null)
                WriteUnHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition);
            sw.WriteLine("ArcGIS Pro Version," + System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString());
            sw.WriteLine();
        }

        private static void WriteUnHeaderInfo(StreamWriter sw, ReportHeaderInfo reportHeaderInfo, UtilityNetworkDefinition utilityNetworkDefinition)
        {
            sw.WriteLine("Utility Network Name," + reportHeaderInfo.UtilityNetworkName);
            sw.WriteLine("Utility Network Release," + utilityNetworkDefinition.GetSchemaVersion());
        }

        public class ReportHeaderInfo
        {
            public string FullPath { get; set; }
            public string SourceType { get; set; }
            public string ProProjectName { get; set; }
            public string UtilityNetworkName { get; set; }
            public string UtiltyNetworkSchemaVersion { get; set; }
        }
    }
}