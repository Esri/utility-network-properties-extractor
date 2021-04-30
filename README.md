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


## Overview
- ArcGIS Pro Add-in that contains:
      
      1. Buttons that extract Utility Network, Geodatabase and Map information to individual CSV files
            - Utility Network:   Asset Groups, Domain Networks, Network Rules, Network Categories, Network Attributes, Network Diagrams, Terminal Configuration, Trace Configuration
            - Geodatabase:  Domain Values, Domain Assignments, Orphan Domains, Fields, Versioning Info, Attribute Rules, Contingent Values
            - Map:  Layer Info, Map Field Settings
                        
      2. Efficiency buttons to help with map configuration.
            - Import Map Field Settings:  Applies these 4 settings, Visibility, Read-only, Highlighted, Field Alias from a CSV file
            - Set Display Field Settings:  Sets the Display Field for all Utility Network Layers to a hard-coded Arcade Expressions (based on layer)
            - Set Containment Display Filters:  Sets the Display Filters for Utility Network Containment

**The source code was written against Pro SDK 2.7**. If using an earlier release, you may have to comment out some sections of code that were introduced at Pro SDK 2.7.


## Instructions

1. Fork and then clone the repo. 
2. Run and try the samples.

## Requirements

* 
* 

## Resources

## Issues

Find a bug or want to request a new feature?  Please let us know by submitting an issue.

## Contributing

Esri welcomes contributions from anyone and everyone. Please see our [guidelines for contributing](https://github.com/esri/contributing).

## Licensing
Copyright 2016 Esri

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
