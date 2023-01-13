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
namespace UtilityNetworkPropertiesExtractor.JSONMappings
{
    public class FeatureServiceJSONMapping
    {
        public float currentVersion { get; set; }
        public string cimVersion { get; set; }
        public string serviceDescription { get; set; }
        public bool hasVersionedData { get; set; }
        public bool hasArchivedData { get; set; }
        public bool hasBranchVersionedData { get; set; }
        public bool supportsDisconnectedEditing { get; set; }
        public bool supportsDatumTransformation { get; set; }
        public bool supportsReturnServiceEditsOption { get; set; }
        public bool returnServiceEditsHaveSR { get; set; }
        public bool supportsQueryDataElements { get; set; }
        public bool supportsRelationshipsResource { get; set; }
        public bool syncEnabled { get; set; }
        public Extractchangescapabilities extractChangesCapabilities { get; set; }
        public string supportedQueryFormats { get; set; }
        public int maxRecordCount { get; set; }
        public int maxRecordCountFactor { get; set; }
        public string capabilities { get; set; }
        public string description { get; set; }
        public string copyrightText { get; set; }
        public Advancededitingcapabilities advancedEditingCapabilities { get; set; }
        public Spatialreference spatialReference { get; set; }
        public Initialextent initialExtent { get; set; }
        public Fullextent fullExtent { get; set; }
        public bool allowGeometryUpdates { get; set; }
        public bool allowTrueCurvesUpdates { get; set; }
        public bool onlyAllowTrueCurveUpdatesByTrueCurveClients { get; set; }
        public bool supportsApplyEditsWithGlobalIds { get; set; }
        public bool supportsOidReservation { get; set; }
        public bool supportsTrueCurve { get; set; }
        public string units { get; set; }
        public Documentinfo documentInfo { get; set; }
        public bool supportsQueryDomains { get; set; }
        public bool supportsQueryContingentValues { get; set; }
        public Layer[] layers { get; set; }
        public Table[] tables { get; set; }
        public Relationship[] relationships { get; set; }
        public Controllerdatasetlayers controllerDatasetLayers { get; set; }
        public bool supportsDynamicLayers { get; set; }
        public bool enableZDefaults { get; set; }
        public int zDefault { get; set; }
        public bool allowUpdateWithoutMValues { get; set; }
        public Heightmodelinfo heightModelInfo { get; set; }
        public bool supportsVCSProjection { get; set; }
        public Datumtransformation[] datumTransformations { get; set; }
        public int referenceScale { get; set; }
        public string serviceItemId { get; set; }
    }
    public class Extractchangescapabilities
    {
        public bool supportsReturnIdsOnly { get; set; }
        public bool supportsReturnExtentOnly { get; set; }
        public bool supportsReturnAttachments { get; set; }
        public bool supportsLayerQueries { get; set; }
        public bool supportsGeometry { get; set; }
        public bool supportsFeatureReturn { get; set; }
    }

    public class Advancededitingcapabilities
    {
        public bool supportsSplit { get; set; }
        public bool supportsReturnServiceEditsInSourceSR { get; set; }
    }

    public class Spatialreference
    {
        public int wkid { get; set; }
        public int latestWkid { get; set; }
        public int vcsWkid { get; set; }
        public int latestVcsWkid { get; set; }
        public float xyTolerance { get; set; }
        public float zTolerance { get; set; }
        public float mTolerance { get; set; }
        public int falseX { get; set; }
        public int falseY { get; set; }
        public float xyUnits { get; set; }
        public int falseZ { get; set; }
        public float zUnits { get; set; }
        public int falseM { get; set; }
        public int mUnits { get; set; }
    }

    public class Initialextent
    {
        public float xmin { get; set; }
        public float ymin { get; set; }
        public float xmax { get; set; }
        public float ymax { get; set; }
        public Spatialreference1 spatialReference { get; set; }
    }

    public class Spatialreference1
    {
        public int wkid { get; set; }
        public int latestWkid { get; set; }
        public int vcsWkid { get; set; }
        public int latestVcsWkid { get; set; }
        public float xyTolerance { get; set; }
        public float zTolerance { get; set; }
        public float mTolerance { get; set; }
        public int falseX { get; set; }
        public int falseY { get; set; }
        public float xyUnits { get; set; }
        public int falseZ { get; set; }
        public float zUnits { get; set; }
        public int falseM { get; set; }
        public int mUnits { get; set; }
    }

    public class Fullextent
    {
        public float xmin { get; set; }
        public float ymin { get; set; }
        public float xmax { get; set; }
        public float ymax { get; set; }
        public Spatialreference2 spatialReference { get; set; }
    }

    public class Spatialreference2
    {
        public int wkid { get; set; }
        public int latestWkid { get; set; }
        public int vcsWkid { get; set; }
        public int latestVcsWkid { get; set; }
        public float xyTolerance { get; set; }
        public float zTolerance { get; set; }
        public float mTolerance { get; set; }
        public int falseX { get; set; }
        public int falseY { get; set; }
        public float xyUnits { get; set; }
        public int falseZ { get; set; }
        public float zUnits { get; set; }
        public int falseM { get; set; }
        public int mUnits { get; set; }
    }

    public class Documentinfo
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string Comments { get; set; }
        public string Subject { get; set; }
        public string Category { get; set; }
        public string Keywords { get; set; }
    }

    public class Controllerdatasetlayers
    {
        public int utilityNetworkLayerId { get; set; }
    }

    public class Heightmodelinfo
    {
        public string heightModel { get; set; }
        public string vertCRS { get; set; }
        public string heightUnit { get; set; }
    }

    public class Layer
    {
        public int id { get; set; }
        public string name { get; set; }
        public int parentLayerId { get; set; }
        public bool defaultVisibility { get; set; }
        public int[] subLayerIds { get; set; }
        public int minScale { get; set; }
        public int maxScale { get; set; }
        public string type { get; set; }
        public string geometryType { get; set; }
    }

    public class Table
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class Relationship
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class Datumtransformation
    {
        public Geotransform[] geoTransforms { get; set; }
    }

    public class Geotransform
    {
        public int wkid { get; set; }
        public int latestWkid { get; set; }
        public bool transformForward { get; set; }
        public string name { get; set; }
    }
}