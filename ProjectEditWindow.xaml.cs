using System;
using System.Collections.ObjectModel;
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
        
        public ProjectEditWindow(Project project = null)
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
        }
        
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(TxtProjectName.Text))
            {
                MessageBox.Show("Project name cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Validate version format (should be x.y.z)
            if (!System.Text.RegularExpressions.Regex.IsMatch(TxtVersion.Text, @"^\d+\.\d+\.\d+$"))
            {
                MessageBox.Show("Version must be in format: x.y.z (e.g. 1.0.0)", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Update project with UI values
            _project.Name = TxtProjectName.Text;
            _project.CurrentVersion = TxtVersion.Text;
            _project.SourcePath = TxtSourcePath.Text;
            
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
            var dialog = new FolderBrowserDialog();
            dialog.Description = "Select Source Directory - This is the folder containing the files to deploy";
            var result = dialog.ShowDialog();
            
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TxtSourcePath.Text = dialog.SelectedPath;
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