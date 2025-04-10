using System;
using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

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
        
        public async Task<bool> BuildProject(string newVersion)
        {
            try
            {
                // Update version number
                _project.CurrentVersion = newVersion;
                
                // Execute build command
                if (!string.IsNullOrEmpty(_project.BuildCommand))
                {
                    ProcessStartInfo processInfo = new ProcessStartInfo
                    {
                        FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
                        Arguments = OperatingSystem.IsWindows() ? $"/c {_project.BuildCommand}" : $"-c \"{_project.BuildCommand}\"",
                        WorkingDirectory = _project.SourcePath,
                        CreateNoWindow = false,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    
                    using (Process process = Process.Start(processInfo))
                    {
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                            return process.ExitCode == 0;
                        }
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Build error: {ex.Message}");
                return false;
            }
        }
        
        public bool DeployToDevelopment()
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.DevelopmentBasePath))
                    return false;
                    
                string targetDir = Path.Combine(_settings.DevelopmentBasePath, _project.Name);
                
                // Ensure target directory exists
                Directory.CreateDirectory(targetDir);
                
                // Copy files from build output to development
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
                    
                string sourceDir = Path.Combine(_settings.DevelopmentBasePath, _project.Name);
                string targetDir = Path.Combine(_settings.TestBasePath, _project.Name);
                
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
                    
                string sourceDir = Path.Combine(_settings.TestBasePath, _project.Name);
                string targetDir = Path.Combine(_settings.ProductionBasePath, _project.Name);
                
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