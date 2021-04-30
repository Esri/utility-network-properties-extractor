using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class GdbObjectNamesButton : Button
    {
        private static string _fileName = string.Empty;

        protected async override void OnClick()
        {
            try
            {
                await ExtractGdbObjectNamesAsync();
                MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract GDB Object Names");
            }
        }

        public static Task ExtractGdbObjectNamesAsync()
        {
            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayer);
                if (utilityNetwork == null)
                    featureLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().First();

                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayer);

                using (Geodatabase geodatabase = featureLayer.GetTable().GetDatastore() as Geodatabase)
                {
                    Common.CreateOutputDirectory();
                    string dateFormatted = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    _fileName = string.Format("{0}_{1}_GdbObjectNames.csv", dateFormatted, reportHeaderInfo.ProProjectName);
                    string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                    using (StreamWriter sw = new StreamWriter(outputFile))
                    {
                        //Header information
                        UtilityNetworkDefinition utilityNetworkDefinition = null;
                        if (utilityNetwork != null)
                            utilityNetworkDefinition = utilityNetwork.GetDefinition();

                        Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "GDB Object Names");

                        if (reportHeaderInfo.SourceType == Common.DatastoreTypeDescriptions.FeatureService)
                        {
                            sw.WriteLine("UN_#_Rules and other system table can be determined against direct connection only.");
                            sw.WriteLine("Against a service it will be shown as 'Rules' which is likely the Layer name.");
                            sw.WriteLine("");
                        }

                        sw.WriteLine("ObjectName,ObjectType");

                        List<DatasetType> datasetTypeList = new List<DatasetType>
                                                                    { DatasetType.UtilityNetwork,
                                                                      DatasetType.FeatureDataset,
                                                                      DatasetType.FeatureClass,
                                                                      DatasetType.Table,
                                                                      DatasetType.RelationshipClass,
                                                                      DatasetType.AttributedRelationshipClass};

                        if (reportHeaderInfo.SourceType == Common.DatastoreTypeDescriptions.FeatureService)
                            datasetTypeList.Remove(DatasetType.FeatureDataset); // Exception raised with this dataset type on a featureservice

                        foreach (DatasetType datasetType in datasetTypeList)
                        {
                            IReadOnlyList<Definition> definitionList = null;

                            switch (datasetType)
                            {
                                case DatasetType.UtilityNetwork:
                                    definitionList = geodatabase.GetDefinitions<UtilityNetworkDefinition>();
                                    break;

                                case DatasetType.FeatureDataset:
                                    definitionList = geodatabase.GetDefinitions<FeatureDatasetDefinition>();
                                    break;

                                case DatasetType.FeatureClass:
                                    definitionList = geodatabase.GetDefinitions<FeatureClassDefinition>();
                                    break;

                                case DatasetType.Table:
                                    definitionList = geodatabase.GetDefinitions<TableDefinition>();
                                    break;

                                case DatasetType.RelationshipClass:
                                    definitionList = geodatabase.GetDefinitions<RelationshipClassDefinition>();
                                    break;

                                case DatasetType.AttributedRelationshipClass:
                                    definitionList = geodatabase.GetDefinitions<AttributedRelationshipClassDefinition>();
                                    break;
                            }

                            foreach (Definition definition in definitionList)
                                sw.WriteLine(definition.GetName() + "," + definition.DatasetType.ToString());
                        }

                        sw.Flush();
                        sw.Close();
                    }
                }
            });
        }
    }
}