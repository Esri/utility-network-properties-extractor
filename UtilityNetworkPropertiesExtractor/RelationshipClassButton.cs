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
using System.Reflection;
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class RelationshipClassButton : Button
    {
        protected async override void OnClick()
        {
            Common.CreateOutputDirectory();
            ProgressDialog progDlg = new ProgressDialog("Extracting Relationship Classes to: \n" + Common.ExtractFilePath);

            try
            {
                progDlg.Show();
                await ExtractRelationshipClassesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Extracting Relationship Classes");
            }
            finally
            {
                progDlg.Dispose();
            }
        }

        public static Task ExtractRelationshipClassesAsync()
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
                            string outputFile = Common.BuildCsvName("RelationshipClasses", dataSourceInMap.Name);
                            using (StreamWriter sw = new StreamWriter(outputFile))
                            {
                                //Header information
                                IReadOnlyList<RelationshipClassDefinition> relateDefList = geodatabase.GetDefinitions<RelationshipClassDefinition>();
                                Common.WriteHeaderInfoForGeodatabase(sw, dataSourceInMap, "Relationship Classes");
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