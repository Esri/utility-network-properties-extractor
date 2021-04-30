# utility-network-properties-extractor

## ArcGIS Pro 2.7 SDK for Microsoft .NET Framework

- ArcGIS Pro Add-in that contains:
      
      1. Buttons that extract Utility Network, Map and Geodatabase information to CSV files
            - Utility Network:   Asset Groups, Domain Networks, Network Rules, Network Categories, Network Attributes, Network Diagrams, Terminal Configuration, Trace Configuration
            - Geodatabase:  Domain Values, Domain Assignments, Orphan Domains, Fields, Versioning Info, Attribute Rules, Contingent Values
            - Map:  Layer Info, Map Field Settings
                        
      2. Efficiency buttons to help with map configuration.
            - Import Map Field Settings:  Applies these 4 settings, Visibility, Read-only, Highlighted, Field Alias from a CSV file
            - Set Display Field Settings:  Sets the Display Field for all Utility Network Layers to a hard-coded Arcade Expressions (based on layer)
            - Set Containment Display Filters:  Sets the Display Filters for Utility Network Containment
