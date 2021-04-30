using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class RelationshipClassButton : Button
    {
        private static string _fileName = string.Empty;

        protected async override void OnClick()
        {
            try
            {
                await ExtractRelationshipClassesAsync();
                MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract GDB Object Names");
            }
        }

        public static Task ExtractRelationshipClassesAsync()
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
                    _fileName = string.Format("{0}_{1}_RelationshipClasses.csv", dateFormatted, reportHeaderInfo.ProProjectName);
                    string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                    using (StreamWriter sw = new StreamWriter(outputFile))
                    {
                        //Header information
                        UtilityNetworkDefinition utilityNetworkDefinition = null;
                        if (utilityNetwork != null)
                            utilityNetworkDefinition = utilityNetwork.GetDefinition();

                        IReadOnlyList<RelationshipClassDefinition> relateDefList = geodatabase.GetDefinitions<RelationshipClassDefinition>();

                        Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "Relationship Classes");
                        sw.WriteLine("Relationship Class Count," + relateDefList.Count);
                        sw.WriteLine();

                        //Get all properties defined in the class.  This will be used to generate the CSV file
                        CSVLayout emptyRec = new CSVLayout();
                        PropertyInfo[] properties = Common.GetPropertiesOfClass(emptyRec);

                        //Write column headers based on properties in the class
                        string columnHeader = Common.ExtractClassPropertyNamesToString(properties);
                        sw.WriteLine(columnHeader);

                        List<CSVLayout> csvLayoutList = new List<CSVLayout>();

                        foreach (RelationshipClassDefinition relateDef in relateDefList)
                        {
                            CSVLayout rec = new CSVLayout()
                            {
                                RelationshipClass = relateDef.GetName(),
                                Cardinality = relateDef.GetCardinality().ToString(),
                                OriginName = relateDef.GetOriginClass(),
                                OriginPrimaryKey = relateDef.GetOriginKeyField(),
                                OriginForeignKey = relateDef.GetOriginForeignKeyField(),
                                DestinationName = relateDef.GetDestinationClass(),
                                SplitPolicy = relateDef.GetRelationshipSplitPolicy().ToString(),
                                AttachmentRelationship = relateDef.IsAttachmentRelationship().ToString(),
                                IsComposite = relateDef.IsComposite().ToString()
                            };
                            csvLayoutList.Add(rec);
                        }

                        //Write body of CSV
                        foreach (CSVLayout row in csvLayoutList.OrderBy(x => x.RelationshipClass))
                        {
                            string output = Common.ExtractClassValuesToString(row, properties);
                            sw.WriteLine(output);
                        }
                    }
                }
            });
        }

        private class CSVLayout
        {
            public string RelationshipClass { get; set; }
            public string Cardinality { get; set; }
            public string OriginName { get; set; }
            public string OriginPrimaryKey { get; set; }
            public string OriginForeignKey { get; set; }
            public string DestinationName { get; set; }
            public string SplitPolicy { get; set; }
            public string AttachmentRelationship { get; set; }
            public string IsComposite { get; set; }
        }
    }
}