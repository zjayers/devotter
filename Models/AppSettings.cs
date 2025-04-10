using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace devotter.Models
{
    public class AppSettings
    {
        public List<Project> Projects { get; set; } = new List<Project>();
        
        public string DevelopmentBasePath { get; set; } = "";
        public string TestBasePath { get; set; } = "";
        public string ProductionBasePath { get; set; } = "";
        
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