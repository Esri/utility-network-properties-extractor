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
using ArcGIS.Desktop.Framework.Contracts;
using System;
using System.Diagnostics;
using System.Management;
using System.Reflection;
using System.Windows;

namespace UtilityNetworkPropertiesExtractor
{
    internal class MachineInfoButton : Button
    {
        protected override void OnClick()
        {
            string machineName = "Machine: " + Environment.MachineName;

            //var plugin = FrameworkApplication.GetPlugInWrapper("UtilityNetworkPropertiesExtractor_MachineNameButton");
            //plugin.Caption = machineName;

            Assembly assembly = Assembly.GetEntryAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            string proVersion = $"ArcGIS Pro {fvi.ProductMajorPart}.{fvi.ProductMinorPart}.{fvi.ProductBuildPart}";
            string operatingSystem = "Operating System: " + Environment.OSVersion.VersionString;
            string cpu = "CPU: " + CpuName(); 
            string coreCount = "Number Of Cores: " + NumberOfCores();
            string processorCount = "Logical Processor Count: " + Environment.ProcessorCount;
            string memory = "Total Physical Memory: " + TotalPhysicalMemory();
            string videoControllerInfo = "\nVideo Controllers:\n" + VideoControllers();

            string mesg = machineName + "\n" +
                          proVersion + "\n" +
                          operatingSystem + "\n" +
                          cpu + "\n" +
                          coreCount + "\n" +
                          processorCount + "\n" +
                          memory + "\n" +
                          videoControllerInfo;

            Clipboard.SetText(mesg);
            MessageBox.Show(mesg + "\n\nThis information has been copied to the clipboard!", machineName);
        }

        private static int NumberOfCores()
        {
            int coreCount = 0;
            ObjectQuery wql = new ObjectQuery("SELECT * FROM Win32_Processor");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(wql);
            ManagementObjectCollection results = searcher.Get();

            foreach (ManagementObject result in results)
                coreCount = Convert.ToInt32(result["NumberOfCores"]);

            return coreCount;
        }

        private static string TotalPhysicalMemory()
        {
            ObjectQuery wql = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(wql);
            ManagementObjectCollection results = searcher.Get();

            double res;
            double fres = 0;

            foreach (ManagementObject result in results)
            {
                res = Convert.ToDouble(result["TotalVisibleMemorySize"]);
                fres = Math.Round((res / (1024 * 1024)), 2);
            }
            return fres.ToString() + " GB";
        }

        private static string CpuName()
        {
            string cpuName = string.Empty;
            using (ManagementObject Mo = new ManagementObject("Win32_Processor.DeviceID='CPU0'"))
            {
                cpuName = (string)(Mo["Name"]);
            }
            return cpuName;
        }

        private static string VideoControllers()
        {
            string retVal = string.Empty;
            ManagementObjectSearcher myVideoObject = new ManagementObjectSearcher("select * from Win32_VideoController");

            foreach (ManagementObject obj in myVideoObject.Get())
            {
                retVal += "\n  DeviceID  -  " + obj["DeviceID"] + "\n";
                retVal += "    Name  -  " + obj["Name"] + "\n";
                                
                //Console.WriteLine("Name  -  " + obj["Name"];
                //Console.WriteLine("Status  -  " + obj["Status"];
                //Console.WriteLine("Caption  -  " + obj["Caption"];
                //Console.WriteLine("DeviceID  -  " + obj["DeviceID"];
                //Console.WriteLine("AdapterRAM  -  " + obj["AdapterRAM"];
                //Console.WriteLine("AdapterDACType  -  " + obj["AdapterDACType"];
                //Console.WriteLine("Monochrome  -  " + obj["Monochrome"];
                //Console.WriteLine("InstalledDisplayDrivers  -  " + obj["InstalledDisplayDrivers"];
                //Console.WriteLine("DriverVersion  -  " + obj["DriverVersion"];
                //Console.WriteLine("VideoProcessor  -  " + obj["VideoProcessor"];
                //Console.WriteLine("VideoArchitecture  -  " + obj["VideoArchitecture"];
                //Console.WriteLine("VideoMemoryType  -  " + obj["VideoMemoryType"];
            }
            return retVal;
        }
    }
}