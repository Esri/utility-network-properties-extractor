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
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Button = ArcGIS.Desktop.Framework.Contracts.Button;
using MessageBox = System.Windows.Forms.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class DisplayFieldExpressionButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                SetDisplayFieldExpressionAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Set Display Field Expression");
            }
        }

        private Task SetDisplayFieldExpressionAsync()
        {
            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayerInUn);
                if (utilityNetwork == null)
                {
                    MessageBox.Show("Utility Network not found in the active map", "Set Display Field Expression", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                //Confirm with user before proceeding
                DialogResult dialogResult = MessageBox.Show("Modify display field expression to be Utility Network meaningful?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogResult == DialogResult.No)
                    return;

                //Pause drawing
                MapView.Active.DrawingPaused = true;

                //Get list of all featurelayers in the map
                List<FeatureLayer> featureLayerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();

                //Standalone Tables in the map
                IReadOnlyList<StandaloneTable> standaloneTableList = MapView.Active.Map.StandaloneTables;

                int total = featureLayerList.Count + standaloneTableList.Count;

                using (ProgressDialog progress = new ProgressDialog("Processing", "Canceled", (uint)total, false))
                {
                    string progressMessage = string.Empty;
                    CancelableProgressorSource cps = new CancelableProgressorSource(progress)
                    {
                        Max = (uint)featureLayerList.Count + (uint)standaloneTableList.Count
                    };

                    QueuedTask.Run(() =>
                    {
                        foreach (FeatureLayer featureLayer in featureLayerList)
                        {
                            //if user clicks the cancel button, stop processing.
                            if (cps.Progressor.CancellationToken.IsCancellationRequested)
                                return;

                            progressMessage = "Processing layer, " + featureLayer.Name + " (" + featureLayer.GetFeatureClass().GetName() + ") --> " + cps.Progressor.Value + " of " + total;
                            cps.Progressor.Value += 1;
                            cps.Progressor.Status = (cps.Progressor.Value * 100 / cps.Progressor.Max) + @"% Completed";
                            cps.Progressor.Message = progressMessage;

                            //Based on fields in featureclass, determine the display field expression.
                            List<FieldDescription> fieldDescList = featureLayer.GetFieldDescriptions();
                            FieldDescription assetTypeRec = fieldDescList.Where(x => x.Name.ToUpper() == "ASSETTYPE").FirstOrDefault();
                            FieldDescription dirtyAreaRec = fieldDescList.Where(x => x.Name.ToUpper() == "DIRTYAREA").FirstOrDefault();
                            FieldDescription subnetLineRec = fieldDescList.Where(x => x.Name.ToUpper() == "LASTACKEXPORTSUBNETWORK").FirstOrDefault();

                            if (assetTypeRec != null || dirtyAreaRec != null || subnetLineRec != null)
                            {
                                CIMBasicFeatureLayer cimBasicFeatureLayer = featureLayer.GetDefinition() as CIMBasicFeatureLayer;
                                CIMFeatureTable cimFeatureTable = cimBasicFeatureLayer.FeatureTable;
                                CIMExpressionInfo cimExpressionInfo = cimFeatureTable.DisplayExpressionInfo;

                                if (cimExpressionInfo is null)
                                    cimExpressionInfo = new CIMExpressionInfo();

                                if (assetTypeRec != null)
                                {
                                    if (featureLayer.IsSubtypeLayer)
                                    {
                                        cimExpressionInfo.Title = "Asset Type and Objectid";
                                        cimExpressionInfo.Expression = "return DomainName($feature, 'ASSETTYPE', $feature.ASSETTYPE, $feature.ASSETGROUP) + ' ' + $feature.OBJECTID";
                                    }
                                    else
                                    {
                                        cimExpressionInfo.Title = "Asset Group, Asset Type and Objectid";
                                        cimExpressionInfo.Expression = "return DomainName($feature, 'ASSETGROUP', $feature.ASSETGROUP, $feature.ASSETGROUP) + ', ' + DomainName($feature, 'ASSETTYPE', $feature.ASSETTYPE, $feature.ASSETGROUP) + ' ' + $feature.OBJECTID";
                                    }
                                }
                                else if (subnetLineRec != null)
                                {
                                    cimExpressionInfo.Title = "Subnetworkname";
                                    cimExpressionInfo.Expression = "$feature.SUBNETWORKNAME";
                                }
                                else if (dirtyAreaRec != null)
                                {
                                    cimExpressionInfo.Title = "Objectid";
                                    cimExpressionInfo.Expression = "$feature.OBJECTID";
                                }

                                cimFeatureTable.DisplayExpressionInfo = cimExpressionInfo;
                                featureLayer.SetDefinition(cimBasicFeatureLayer);
                            }
                        }

                        foreach (StandaloneTable standaloneTable in standaloneTableList)
                        {
                            //if user clicks the cancel button, stop processing.
                            if (cps.Progressor.CancellationToken.IsCancellationRequested)
                                return;

                            progressMessage = "Processing table, " + standaloneTable.Name + " --> " + cps.Progressor.Value + " of " + total;
                            cps.Progressor.Value += 1;
                            cps.Progressor.Status = (cps.Progressor.Value * 100 / cps.Progressor.Max) + @"% Completed";
                            cps.Progressor.Message = progressMessage;

                            List<FieldDescription> fieldDescList = standaloneTable.GetFieldDescriptions();
                            FieldDescription assetTypeRec = fieldDescList.Where(x => x.Name.ToUpper() == "ASSETTYPE").FirstOrDefault();

                            if (assetTypeRec != null)
                            {
                                CIMStandaloneTable cimStandaloneTable = standaloneTable.GetDefinition();
                                CIMExpressionInfo cimExpressionInfo = cimStandaloneTable.DisplayExpressionInfo;

                                if (cimExpressionInfo is null)
                                    cimExpressionInfo = new CIMExpressionInfo();

                                cimExpressionInfo.Title = "Asset Group, Asset Type and Objectid";
                                cimExpressionInfo.Expression = "return DomainName($feature, 'ASSETGROUP', $feature.ASSETGROUP, $feature.ASSETGROUP) + ', ' + DomainName($feature, 'ASSETTYPE', $feature.ASSETTYPE, $feature.ASSETGROUP) + ' ' + $feature.OBJECTID"; ;

                                cimStandaloneTable.DisplayExpressionInfo = cimExpressionInfo;
                                standaloneTable.SetDefinition(cimStandaloneTable);
                            }
                        }
                    }, cps.Progressor);
                }
                MapView.Active.DrawingPaused = false;
            });
        }
    }
}
