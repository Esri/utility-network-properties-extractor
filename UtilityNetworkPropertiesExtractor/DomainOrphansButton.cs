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
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
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
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Orphan Domains to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractOrphanDomainsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extract Domain Values");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractOrphanDomainsAsync()
        {
            return QueuedTask.Run(() =>
            {
                List<DataSourceInMap> dataSourceInMapList = DataSourcesInMapHelper.GetDataSourcesInMap();
                foreach (DataSourceInMap dataSourceInMap in dataSourceInMapList)
                {
                    if (dataSourceInMap.WorkspaceFactory != WorkspaceFactory.Shapefile.ToString())
                    {
                        using (Geodatabase geodatabase = dataSourceInMap.Geodatabase)
                        {
                            string outputFile = Common.BuildCsvName("OrphanDomains", dataSourceInMap.NameForCSV);
                            using (StreamWriter sw = new StreamWriter(outputFile))
                            {
                                //Header information
                                Common.WriteHeaderInfoForGeodatabase(sw, dataSourceInMap, "Orphan Domains");

                                HashSet<string> assignedDomainsList = new HashSet<string>();

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
                                        IReadOnlyList<Subtype> subtypes = tableDefinition.GetSubtypes();

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