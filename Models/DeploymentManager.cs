using System;
using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;

namespace devotter.Models
{
    public class DeploymentManager
    {
        private readonly Project _project;
        private readonly AppSettings _settings;
        
        public DeploymentManager(Project project, AppSettings settings)
        {
            _project = project;
            _settings = settings;
        }
        
        public bool CheckIfDeployedToDevelopment()
        {
            if (string.IsNullOrEmpty(_settings.DevelopmentBasePath))
                return false;
                
            string folderName = GetVersionFolderName(_project.CurrentVersion);
            string deploymentDir = Path.Combine(_settings.DevelopmentBasePath, folderName);
            return Directory.Exists(deploymentDir);
        }
        
        public bool CheckIfDeployedToTest()
        {
            if (string.IsNullOrEmpty(_settings.TestBasePath))
                return false;
                
            string folderName = GetVersionFolderName(_project.CurrentVersion);
            string testDir = Path.Combine(_settings.TestBasePath, folderName);
            return Directory.Exists(testDir);
        }
        
        public Task<bool> BuildProject(string newVersion)
        {
            try
            {
                // Update version number
                _project.CurrentVersion = newVersion;
                
                // No actual build process, just return success
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Build error: {ex.Message}");
                return Task.FromResult(false);
            }
        }
        
        private string GetVersionFolderName(string version)
        {
            // Handle invalid characters in project name
            string safeName = string.Join("_", _project.Name.Split(Path.GetInvalidFileNameChars()));
            
            // Convert version 1.0.1 to v1_0_1 format
            string formattedVersion = "v" + version.Replace(".", "_");
            
            // Return sanitized folder name
            return $"{safeName}_{formattedVersion}";
        }
        
