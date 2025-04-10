using System;
using System.Collections.ObjectModel;
using System.IO;  // Add explicit using for Path
using System.Linq; // Add using for LINQ operations
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using devotter.Models;

namespace devotter
{
    public partial class ProjectEditWindow : Window
    {
        private Project _project;
        private ObservableCollection<ConfigSetting> _configSettings;
        
        public Project Project => _project;
        
        public ProjectEditWindow(Project? project = null)
        {
            InitializeComponent();
            
            // Initialize with new or existing project
            _project = project ?? new Project { Name = "New Project" };
            _configSettings = new ObservableCollection<ConfigSetting>(_project.ConfigSettings);
            
            // Bind config settings to list view
            LstConfigSettings.ItemsSource = _configSettings;
            
            // Load project data into UI
            LoadProjectData();
        }
        
        private void LoadProjectData()
        {
            TxtProjectName.Text = _project.Name;
            TxtVersion.Text = _project.CurrentVersion;
            TxtSourcePath.Text = _project.SourcePath;
            TxtBuildCommand.Text = _project.BuildCommand;
        }
        
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(TxtProjectName.Text))
            {
                MessageBox.Show("Project name cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Sanitize project name (remove invalid characters)
            string sanitizedName = string.Join("_", TxtProjectName.Text.Split(Path.GetInvalidFileNameChars()));
            if (sanitizedName != TxtProjectName.Text)
            {
                var result = MessageBox.Show(
                    $"Project name contains invalid characters. Change to '{sanitizedName}'?",
                    "Invalid Project Name",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                    
                if (result == MessageBoxResult.Yes)
                {
                    TxtProjectName.Text = sanitizedName;
                }
                else
                {
                    MessageBox.Show(
                        "Project name must not contain characters that are invalid in file names.",
                        "Validation Error", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning);
                    return;
                }
            }
            
            // Validate version format (should be x.y.z)
            if (!System.Text.RegularExpressions.Regex.IsMatch(TxtVersion.Text, @"^\d+\.\d+\.\d+$"))
            {
                MessageBox.Show("Version must be in format: x.y.z (e.g. 1.0.0)", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Validate source path exists if specified
            if (!string.IsNullOrWhiteSpace(TxtSourcePath.Text))
            {
                if (!Directory.Exists(TxtSourcePath.Text))
                {
                    var result = MessageBox.Show(
                        $"Source path does not exist: {TxtSourcePath.Text}\nDo you want to continue anyway?",
                        "Path Not Found",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                        
                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
                
                // Check if source path is an absolute path
                if (!Path.IsPathRooted(TxtSourcePath.Text))
                {
                    var result = MessageBox.Show(
                        $"Source path should be an absolute path. Current path is relative: {TxtSourcePath.Text}\nDo you want to continue anyway?",
                        "Relative Path Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                        
                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }
            
            // Validate configuration settings - check for empty keys
            var emptyKeySettings = _configSettings.Where(s => string.IsNullOrWhiteSpace(s.KeyName)).ToList();
            if (emptyKeySettings.Any())
            {
                var result = MessageBox.Show(
                    $"{emptyKeySettings.Count} configuration setting(s) have empty keys. These settings will be removed. Continue?",
                    "Invalid Configuration",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                    
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
                
                // Remove settings with empty keys
                foreach (var setting in emptyKeySettings)
                {
                    _configSettings.Remove(setting);
                }
            }
            
            // Check for duplicate keys
            var duplicateKeys = _configSettings
                .GroupBy(s => s.KeyName)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
                
            if (duplicateKeys.Any())
            {
                var result = MessageBox.Show(
                    $"Duplicate key names found: {string.Join(", ", duplicateKeys)}.\nOnly the last setting for each key will be used. Continue?",
                    "Duplicate Keys Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                    
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            
            // Update project with UI values
            _project.Name = TxtProjectName.Text;
            _project.CurrentVersion = TxtVersion.Text;
            _project.SourcePath = TxtSourcePath.Text;
            _project.BuildCommand = TxtBuildCommand.Text;
            
            // Update config settings
            _project.ConfigSettings.Clear();
            foreach (ConfigSetting setting in _configSettings)
            {
                _project.ConfigSettings.Add(setting);
            }
            
            DialogResult = true;
            Close();
        }
        
        private void BtnBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Source Directory - This is the folder containing the files to deploy";
                dialog.ShowNewFolderButton = true; // Allow creating new folders
                
                var result = dialog.ShowDialog();
                
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    TxtSourcePath.Text = dialog.SelectedPath;
                    
                    // Validate that the path is absolute
                    if (!Path.IsPathRooted(dialog.SelectedPath))
                    {
                        MessageBox.Show(
                            "Warning: Selected path is not an absolute path. This may cause issues during deployment.",
                            "Path Validation Warning",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
        }
        
        private void BtnAddConfig_Click(object sender, RoutedEventArgs e)
        {
            _configSettings.Add(new ConfigSetting
            {
                KeyName = "NewSetting",
                DevelopmentValue = "DevValue",
                TestValue = "TestValue",
                ProductionValue = "ProdValue"
            });
        }
        
        private void BtnRemoveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is ConfigSetting setting)
            {
                _configSettings.Remove(setting);
            }
        }
    }
}