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
        
        public bool EnableFileLogging { get; set; } = true;
        public string LogFilePath { get; set; } = "";
        
        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "devotter", 
            "settings.json");
            
        private static string DefaultLogFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "devotter", 
            "logs");
            
        public static AppSettings Load()
        {
            try
            {
                string settingsFilePath = SettingsPath;
                
                if (File.Exists(settingsFilePath))
                {
                    try
                    {
                        // File.ReadAllText is an atomic operation, so we don't need to worry about the file changing
                        // between checking existence and reading
                        string json = File.ReadAllText(settingsFilePath);
                        
                        if (string.IsNullOrWhiteSpace(json))
                        {
                            System.Diagnostics.Debug.WriteLine("Settings file is empty");
                            return new AppSettings();
                        }
                        
                        var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                        
                        // Perform null checks and validation on loaded settings
                        if (settings == null)
                        {
                            System.Diagnostics.Debug.WriteLine("Settings deserialization returned null");
                            return new AppSettings();
                        }
                        
                        // Initialize collections if they're null
                        settings.Projects ??= new List<Project>();
                        
                        // Validate string properties
                        settings.DevelopmentBasePath ??= "";
                        settings.TestBasePath ??= "";
                        settings.ProductionBasePath ??= "";
                        settings.LogFilePath ??= "";
                        
                        // Validate and sanitize projects
                        foreach (var project in settings.Projects)
                        {
                            // Validate version format
                            if (string.IsNullOrWhiteSpace(project.CurrentVersion) || 
                                !System.Text.RegularExpressions.Regex.IsMatch(project.CurrentVersion, @"^\d+(\.\d+)*$"))
                            {
                                System.Diagnostics.Debug.WriteLine($"Invalid version format in project {project.Name}, defaulting to 1.0.0");
                                project.CurrentVersion = "1.0.0";
                            }
                            
                            // Ensure name is valid
                            if (string.IsNullOrWhiteSpace(project.Name))
                            {
                                System.Diagnostics.Debug.WriteLine("Found project with empty name, setting default name");
                                project.Name = "Unnamed Project";
                            }
                            
                            // Validate source path is absolute if provided
                            if (!string.IsNullOrEmpty(project.SourcePath) && !Path.IsPathRooted(project.SourcePath))
                            {
                                System.Diagnostics.Debug.WriteLine($"Project {project.Name} has relative source path: {project.SourcePath}");
                            }
                            
                            // Initialize config settings if null
                            project.ConfigSettings ??= new List<ConfigSetting>();
                        }
                        
                        // Validate paths
                        bool hasInvalidPaths = false;
                        if (!string.IsNullOrEmpty(settings.DevelopmentBasePath) && !Path.IsPathRooted(settings.DevelopmentBasePath))
                        {
                            System.Diagnostics.Debug.WriteLine($"Invalid development path: {settings.DevelopmentBasePath} - not rooted");
                            hasInvalidPaths = true;
                        }
                        
                        if (!string.IsNullOrEmpty(settings.TestBasePath) && !Path.IsPathRooted(settings.TestBasePath))
                        {
                            System.Diagnostics.Debug.WriteLine($"Invalid test path: {settings.TestBasePath} - not rooted");
                            hasInvalidPaths = true;
                        }
                        
                        if (!string.IsNullOrEmpty(settings.ProductionBasePath) && !Path.IsPathRooted(settings.ProductionBasePath))
                        {
                            System.Diagnostics.Debug.WriteLine($"Invalid production path: {settings.ProductionBasePath} - not rooted");
                            hasInvalidPaths = true;
                        }
                        
                        if (hasInvalidPaths)
                        {
                            System.Windows.MessageBox.Show(
                                "Some environment paths in the settings file are not valid absolute paths.\n" +
                                "Please reconfigure them in the application settings.",
                                "Path Validation Warning",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        }
                        
                        return settings;
                    }
                    catch (IOException ioEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"IO error loading settings: {ioEx.Message}");
                        System.Windows.MessageBox.Show(
                            $"Unable to read application settings file: {ioEx.Message}", 
                            "Settings Error", 
                            System.Windows.MessageBoxButton.OK, 
                            System.Windows.MessageBoxImage.Warning);
                        return new AppSettings();
                    }
                    catch (JsonException jsonEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"JSON parsing error loading settings: {jsonEx.Message}");
                        System.Windows.MessageBox.Show(
                            $"Invalid settings file format: {jsonEx.Message}", 
                            "Settings Error", 
                            System.Windows.MessageBoxButton.OK, 
                            System.Windows.MessageBoxImage.Warning);
                        return new AppSettings();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                        System.Windows.MessageBox.Show(
                            $"Unable to load application settings: {ex.Message}", 
                            "Settings Error", 
                            System.Windows.MessageBoxButton.OK, 
                            System.Windows.MessageBoxImage.Warning);
                        return new AppSettings();
                    }
                }
                
                // If file doesn't exist, create a new settings object with default values
                System.Diagnostics.Debug.WriteLine("Settings file does not exist, creating new settings");
                return new AppSettings();
            }
            catch (Exception ex)
            {
                // Last resort exception handler
                System.Diagnostics.Debug.WriteLine($"Unhandled exception in settings load: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Critical error loading settings: {ex.Message}", 
                    "Settings Error", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
                return new AppSettings();
            }
        }
        
        public bool Save()
        {
            try
            {
                // Get the directory path and verify it's not null
                string directoryPath = Path.GetDirectoryName(SettingsPath);
                if (string.IsNullOrEmpty(directoryPath))
                {
                    throw new InvalidOperationException("Could not determine settings directory path");
                }
                
                // Create directory if it doesn't exist
                try
                {
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                }
                catch (IOException ioEx)
                {
                    throw new IOException($"Could not create settings directory: {directoryPath}", ioEx);
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    throw new UnauthorizedAccessException($"Access denied when creating settings directory: {directoryPath}", uaEx);
                }
                
                // Serialize the settings
                string json;
                try
                {
                    json = JsonConvert.SerializeObject(this, Formatting.Indented);
                }
                catch (JsonException jsonEx)
                {
                    throw new InvalidOperationException("Failed to serialize settings to JSON", jsonEx);
                }
                
                // First, create a backup of the existing settings file if it exists
                string backupPath = SettingsPath + ".bak";
                if (File.Exists(SettingsPath))
                {
                    try
                    {
                        // Create a backup - remove old backup first if it exists
                        if (File.Exists(backupPath))
                        {
                            File.Delete(backupPath);
                        }
                        File.Copy(SettingsPath, backupPath);
                    }
                    catch (Exception backupEx)
                    {
                        // Log but continue - this is just a best-effort backup
                        System.Diagnostics.Debug.WriteLine($"Warning: Could not create settings backup: {backupEx.Message}");
                    }
                }
                
                // Write to a temporary file first, then use atomic replace operation 
                string tempPath = SettingsPath + ".tmp";
                try
                {
                    // Write the JSON to the temporary file
                    File.WriteAllText(tempPath, json);
                    
                    // Use atomic replace with File.Replace or File.Move to ensure consistency
                    if (File.Exists(SettingsPath))
                    {
                        // On Windows, File.Replace is atomic
                        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                        {
                            File.Replace(tempPath, SettingsPath, backupPath, true);
                        }
                        else
                        {
                            // On non-Windows platforms, we first delete and then move
                            // This is not atomic but is the best we can do
                            File.Delete(SettingsPath);
                            File.Move(tempPath, SettingsPath);
                        }
                    }
                    else
                    {
                        // If the target doesn't exist, simply move the temp file
                        File.Move(tempPath, SettingsPath);
                    }
                    
                    return true;
                }
                catch (IOException ioEx)
                {
                    // Try to restore from backup if we have one
                    if (File.Exists(backupPath) && !File.Exists(SettingsPath))
                    {
                        try
                        {
                            File.Copy(backupPath, SettingsPath);
                            System.Diagnostics.Debug.WriteLine("Restored settings from backup after write failure");
                        }
                        catch
                        {
                            System.Diagnostics.Debug.WriteLine("Failed to restore settings from backup");
                        }
                    }
                    throw new IOException($"Failed to write settings file: {ioEx.Message}", ioEx);
                }
                finally
                {
                    // Clean up temp file if it still exists
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { /* Ignore cleanup errors */ }
                    }
                }
            }
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"Error saving application settings: {ex.Message}",
                    "Settings Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
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