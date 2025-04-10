using System;
using System.Collections.Generic;

namespace devotter.Models
{
    public class Project
    {
        public string Name { get; set; } = "";
        public string CurrentVersion { get; set; } = "1.0.0";
        public string SourcePath { get; set; } = "";
        public string BuildCommand { get; set; } = "";
        
        public bool DeployedToDevelopment { get; set; } = false;
        public bool DeployedToTest { get; set; } = false;
        public bool DeployedToProduction { get; set; } = false;
        
        public List<ConfigSetting> ConfigSettings { get; set; } = new List<ConfigSetting>();
    }
}