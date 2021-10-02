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

// Esri Community blog on creating C# classes needed to deseriable JSON.  
//  https://community.esri.com/t5/arcgis-pro-sdk-questions/extracting-coordinates-from-json-results/td-p/1014686
//1.  Copy your [valid] JSON string to the Clipboard
//2.  In Visual Studio:
//3.  Create a new cs class file in your project
//4.  Delete the create class stub in the new class file and use Edit -> Paste Special -> Paste as JSON Classes
//5.  This will create the classes you need to deserialize the JSON. 
//6.  You need to add the Newton.JSON nuget to your project (VS will suggest that to you once you add the code below)
//7.  Now you can use this code to deserialize the JSON string


namespace UtilityNetworkPropertiesExtractor.JSONMappings
{
    public class ArcRestError
    {
        public Error error { get; set; }
    }

    public class Error
    {
        public int code { get; set; }
        public string message { get; set; }
        public object[] details { get; set; }
    }
}