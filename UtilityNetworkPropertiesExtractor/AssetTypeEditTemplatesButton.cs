///*
//   Copyright 2021 Esri
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//       http://www.apache.org/licenses/LICENSE-2.0
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS, 
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//*/
//using ArcGIS.Core.CIM;
//using ArcGIS.Core.Data.UtilityNetwork;
//using ArcGIS.Desktop.Framework.Threading.Tasks;
//using ArcGIS.Desktop.Mapping;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using System.Windows.Forms;
//using Button = ArcGIS.Desktop.Framework.Contracts.Button;
//using MessageBox = System.Windows.Forms.MessageBox;

//namespace UtilityNetworkPropertiesExtractor
//{
//    internal class AssetTypeEditTemplatesButton : Button
//    {
//        private const string _assetGroupFieldName = "ASSETGROUP";
//        private const string _assetTypeFieldName = "ASSETYPE";

//        protected override void OnClick()
//        {
//            try
//            {
//                EditTemplateMasterAsync();
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show(ex.Message, "Asset Type Edit Templates");
//            }
//        }

//        private Task EditTemplateMasterAsync()
//        {
//            return QueuedTask.Run(() =>
//            {
//                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayerInUn);
//                if (utilityNetwork == null)
//                {
//                    MessageBox.Show("Utility Network not found in the active map", "Asset Type Edit Templates", MessageBoxButtons.OK, MessageBoxIcon.Error);
//                    return;
//                }

//                Common.ReportHeaderInfo reportHeaderInfo = Common.DetermineReportHeaderProperties(utilityNetwork, featureLayerInUn);

//                //Confirm with user before proceeding
//                DialogResult dialogResult = MessageBox.Show("Create Edit Templates based on the Utility Network's Asset Types?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
//                if (dialogResult == DialogResult.No)
//                    return;

//                //Pause Drawing
//                MapView.Active.DrawingPaused = true;

//                //Get Utility Network definition
//                UtilityNetworkDefinition utilityNetworkDefinition = utilityNetwork.GetDefinition();
//                IReadOnlyList<NetworkSource> networkSources = utilityNetworkDefinition.GetNetworkSources();

//                //Get list of all featurelayers in the map
//                List<FeatureLayer> featureLayerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();

//                //Create cancellable progress bar
//                using (ProgressDialog progress = new ProgressDialog("Processing", "Canceled", (uint)featureLayerList.Count, false))
//                {
//                    string progressMessage = string.Empty;
//                    CancelableProgressorSource cps = new CancelableProgressorSource(progress)
//                    {
//                        Max = (uint)featureLayerList.Count
//                    };

//                    QueuedTask.Run(() =>
//                    {
//                        foreach (FeatureLayer featureLayer in featureLayerList)
//                        {
//                            //if user clicks the cancel button, stop processing.
//                            if (cps.Progressor.CancellationToken.IsCancellationRequested)
//                                return;

//                            progressMessage = "Processing layer, " + featureLayer.Name + " (" + featureLayer.GetFeatureClass().GetName() + ") --> " + cps.Progressor.Value + " of " + featureLayerList.Count;
//                            cps.Progressor.Value += 1;
//                            cps.Progressor.Status = (cps.Progressor.Value * 100 / cps.Progressor.Max) + @"% Completed";
//                            cps.Progressor.Message = progressMessage;

//                            string tableName = featureLayer.GetTable().GetName();

//                            if (reportHeaderInfo.SourceType == Common.DatastoreTypeDescriptions.FeatureService)
//                            {
//                                //strip out the leading number from the table name "L0Electric_Device".  
//                                //Also need to replace the underscore with a blank space
//                                int index = tableName.LastIndexOfAny("0123456789".ToCharArray());
//                                tableName = tableName.Substring(index + 1).Replace("_", " ");
//                            }

//                            //Determine if layer is in the Utility Network
//                            NetworkSource networkSource = networkSources.Where(x => x.Name == tableName).FirstOrDefault();
//                            if (networkSource == null)
//                                continue;

