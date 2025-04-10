using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Linq;

namespace devotter.Models
{
    public class Project
    {
        public string Name { get; set; } = "";
        public string CurrentVersion { get; set; } = "1.0.0";
        public string SourcePath { get; set; } = "";
        public string ProjectFilePath { get; set; } = "";
        public string BuildCommand { get; set; } = "dotnet build -c Release";
        
        public bool DeployedToDevelopment { get; set; } = false;
        public bool DeployedToTest { get; set; } = false;
        public bool DeployedToProduction { get; set; } = false;
        
        public List<ConfigSetting> ConfigSettings { get; set; } = new List<ConfigSetting>();
        
        /// <summary>
        /// Loads project information from a .csproj file
        /// </summary>
        public static Project FromCsprojFile(string csprojPath)
        {
            if (!File.Exists(csprojPath) || !csprojPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid .csproj file path", nameof(csprojPath));
            }
            
            var project = new Project
            {
                ProjectFilePath = csprojPath,
                SourcePath = Path.GetDirectoryName(csprojPath) ?? ""
            };
            
            try
            {
                // Load the .csproj file as XML
                var doc = new System.Xml.XmlDocument();
                doc.Load(csprojPath);
                
                // Get project name from Assembly name or file name
                var assemblyName = doc.SelectSingleNode("//AssemblyName")?.InnerText;
                if (!string.IsNullOrEmpty(assemblyName))
                {
                    project.Name = assemblyName;
                }
                else
                {
                    // Fall back to file name without extension
                    project.Name = Path.GetFileNameWithoutExtension(csprojPath);
                }
                
                // Try to get version from different possible locations
                var version = doc.SelectSingleNode("//Version")?.InnerText
                    ?? doc.SelectSingleNode("//AssemblyVersion")?.InnerText
                    ?? doc.SelectSingleNode("//FileVersion")?.InnerText
                    ?? "1.0.0";
                
                // Ensure version is in format x.y.z
                if (System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+"))
                {
                    string[] versionParts = version.Split(new[] { '.' });
                    // Verify we have at least 3 parts after splitting
                    if (versionParts.Length >= 3)
                    {
                        project.CurrentVersion = versionParts.Take(3).Aggregate((a, b) => $"{a}.{b}");
                    }
                    else
                    {
                        project.CurrentVersion = "1.0.0"; // Default if format is incorrect
                        System.Diagnostics.Debug.WriteLine($"Warning: Version format from {csprojPath} doesn't have 3 parts: {version}. Using default version 1.0.0.");
                    }
                }
                else
                {
                    project.CurrentVersion = "1.0.0"; // Default if not found
                    System.Diagnostics.Debug.WriteLine($"Warning: Could not parse version format from {csprojPath}. Using default version 1.0.0.");
                }
            }
            catch (Exception ex)
            {
                // If there are any errors parsing the file, use defaults but log the error
                System.Diagnostics.Debug.WriteLine($"Error parsing project file {csprojPath}: {ex.Message}");
                project.Name = Path.GetFileNameWithoutExtension(csprojPath);
                project.CurrentVersion = "1.0.0";
                
                // Show a warning to the user but don't block the operation
                System.Windows.MessageBox.Show(
                    $"Warning: Could not fully parse the project file {Path.GetFileName(csprojPath)}. Using default values.\n\nError: {ex.Message}",
                    "Project File Warning",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
            
            return project;
        }
    }
}