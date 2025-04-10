using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading;

namespace devotter.Models
{
    public class DeploymentManager : IDisposable
    {
        private readonly Project _project;
        private readonly AppSettings _settings;
        private readonly SemaphoreSlim _projectLock = new SemaphoreSlim(1, 1);
        private bool _isDisposed;
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;
            
            if (disposing)
            {
                try
                {
                    _projectLock.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing DeploymentManager: {ex.Message}");
                }
            }
            
            _isDisposed = true;
        }
        
        ~DeploymentManager()
        {
            Dispose(false);
        }
        
        public DeploymentManager(Project project, AppSettings settings)
        {
            _project = project;
            _settings = settings;
        }
        
        public async Task<bool> CheckIfDeployedToDevelopment()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(DeploymentManager));
                
            if (string.IsNullOrEmpty(_settings.DevelopmentBasePath))
                return false;
                
            string folderName = GetVersionFolderName(_project.CurrentVersion);
            string deploymentDir = Path.Combine(_settings.DevelopmentBasePath, folderName);
            
            bool exists = Directory.Exists(deploymentDir);
            
            // Update the project status (thread-safe)
            await _projectLock.WaitAsync();
            try
            {
                _project.DeployedToDevelopment = exists;
            }
            finally
            {
                _projectLock.Release();
            }
            
            return exists;
        }
        
        public async Task<bool> CheckIfDeployedToTest()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(DeploymentManager));
                
            if (string.IsNullOrEmpty(_settings.TestBasePath))
                return false;
                
            string folderName = GetVersionFolderName(_project.CurrentVersion);
            string testDir = Path.Combine(_settings.TestBasePath, folderName);
            
            bool exists = Directory.Exists(testDir);
            
            // Update the project status (thread-safe)
            await _projectLock.WaitAsync();
            try
            {
                _project.DeployedToTest = exists;
            }
            finally
            {
                _projectLock.Release();
            }
            
            return exists;
        }
        
        public async Task<bool> CheckIfDeployedToProduction()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(DeploymentManager));
                
            if (string.IsNullOrEmpty(_settings.ProductionBasePath))
                return false;
                
            string folderName = GetVersionFolderName(_project.CurrentVersion);
            string prodDir = Path.Combine(_settings.ProductionBasePath, folderName);
            
            bool exists = Directory.Exists(prodDir);
            
            // Update the project status (thread-safe)
            await _projectLock.WaitAsync();
            try
            {
                _project.DeployedToProduction = exists;
            }
            finally
            {
                _projectLock.Release();
            }
            
            return exists;
        }
        
        public async Task UpdateAllDeploymentStatus()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(DeploymentManager));
                
            // Check and update all deployment statuses
            await CheckIfDeployedToDevelopment();
            await CheckIfDeployedToTest();
            await CheckIfDeployedToProduction();
        }
        
        public async Task<bool> UpdateProjectVersion(string newVersion)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(DeploymentManager));
                
            await _projectLock.WaitAsync();
            try
            {
                if (string.IsNullOrEmpty(_project.ProjectFilePath) || !File.Exists(_project.ProjectFilePath))
                {
                    // Don't use Logger here to avoid circular dependency when initializing
                    System.Diagnostics.Debug.WriteLine($"Project file not found: {_project.ProjectFilePath}");
                    return false;
                }
                
                // Load the project file as XML
                XmlDocument doc = new XmlDocument();
                doc.Load(_project.ProjectFilePath);
                
                // Store whether we updated anything
                bool versionUpdated = false;
                
                // Try to update version in various locations
                List<string> versionElements = new List<string>
                {
                    "//Version", "//AssemblyVersion", "//FileVersion", "//PackageVersion"
                };
                
                foreach (string xpath in versionElements)
                {
                    XmlNode? versionNode = doc.SelectSingleNode(xpath);
                    if (versionNode != null)
                    {
                        string oldVersion = versionNode.InnerText;
                        versionNode.InnerText = newVersion;
                        versionUpdated = true;
                        Logger.Instance.LogDebug($"Updated version from {oldVersion} to {newVersion} in node {xpath}");
                    }
                }
                
                // If we didn't find any version nodes, try to create one in PropertyGroup
                if (!versionUpdated)
                {
                    XmlNode? propertyGroup = doc.SelectSingleNode("//PropertyGroup");
                    if (propertyGroup != null)
                    {
                        // Create Version element
                        XmlElement versionElement = doc.CreateElement("Version");
                        versionElement.InnerText = newVersion;
                        propertyGroup.AppendChild(versionElement);
                        versionUpdated = true;
                        Logger.Instance.LogDebug($"Added Version element with value {newVersion}");
                    }
                }
                
                if (versionUpdated)
                {
                    // Save the updated project file
                    doc.Save(_project.ProjectFilePath);
                    
                    // Update current version in the project object
                    _project.CurrentVersion = newVersion;
                    Logger.Instance.LogInfo($"Updated project file version to {newVersion}");
                    return true;
                }
                
                // If we couldn't update the file, just update the in-memory version
                _project.CurrentVersion = newVersion;
                Logger.Instance.LogWarning($"Could not update version in project file, but set in-memory version to {newVersion}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating project version: {ex.Message}");
                Logger.Instance.LogError($"Error updating project version: {ex.Message}");
                
                try
                {
                    // Still update the in-memory version
                    _project.CurrentVersion = newVersion;
                }
                catch (Exception versionEx)
                {
                    // Log but don't throw as this is a best-effort update
                    Logger.Instance.LogError($"Error updating in-memory version: {versionEx.Message}");
                }
                return false;
            }
            finally
            {
                _projectLock.Release();
            }
        }
        
        public async Task<bool> BuildProject(string newVersion)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(DeploymentManager));
                
            try
            {
                // Update version in project file first
                await UpdateProjectVersion(newVersion);
                
                // Skip build if no command is specified
                if (string.IsNullOrWhiteSpace(_project.BuildCommand))
                {
                    Logger.Instance.LogInfo("No build command specified, skipping build step.");
                    return true;
                }
                
                // Create process to run build command
                using (var process = new Process())
                {
                    // Configure process info
                    process.StartInfo.FileName = "cmd.exe";
                    
                    // Use bash on macOS/Linux
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
                        RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        process.StartInfo.FileName = "/bin/bash";
                        process.StartInfo.Arguments = $"-c \"{_project.BuildCommand}\"";
                    }
                    else
                    {
                        // Windows
                        process.StartInfo.Arguments = $"/c {_project.BuildCommand}";
                    }
                    
                    // Set working directory to project source path
                    process.StartInfo.WorkingDirectory = _project.SourcePath;
                    
                    // Configure process to capture output
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    
                    // Start the process
                    process.Start();
                    
                    // Create tasks to read output and error streams asynchronously
                    Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> errorTask = process.StandardError.ReadToEndAsync();
                    
                    using (var timeoutCts = new CancellationTokenSource(60000)) // 60 second timeout
                    {
                        try
                        {
                            // Create task for process exit
                            var processTask = Task.Run(() => process.WaitForExit());
                            
                            // Wait for process to exit with a timeout
                            if (await Task.WhenAny(processTask, Task.Delay(60000, timeoutCts.Token)) != processTask)
                            {
                                // Process timed out
                                try 
                                { 
                                    if (!process.HasExited)
                                        process.Kill(); 
                                } 
                                catch { /* Ignore if process already exited */ }
                                
                                throw new TimeoutException("Build process timed out after 60 seconds");
                            }
                            
                            // Cancel the timeout task
                            timeoutCts.Cancel();
                            
                            // Wait for output and error to be fully read with timeout
                            await Task.WhenAny(
                                Task.WhenAll(outputTask, errorTask),
                                Task.Delay(5000)
                            );
                            
                            string output = outputTask.IsCompleted ? await outputTask : "Output collection timed out";
                            string error = errorTask.IsCompleted ? await errorTask : "Error collection timed out";
                            
                            // Check exit code
                            if (process.ExitCode != 0)
                            {
                                Logger.Instance.LogError($"Build command failed with exit code {process.ExitCode}");
                                Logger.Instance.LogError($"Error output: {error}");
                                throw new Exception($"Build failed with exit code {process.ExitCode}. Error: {error}");
                            }
                            
                            Logger.Instance.LogInfo($"Build completed successfully");
                            Logger.Instance.LogDebug($"Build output: {output}");
                            return true;
                        }
                        catch (OperationCanceledException)
                        {
                            // Handle cancellation
                            Logger.Instance.LogWarning("Build operation was canceled");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Build error: {ex.Message}");
                throw new Exception($"Build error: {ex.Message}", ex);
            }
        }
        
        private string GetVersionFolderName(string version)
        {
            // Ensure project name is not empty
            string projectName = !string.IsNullOrWhiteSpace(_project.Name) ? _project.Name : "Project";
            
            // Handle invalid characters in project name
            string safeName = string.Join("_", projectName.Split(Path.GetInvalidFileNameChars()));
            
            // Ensure we have a valid version string
            string versionToFormat = version;
            if (string.IsNullOrWhiteSpace(versionToFormat))
            {
                Logger.Instance.LogWarning("Empty version string provided to GetVersionFolderName, using default 1.0.0");
                versionToFormat = "1.0.0";
            }
            
            // Validate version format (should be dot-separated numbers)
            if (!System.Text.RegularExpressions.Regex.IsMatch(versionToFormat, @"^\d+(\.\d+)*$"))
            {
                Logger.Instance.LogWarning($"Invalid version format: {versionToFormat}, using default 1.0.0");
                versionToFormat = "1.0.0";
            }
            
            // Convert version 1.0.1 to v1_0_1 format
            string formattedVersion = "v" + versionToFormat.Replace(".", "_");
            
            // Ensure folder name doesn't exceed maximum path length
            string folderName = $"{safeName}_{formattedVersion}";
            if (folderName.Length > 100) // Arbitrary limit to prevent extremely long folder names
            {
                // Truncate the project name if needed
                int maxProjectNameLength = 90 - formattedVersion.Length;
                if (maxProjectNameLength < 10) maxProjectNameLength = 10; // Minimum length
                
                safeName = safeName.Substring(0, Math.Min(safeName.Length, maxProjectNameLength));
                folderName = $"{safeName}_{formattedVersion}";
                
                Logger.Instance.LogWarning($"Folder name was too long and has been truncated to: {folderName}");
            }
            
            // Return sanitized folder name
            return folderName;
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
                System.Diagnostics.Debug.WriteLine($"Development removal error: {ex.Message}");
                throw new IOException($"Failed to remove {_project.Name} from development: {ex.Message}", ex);
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
                System.Diagnostics.Debug.WriteLine($"Test removal error: {ex.Message}");
                throw new IOException($"Failed to remove {_project.Name} from test: {ex.Message}", ex);
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
                System.Diagnostics.Debug.WriteLine($"Production removal error: {ex.Message}");
                throw new IOException($"Failed to remove {_project.Name} from production: {ex.Message}", ex);
            }
        }
        
        public bool RemoveFromAllEnvironments()
        {
            bool devRemoved = false;
            bool testRemoved = false;
            bool prodRemoved = false;
            List<string> errors = new List<string>();
            
            try
            {
                prodRemoved = RemoveFromProduction();
            }
            catch (Exception ex)
            {
                errors.Add($"Production: {ex.Message}");
            }
            
            try
            {
                testRemoved = RemoveFromTest();
            }
            catch (Exception ex)
            {
                errors.Add($"Test: {ex.Message}");
            }
            
            try
            {
                devRemoved = RemoveFromDevelopment();
            }
            catch (Exception ex)
            {
                errors.Add($"Development: {ex.Message}");
            }
            
            // If we have errors but some removals succeeded, report the partial success
            if (errors.Count > 0 && (devRemoved || testRemoved || prodRemoved))
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Some environments were removed, but errors occurred: {string.Join("; ", errors)}");
            }
            // If only errors and no success, throw an exception with all error details
            else if (errors.Count > 0)
            {
                throw new AggregateException($"Failed to remove from any environment: {string.Join("; ", errors)}");
            }
            
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
                System.Diagnostics.Debug.WriteLine($"Deployment error: {ex.Message}");
                throw new Exception($"Failed to deploy {_project.Name} to development: {ex.Message}", ex);
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
                System.Diagnostics.Debug.WriteLine($"Test deployment error: {ex.Message}");
                throw new Exception($"Failed to deploy {_project.Name} to test: {ex.Message}", ex);
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
                System.Diagnostics.Debug.WriteLine($"Production deployment error: {ex.Message}");
                throw new Exception($"Failed to deploy {_project.Name} to production: {ex.Message}", ex);
            }
        }
        
        private void CopyDirectory(string sourceDir, string targetDir)
        {
            try
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
                    
                    // Use using statement to properly dispose file handles
                    using (FileStream sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    using (FileStream destStream = new FileStream(newFilePath, FileMode.Create, FileAccess.Write))
                    {
                        sourceStream.CopyTo(destStream);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Error copying directory: {ex.Message}", ex);
            }
        }
        
        private void UpdateConfigFiles(string directory, Environment env)
        {
            try
            {
                List<string> updatedFiles = new List<string>();
                List<string> errorFiles = new List<string>();
                
                // Find XML config files (.config files)
                string[] xmlConfigFiles = Directory.GetFiles(directory, "*.config", SearchOption.AllDirectories);
                
                foreach (string configFile in xmlConfigFiles)
                {
                    try
                    {
                        UpdateXmlConfigFile(configFile, env);
                        updatedFiles.Add(Path.GetFileName(configFile));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating XML config file {configFile}: {ex.Message}");
                        errorFiles.Add(Path.GetFileName(configFile));
                    }
                }
                
                // Find JSON config files (appsettings.json)
                string[] jsonConfigFiles = Directory.GetFiles(directory, "appsettings*.json", SearchOption.AllDirectories);
                
                foreach (string configFile in jsonConfigFiles)
                {
                    try 
                    {
                        UpdateJsonConfigFile(configFile, env);
                        updatedFiles.Add(Path.GetFileName(configFile));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating JSON config file {configFile}: {ex.Message}");
                        errorFiles.Add(Path.GetFileName(configFile));
                    }
                }
                
                if (updatedFiles.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Updated configuration files: {string.Join(", ", updatedFiles)}");
                }
                
                if (errorFiles.Count > 0)
                {
                    throw new Exception($"Failed to update some configuration files: {string.Join(", ", errorFiles)}");
                }
            }
            catch (Exception ex)
            {
                // Use System.Diagnostics.Debug to log error before rethrowing
                System.Diagnostics.Debug.WriteLine($"Config update error: {ex.Message}");
                throw new Exception($"Error updating configuration files: {ex.Message}", ex);
            }
        }
        
        private void UpdateXmlConfigFile(string configFilePath, Environment env)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(configFilePath);
            
            // Find appSettings section
            XmlNode? appSettingsNode = doc.SelectSingleNode("//appSettings");
            
            if (appSettingsNode != null)
            {
                bool settingsUpdated = false;
                
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
                            string oldValue = valueAttr.Value ?? "";
                            valueAttr.Value = newValue;
                            settingsUpdated = true;
                            Logger.Instance.LogDebug($"Updated config key '{setting.KeyName}' from '{oldValue}' to '{newValue}'");
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
                        settingsUpdated = true;
                        Logger.Instance.LogDebug($"Added config key '{setting.KeyName}' with value '{newValue}'");
                    }
                }
                
                if (settingsUpdated)
                {
                    doc.Save(configFilePath);
                    Logger.Instance.LogDebug($"Saved updated XML config file: {configFilePath}");
                }
                else
                {
                    Logger.Instance.LogDebug($"No changes made to XML config file: {configFilePath}");
                }
            }
            else
            {
                throw new InvalidOperationException($"No appSettings section found in XML config file: {configFilePath}");
            }
        }
        
        private void UpdateJsonConfigFile(string configFilePath, Environment env)
        {
            try
            {
                // Read the JSON file
                string jsonContent = File.ReadAllText(configFilePath);
                
                // Parse JSON content
                using (var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent))
                {
                    // Create a mutable copy of the document
                    var jsonElement = jsonDoc.RootElement;
                    var jsonObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
                
                    if (jsonObj == null)
                    {
                        throw new InvalidOperationException($"Failed to parse JSON content of file: {configFilePath}. Check that it contains valid JSON with key-value pairs.");
                    }
                    
                    bool settingsUpdated = false;
                    
                    foreach (ConfigSetting setting in _project.ConfigSettings)
                    {
                        string newValue = env switch
                        {
                            Environment.Development => setting.DevelopmentValue,
                            Environment.Test => setting.TestValue,
                            Environment.Production => setting.ProductionValue,
                            _ => setting.DevelopmentValue
                        };
                        
                        if (jsonObj.ContainsKey(setting.KeyName))
                        {
                            var oldValue = jsonObj[setting.KeyName]?.ToString();
                            jsonObj[setting.KeyName] = newValue;
                            settingsUpdated = true;
                            Logger.Instance.LogDebug($"Updated JSON config key '{setting.KeyName}' from '{oldValue}' to '{newValue}'");
                        }
                        else
                        {
                            jsonObj.Add(setting.KeyName, newValue);
                            settingsUpdated = true;
                            Logger.Instance.LogDebug($"Added JSON config key '{setting.KeyName}' with value '{newValue}'");
                        }
                    }
                    
                    if (settingsUpdated)
                    {
                        // Serialize the modified object back to JSON
                        string updatedJson = System.Text.Json.JsonSerializer.Serialize(jsonObj, new System.Text.Json.JsonSerializerOptions 
                        { 
                            WriteIndented = true 
                        });
                        
                        // Write the updated JSON back to the file
                        File.WriteAllText(configFilePath, updatedJson);
                        Logger.Instance.LogDebug($"Saved updated JSON config file: {configFilePath}");
                    }
                    else
                    {
                        Logger.Instance.LogDebug($"No changes made to JSON config file: {configFilePath}");
                    }
                }
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                Logger.Instance.LogError($"Invalid JSON format in config file {configFilePath}: {jsonEx.Message}");
                throw new InvalidOperationException($"Invalid JSON format in config file {configFilePath}: {jsonEx.Message}", jsonEx);
            }
            catch (IOException ioEx)
            {
                Logger.Instance.LogError($"Error reading or writing config file {configFilePath}: {ioEx.Message}");
                throw new IOException($"Error reading or writing config file {configFilePath}: {ioEx.Message}", ioEx);
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
}