//                            if (featureLayer.IsSubtypeLayer)
//                            {
//                                //Only use the asset group whose name matches the subtype layer.
//                                IReadOnlyList<AssetGroup> assetGroupsList = networkSource.GetAssetGroups();
//                                foreach (AssetGroup assetGroup in assetGroupsList)
//                                {
//                                    if (assetGroup.Name == featureLayer.Name)
//                                    {
//                                        CreateEditTemplate(featureLayer, assetGroup);
//                                        break;
//                                    }
//                                }
//                            }
//                            else
//                            {
//                                //Featurelayer  --> get all assetgroups
//                                IReadOnlyList<AssetGroup> assetGroupsList = networkSource.GetAssetGroups();
//                                CreateEditTemplate(featureLayer, assetGroupsList);
//                            }
//                        }
//                    }, cps.Progressor);
//                }
//                MapView.Active.DrawingPaused = false;
//            });
//        }

//        private static void CreateEditTemplate(FeatureLayer featureLayer, IReadOnlyList<AssetGroup> assetGroupList)
//        {
//            //Standard Utility Network Feature Layer (ex.  StructureJunction, ElecricDevice, etc);
//            CIMFeatureLayer layerDef = featureLayer.GetDefinition() as CIMFeatureLayer;
//            featureLayer.AutoGenerateTemplates(true);

//            foreach (AssetGroup assetGroup in assetGroupList)
//            {
//                IReadOnlyList<AssetType> assetTypesList = assetGroup.GetAssetTypes();
//                BuildTheTemplate(featureLayer, layerDef, assetGroup, assetTypesList);
//            }

//            featureLayer.SetDefinition(layerDef);
//        }

//        private static void CreateEditTemplate(FeatureLayer featureLayer, AssetGroup assetGroup)
//        {
//            //Subtype Group Layer --> only 1 assetGroup for each layer
//            CIMFeatureLayer layerDef = featureLayer.GetDefinition() as CIMFeatureLayer;
//            featureLayer.AutoGenerateTemplates(true);

//            IReadOnlyList<AssetType> assetTypesList = assetGroup.GetAssetTypes();
//            BuildTheTemplate(featureLayer, layerDef, assetGroup, assetTypesList);

//            featureLayer.SetDefinition(layerDef);
//        }

//        private static void BuildTheTemplate(BasicFeatureLayer featureLayer, CIMFeatureLayer layerDef, AssetGroup assetGroup, IReadOnlyList<AssetType> assetTypesList)
//        {
//            //get all templates on this layer
//            // NOTE - layerDef.FeatureTemplates could be null if Create Features window hasn't been opened
//            List<CIMEditingTemplate> layerTemplates = layerDef.FeatureTemplates?.ToList();
//            if (layerTemplates == null)
//                layerTemplates = new List<CIMEditingTemplate>();

//            bool createTemplate = true;

//            foreach (AssetType assetType in assetTypesList)
//            {
//                //check if asset group/asset type template already exists
//                foreach (CIMEditingTemplate template in layerTemplates)
//                {
//                    CIMFeatureTemplate cimFeatureTemplate = template as CIMFeatureTemplate;
//                    if (DoesTemplateAlreadyExist(cimFeatureTemplate, assetGroup, assetType))
//                        createTemplate = false;
//                }

//                if (createTemplate)
//                {
//                    //set new template values
//                    CIMFeatureTemplate myTemplateDef = new CIMFeatureTemplate
//                    {
//                        Name = assetGroup.Name + " " + assetType.Name + " Template",
//                        Description = assetGroup.Name + " " + assetType.Name + " Template"
//                    };

//                    myTemplateDef.WriteTags(new[] { "Created via Add-in" });

//                    //set some default attributes
//                    myTemplateDef.DefaultValues = new Dictionary<string, object>
//                    {
//                        { _assetGroupFieldName, assetGroup.Code },
//                        { _assetTypeFieldName, assetType.Code }
//                    };