        public bool RemoveFromDevelopment()
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.DevelopmentBasePath))
                    return false;
                
                string folderName = GetVersionFolderName(_project.CurrentVersion);
                string deploymentDir = Path.Combine(_settings.DevelopmentBasePath, folderName);
                
                if (Directory.Exists(deploymentDir))
                {
                    Directory.Delete(deploymentDir, true);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Development removal error: {ex.Message}");
                return false;
            }
        }
        
        public bool RemoveFromTest()
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.TestBasePath))
                    return false;
                
                string folderName = GetVersionFolderName(_project.CurrentVersion);
                string testDir = Path.Combine(_settings.TestBasePath, folderName);
                
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test removal error: {ex.Message}");
                return false;
            }
        }
        
        public bool RemoveFromProduction()
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.ProductionBasePath))
                    return false;
                
                string folderName = GetVersionFolderName(_project.CurrentVersion);
                string prodDir = Path.Combine(_settings.ProductionBasePath, folderName);
                
                if (Directory.Exists(prodDir))
                {
                    Directory.Delete(prodDir, true);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Production removal error: {ex.Message}");
                return false;
            }
        }
        
        public bool RemoveFromAllEnvironments()
        {
            bool devRemoved = RemoveFromDevelopment();
            bool testRemoved = RemoveFromTest();
            bool prodRemoved = RemoveFromProduction();
            
            // Return true if any environment was cleaned up
            return devRemoved || testRemoved || prodRemoved;
        }
        
        public bool DeployToDevelopment()
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.DevelopmentBasePath))
                    return false;
                
                string folderName = GetVersionFolderName(_project.CurrentVersion);    
                string targetDir = Path.Combine(_settings.DevelopmentBasePath, folderName);
                
                // Ensure target directory exists
                Directory.CreateDirectory(targetDir);
                
                // Copy files from source to development
                CopyDirectory(_project.SourcePath, targetDir);
                
                // Update config files
                UpdateConfigFiles(targetDir, Environment.Development);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Deployment error: {ex.Message}");
                return false;
            }
        }
        
        public bool DeployToTest()
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.TestBasePath) || string.IsNullOrEmpty(_settings.DevelopmentBasePath))
                    return false;
                
                string folderName = GetVersionFolderName(_project.CurrentVersion);
                string sourceDir = Path.Combine(_settings.DevelopmentBasePath, folderName);
                string targetDir = Path.Combine(_settings.TestBasePath, folderName);
                
                // Ensure source exists and target directory exists
                if (!Directory.Exists(sourceDir))
                    return false;
                    
                Directory.CreateDirectory(targetDir);
                
                // Copy files from development to test
                CopyDirectory(sourceDir, targetDir);
                
                // Update config files
                UpdateConfigFiles(targetDir, Environment.Test);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test deployment error: {ex.Message}");
                return false;
            }
        }
        
        public bool DeployToProduction()
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.ProductionBasePath) || string.IsNullOrEmpty(_settings.TestBasePath))
                    return false;
                
                string folderName = GetVersionFolderName(_project.CurrentVersion);
                string sourceDir = Path.Combine(_settings.TestBasePath, folderName);
                string targetDir = Path.Combine(_settings.ProductionBasePath, folderName);
                
                // Ensure source exists and target directory exists
                if (!Directory.Exists(sourceDir))
                    return false;
                    
                Directory.CreateDirectory(targetDir);
                
                // Copy files from test to production
                CopyDirectory(sourceDir, targetDir);
                
                // Update config files
                UpdateConfigFiles(targetDir, Environment.Production);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Production deployment error: {ex.Message}");
                return false;
            }
        }
        
        private void CopyDirectory(string sourceDir, string targetDir)
        {
            // Ensure target directory exists
            Directory.CreateDirectory(targetDir);
            
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string newDir = Path.Combine(targetDir, Path.GetRelativePath(sourceDir, dirPath));
                Directory.CreateDirectory(newDir);
            }

            foreach (string filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string newFilePath = Path.Combine(targetDir, Path.GetRelativePath(sourceDir, filePath));
                Directory.CreateDirectory(Path.GetDirectoryName(newFilePath) ?? targetDir);
                File.Copy(filePath, newFilePath, true);
            }
        }
        
        private void UpdateConfigFiles(string directory, Environment env)
        {
            try
            {
                // Find all config files
                string[] configFiles = Directory.GetFiles(directory, "*.config", SearchOption.AllDirectories);
                
                foreach (string configFile in configFiles)
                {
                    UpdateConfigFile(configFile, env);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Config update error: {ex.Message}");
            }
        }
        
        private void UpdateConfigFile(string configFilePath, Environment env)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(configFilePath);
                
                // Find appSettings section
                XmlNode? appSettingsNode = doc.SelectSingleNode("//appSettings");
                
                if (appSettingsNode != null)
                {
                    foreach (ConfigSetting setting in _project.ConfigSettings)
                    {
                        // Find the setting by key - use relative XPath to avoid selecting wrong nodes
                        XmlNode? settingNode = appSettingsNode.SelectSingleNode($"./add[@key='{setting.KeyName}']");
                        
                        string newValue = env switch
                        {
                            Environment.Development => setting.DevelopmentValue,
                            Environment.Test => setting.TestValue,
                            Environment.Production => setting.ProductionValue,
                            _ => setting.DevelopmentValue
                        };
                        
                        if (settingNode != null)
                        {
                            // Update existing setting
                            XmlAttribute? valueAttr = settingNode.Attributes?["value"];
                            if (valueAttr != null)
                            {
                                valueAttr.Value = newValue;
                            }
                        }
                        else
                        {
                            // Add new setting
                            XmlElement newElement = doc.CreateElement("add");
                            
                            XmlAttribute keyAttr = doc.CreateAttribute("key");
                            keyAttr.Value = setting.KeyName;
                            newElement.Attributes.Append(keyAttr);
                            
                            XmlAttribute valueAttr = doc.CreateAttribute("value");
                            valueAttr.Value = newValue;
                            newElement.Attributes.Append(valueAttr);
                            
                            appSettingsNode.AppendChild(newElement);
                        }
                    }
                    
                    doc.Save(configFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Config file update error: {ex.Message}");
            }
        }
    }
    
    public enum Environment
    {
        Development,
        Test,
        Production
    }
}