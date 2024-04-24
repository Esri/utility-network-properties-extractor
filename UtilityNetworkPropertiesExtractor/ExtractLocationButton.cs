﻿/*
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
using ArcGIS.Desktop.Framework.Contracts;
using System.Windows;

namespace UtilityNetworkPropertiesExtractor
{
    internal class ExtractLocationButton : Button
    {
        protected override void OnClick()
        {
            Common.CreateOutputDirectory();
            string mesg = "Extract Loction for this map is: \n\n" + Common.ExtractFilePath;
            Clipboard.SetText(Common.ExtractFilePath);
            MessageBox.Show(mesg + "\n\nThe path to the folder has been copied to the clipboard", "Extract Location");
        }
    }
}