# utility-network-properties-extractor
Contains the source code for the 'Utility Network Property Extractor' ArcGIS Pro Add-in which creates individual CSV files for Utility Network, Geodatabase and Map properties.

<!-- TODO: Fill this section below with metadata about this sample-->
```
Language:              C#
Subject:               Utility Network
Organization:          Esri, http://www.esri.com
Date:                  5/01/2021
ArcGIS Pro:            2.7
Visual Studio:         2019
.NET Target Framework: .NET Framework 4.8
```


## Sceenshot
 ![Screenshot](Screenshots/Toolbar.PNG) 

- ArcGIS Pro Add-in contains:
      
### Buttons that extract Utility Network, Geodatabase and Map information to individual CSV files
* **Utility Network**:   Asset Groups, Domain Networks, Network Rules, Network Categories, Network Attributes, Network Diagrams, Terminal Configuration, Trace Configuration
* **Geodatabase**:  Domain Values, Domain Assignments, Orphan Domains, Fields, Versioning Info, Attribute Rules, Contingent Values
* **Map**:  Layer Info, Map Field Settings
                        
### Efficiency buttons to help with map configuration.

#### 1. Import Map Field Settings
* Using a generated CSV from “Map Field Settings” extraction, field settings can be modified and applied to the map.
* Field Settings include:  Visibility, Read-Only, Highlighted and Field Alias

#### 2. Set Display Field Expressions
* For Utility Network Layers, set the primary display field to an Arcade Expression
* Domain/Structure Layers:  Asset Group, Asset Type and Objectid
* Subnetline Layer:  Subnetwork Name

#### 3. Set Containment Display Filters
* For Utility Network Layers with an assocationstatus field, sets the Display Filter used by Containment
* Sql:  associationstatus not in (4,5,6,12,13,14,36,37,38,44,45,46)

              
**The source code was written against Pro SDK 2.7**. If using an earlier release, you may have to comment out some sections of code that were introduced at Pro SDK 2.7.

## Directions

1.  Download the source code
2.  In Visual studio compile the solution
3.  Start up ArcGIS Pro
4.  Open a map that contains the Utility Network
5.  Generate a report by clicking on the appropriate button  


## ArcGIS Pro SDK Resources

[ArcGIS Pro SDK for Microsoft .NET](https://pro.arcgis.com/en/pro-app/latest/sdk/)

[ProConcepts Migrating to ArcGIS Pro](https://github.com/esri/arcgis-pro-sdk/wiki/ProConcepts-Migrating-to-ArcGIS-Pro)

[Pro SDK Community Samples](https://github.com/esri/arcgis-pro-sdk-community-samples)


## Issues

Find a bug or want to request a new feature?  Please let us know by submitting an issue.

## Contributing

Esri welcomes contributions from anyone and everyone. Please see our [guidelines for contributing](https://github.com/esri/contributing).

## Licensing
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

A copy of the license is available in the repository's [license.txt]( https://raw.github.com/Esri/quickstart-map-js/master/license.txt) file.
