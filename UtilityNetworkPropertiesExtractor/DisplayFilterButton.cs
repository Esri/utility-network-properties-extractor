using ArcGIS.Core.CIM;
using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Button = ArcGIS.Desktop.Framework.Contracts.Button;
using MessageBox = System.Windows.Forms.MessageBox;

namespace UtilityNetworkPropertiesExtractor
{
    internal class DisplayFilterButton : Button
    {
        private const string _ContainmentFilterName = "DisplayContent";
        private const string _AssocationStatusFieldName = "ASSOCIATIONSTATUS";

        protected override void OnClick()
        {
            SetDisplayFilterChoiceAsync();
        }

        private Task SetDisplayFilterChoiceAsync()
        {
            return QueuedTask.Run(() =>
            {
                UtilityNetwork utilityNetwork = Common.GetUtilityNetwork(out FeatureLayer featureLayerInUn);
                if (utilityNetwork == null)
                {
                    MessageBox.Show("Utility Network not found in the active map", "Set Display Filters", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                //Confirm with user before proceeding
                DialogResult dialogResult = MessageBox.Show("Create Containment Display Filters on Utility Network layers?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogResult == DialogResult.No)
                    return;

                //pause drawing
                MapView.Active.DrawingPaused = true;

                bool createFilter = true;

                //Get all layers in the map
                List<Layer> layerList = MapView.Active.Map.GetLayersAsFlattenedList().OfType<Layer>().ToList();

                using (ProgressDialog progress = new ProgressDialog("Processing", "Canceled", (uint)layerList.Count, false))
                {
                    string progressMessage = string.Empty;
                    CancelableProgressorSource cps = new CancelableProgressorSource(progress)
                    {
                        Max = (uint)layerList.Count
                    };

                    QueuedTask.Run(() =>
                    {
                        foreach (Layer layer in layerList)
                        {
                            //if user clicks the cancel button, stop processing.
                            if (cps.Progressor.CancellationToken.IsCancellationRequested)
                                return;

                            progressMessage = "Processing layer, " + layer.Name + " --> " + cps.Progressor.Value + " of " + layerList.Count;
                            cps.Progressor.Value += 1;
                            cps.Progressor.Status = (cps.Progressor.Value * 100 / cps.Progressor.Max) + @"% Completed";
                            cps.Progressor.Message = progressMessage;

                            if (layer is SubtypeGroupLayer subtypeGroupLayer)
                            {
                                CompositeLayer compositeLayer = layer as CompositeLayer;
                                FeatureLayer featureLayer = compositeLayer.Layers.First() as FeatureLayer;
                                FieldDescription associationStatusFieldDesc = FieldDescriptionOfAssociationstatus(featureLayer);
                                if (associationStatusFieldDesc != null)
                                {
                                    CIMSubtypeGroupLayer cimSubtypeGroupLayerDefinition = subtypeGroupLayer.GetDefinition() as CIMSubtypeGroupLayer;

                                    //Get list of existing Display Filter choices
                                    List<CIMDisplayFilter> existingDisplayFilterChoicesList = cimSubtypeGroupLayerDefinition.DisplayFilterChoices?.ToList();
                                    if (existingDisplayFilterChoicesList == null)
                                        existingDisplayFilterChoicesList = new List<CIMDisplayFilter>();
                                    else
                                        createFilter = CreateContainmentFilter(existingDisplayFilterChoicesList);

                                    if (createFilter)  //Add new display filter for containment
                                    {
                                        cimSubtypeGroupLayerDefinition.EnableDisplayFilters = true;
                                        cimSubtypeGroupLayerDefinition.DisplayFiltersType = DisplayFilterType.ByChoice;

                                        existingDisplayFilterChoicesList.Add(BuildDisplayFilterForContainment());
                                        cimSubtypeGroupLayerDefinition.DisplayFilterChoices = existingDisplayFilterChoicesList.ToArray();
                                        layer.SetDefinition(cimSubtypeGroupLayerDefinition);
                                    }
                                }
                            }
                            else if (layer is FeatureLayer featureLayer)
                            {
                                if (featureLayer.IsSubtypeLayer)
                                    continue;  // if subtype layer, then the display filter is assigned to the SubtypeGroupLayer.
                                else
                                {
                                    FieldDescription associationStatusFieldDesc = FieldDescriptionOfAssociationstatus(featureLayer);
                                    if (associationStatusFieldDesc != null)
                                    {
                                        CIMFeatureLayer cimFeatureLayerDefinition = layer.GetDefinition() as CIMFeatureLayer;

                                        //Get list of existing Display Filter choices
                                        List<CIMDisplayFilter> existingDisplayFilterChoicesList = cimFeatureLayerDefinition.DisplayFilterChoices?.ToList();
                                        if (existingDisplayFilterChoicesList == null)
                                            existingDisplayFilterChoicesList = new List<CIMDisplayFilter>();
                                        else
                                            createFilter = CreateContainmentFilter(existingDisplayFilterChoicesList);

                                        if (createFilter)
                                        {
                                            //Add new display filter for containment
                                            cimFeatureLayerDefinition.EnableDisplayFilters = true;
                                            cimFeatureLayerDefinition.DisplayFiltersType = DisplayFilterType.ByChoice;
                                            existingDisplayFilterChoicesList.Add(BuildDisplayFilterForContainment());

                                            cimFeatureLayerDefinition.DisplayFilterChoices = existingDisplayFilterChoicesList.ToArray();
                                            layer.SetDefinition(cimFeatureLayerDefinition);
                                        }
                                    }
                                }
                            }
                            createFilter = true;
                        }
                    }, cps.Progressor);
                }
                MapView.Active.DrawingPaused = false;
            });
        }

        private static FieldDescription FieldDescriptionOfAssociationstatus(FeatureLayer featureLayer)
        {
            List<FieldDescription> fieldDescList = featureLayer.GetFieldDescriptions();
            FieldDescription associationStatus = fieldDescList.Where(x => x.Name.ToUpper() == _AssocationStatusFieldName).FirstOrDefault();
            return associationStatus;
        }

        private static bool CreateContainmentFilter(List<CIMDisplayFilter> existingDisplayFilterChoicesList)
        {
            //check if Containment filter already exists
            bool retVal = true;
            foreach (CIMDisplayFilter displayFilter in existingDisplayFilterChoicesList)
            {
                if (displayFilter.Name == _ContainmentFilterName)
                {
                    retVal = false;
                    continue;
                }
            }

            return retVal;
        }

        private static CIMDisplayFilter BuildDisplayFilterForContainment()
        {
            return new CIMDisplayFilter
            {
                Name = _ContainmentFilterName,
                WhereClause = string.Format("{0} not in (4,5,6,12,13,14,36,37,38,44,45,46)", _AssocationStatusFieldName)
            };
        }
    }
}