//                    //Determine which editing tools to add based on teh geometry of the layer
//                    List<string> filter = BuildToolFilter(featureLayer, myTemplateDef);

//                    myTemplateDef.ToolFilter = filter.ToArray();

//                    //add the new template to the layer template list
//                    layerTemplates.Add(myTemplateDef);

//                    //update the layerdefinition with the templates
//                    layerDef.FeatureTemplates = layerTemplates.ToArray();

//                    // check the AutoGenerateFeatureTemplates flag, set to false so our changes will stick
//                    if (layerDef.AutoGenerateFeatureTemplates)
//                        layerDef.AutoGenerateFeatureTemplates = false;
//                }
//                else
//                    createTemplate = true; //reset variable
//            }
//        }

//        private static bool DoesTemplateAlreadyExist(CIMFeatureTemplate cimFeatureTemplate, AssetGroup assetGroup, AssetType assetType)
//        {
//            //Look for match on AssetGroup and AssetType
//            bool retVal = false;
//            int myCount = 0;

//            IDictionary<string, object> templateDict = cimFeatureTemplate.DefaultValues;
//            foreach (KeyValuePair<string, object> pair in templateDict)
//            {
//                if (pair.Key.ToUpper() == _assetGroupFieldName)
//                {
//                    if ((int)pair.Value == assetGroup.Code)
//                        myCount += 1;
//                }
//                else if (pair.Key.ToUpper() == _assetTypeFieldName)
//                {
//                    if ((int)pair.Value == assetType.Code)
//                        myCount += 1;
//                }

//                if (myCount == 2) // template exists
//                {
//                    retVal = true;
//                    break;
//                }
//            }
//            return retVal;
//        }

//        private static List<string> BuildToolFilter(BasicFeatureLayer featureLayer, CIMFeatureTemplate myTemplateDef)
//        {
//            List<string> filter = new List<string>();

//            switch (featureLayer.ShapeType)
//            {
//                case esriGeometryType.esriGeometryPoint:
//                    myTemplateDef.SetDefaultToolDamlID("esri_editing_SketchPointTool");
//                    filter.Add("esri_editing_ConstructPointsAlongLineCommand");
//                    filter.Add("esri_editing_SketchPointAtLineEndPointsTool");
//                    break;

//                case esriGeometryType.esriGeometryLine:
//                    myTemplateDef.SetDefaultToolDamlID("esri_editing_SketchLineTool");
//                    filter.Add("esri_editing_SketchRightLineTool");
//                    filter.Add("esri_editing_CreateAndCutFeatures");
//                    filter.Add("esri_editing_SketchRadialLineTool");
//                    filter.Add("esri_editing_SketchTwoPointLineTool");
//                    filter.Add("esri_editing_SketchCircleLineTool");
//                    filter.Add("esri_editing_SketchRectangleLineTool");
//                    filter.Add("esri_editing_SketchEllipseLineTool");
//                    filter.Add("esri_editing_SketchFreehandLineTool");
//                    filter.Add("esri_editing_SketchTraceLineTool");
//                    filter.Add("esri_editing_SketchStreamLineTool");
//                    break;

//                case esriGeometryType.esriGeometryPolygon:
//                    myTemplateDef.SetDefaultToolDamlID("esri_editing_SketchPolygonTool");
//                    filter.Add("esri_editing_SketchAutoCompletePolygonTool");
//                    filter.Add("esri_editing_SketchRightPolygonTool");
//                    filter.Add("esri_editing_SketchCirclePolygonTool");
//                    filter.Add("esri_editing_SketchRectanglePolygonTool");
//                    filter.Add("esri_editing_SketchEllipsePolygonTool");
//                    filter.Add("esri_editing_SketchFreehandPolygonTool");
//                    filter.Add("esri_editing_SketchAutoCompleteFreehandPolygonTool");
//                    break;
//            }

//            return filter;
//        }
//    }
//}