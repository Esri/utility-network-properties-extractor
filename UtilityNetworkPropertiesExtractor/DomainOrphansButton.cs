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
    internal class DomainOrphansButton : Button
    {
        private static string _fileName = string.Empty;

        protected async override void OnClick()
        {
            try
            {
                await ExtractOrphanDomainsAsync();
                MessageBox.Show("Directory: " + Common.ExtractFilePath + Environment.NewLine + "File Name: " + _fileName, "CSV file has been generated");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Orphan Domains");
            }
        }

        public static Task ExtractOrphanDomainsAsync()
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
                    _fileName = string.Format("{0}_{1}_OrphanDomains.csv", dateFormatted, reportHeaderInfo.ProProjectName);
                    string outputFile = Path.Combine(Common.ExtractFilePath, _fileName);

                    string defaultCode = string.Empty;
                    string defaultValue = string.Empty;
                    using (StreamWriter sw = new StreamWriter(outputFile))
                    {
                        //Header information
                        UtilityNetworkDefinition utilityNetworkDefinition = null;
                        if (utilityNetwork != null)
                            utilityNetworkDefinition = utilityNetwork.GetDefinition();

                        HashSet<string> assignedDomainsList = new HashSet<string>();
                        Common.WriteHeaderInfo(sw, reportHeaderInfo, utilityNetworkDefinition, "Orphan Domains");

                        //Get Domains assigned to a featureclass
                        IReadOnlyList<FeatureClassDefinition> featureClassDefinitions = geodatabase.GetDefinitions<FeatureClassDefinition>();
                        foreach (FeatureClassDefinition fcDefinition in featureClassDefinitions)
                        {
                            try
                            {
                                IReadOnlyList<Field> listOfFields = fcDefinition.GetFields();
                                IReadOnlyList<Subtype> subtypes = fcDefinition.GetSubtypes();

                                if (subtypes.Count != 0)
                                {
                                    foreach (Subtype subtype in subtypes)
                                        PopulateAssignedDomainList(assignedDomainsList, listOfFields, subtype);
                                }
                                else
                                    PopulateAssignedDomainList(assignedDomainsList, listOfFields, null);
                            }
                            catch (Exception ex)
                            {
                                if (ex.HResult != -2146233088) // No database permissions to perform the operation.
                                    MessageBox.Show(ex.Message);
                            }
                        }

                        //Get Domains assigned to a table
                        IReadOnlyList<TableDefinition> tableDefinitions = geodatabase.GetDefinitions<TableDefinition>();
                        foreach (TableDefinition tableDefinition in tableDefinitions)
                        {
                            try
                            {
                                IReadOnlyList<Field> listOfFields = tableDefinition.GetFields();
                                PopulateAssignedDomainList(assignedDomainsList, listOfFields, null);
                            }
                            catch (Exception ex)
                            {
                                if (ex.HResult != -2146233088) // No database permissions to perform the operation.
                                    MessageBox.Show(ex.Message);
                            }
                        }

                        //Now loop through each domain in the geodatabase and see if it's in the assigned list
                        sw.WriteLine("Domain Name");
                        IEnumerable<Domain> domainsList = geodatabase.GetDomains().OrderBy(x => x.GetName());
                        foreach (Domain domain in domainsList)
                        {
                            if (!assignedDomainsList.Contains(domain.GetName()))
                                sw.WriteLine(domain.GetName());
                        }

                        sw.Flush();
                        sw.Close();
                        assignedDomainsList.Clear();
                    }
                }
            });
        }

        private static void PopulateAssignedDomainList(HashSet<string> assignedDomainsList, IReadOnlyList<Field> listOfFields, Subtype subtype)
        {
            foreach (Field field in listOfFields)
            {
                Domain domain = field.GetDomain(subtype);
                if (domain != null)
                {
                    if (!assignedDomainsList.Contains(domain.GetName()))
                        assignedDomainsList.Add(domain.GetName());
                }
            }
        }
    }
}
