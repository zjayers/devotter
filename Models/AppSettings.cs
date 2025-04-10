using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace devotter.Models
{
    public class AppSettings
    {
        public string ProjectName { get; set; } = "MyApp";
        public string CurrentVersion { get; set; } = "1.0.0";
        public string SourcePath { get; set; } = "";
        public string BuildCommand { get; set; } = "";
        
        public string DevelopmentPath { get; set; } = "";
        public string TestPath { get; set; } = "";
        public string ProductionPath { get; set; } = "";
        
        public List<ConfigSetting> ConfigSettings { get; set; } = new List<ConfigSetting>();
        
        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "devotter", 
            "settings.json");
            
        public static AppSettings Load()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    return new AppSettings();
                }
            }
            
            return new AppSettings();
        }
        
        public void Save()
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(SettingsPath) ?? "";
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex) 
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
    
    public class ConfigSetting
    {
        public string KeyName { get; set; } = "";
        public string DevelopmentValue { get; set; } = "";
        public string TestValue { get; set; } = "";
        public string ProductionValue { get; set; } = "";
    }
}