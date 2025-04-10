using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.IO;
using System.Threading.Tasks;
using devotter.Models;

namespace devotter
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings;
        private ObservableCollection<Project> _projects;
        
        public MainWindow()
        {
            InitializeComponent();
            
            // Load settings
            _settings = AppSettings.Load();
            _projects = new ObservableCollection<Project>(_settings.Projects);
            
            // Bind projects to data grid
            DgProjects.ItemsSource = _projects;
            
            // Load settings into UI
            LoadSettingsToUI();
        }
        
        private void LoadSettingsToUI()
        {
            TxtDevelopmentBasePath.Text = _settings.DevelopmentBasePath;
            TxtTestBasePath.Text = _settings.TestBasePath;
            TxtProductionBasePath.Text = _settings.ProductionBasePath;
        }
        
        private void SaveSettingsToFile()
        {
            // Update settings with UI values
            _settings.DevelopmentBasePath = TxtDevelopmentBasePath.Text;
            _settings.TestBasePath = TxtTestBasePath.Text;
            _settings.ProductionBasePath = TxtProductionBasePath.Text;
            
            // Update projects list
            _settings.Projects.Clear();
            foreach (Project project in _projects)
            {
                _settings.Projects.Add(project);
            }
            
            // Save settings to file
            _settings.Save();
            
            LogMessage("Settings saved successfully.");
        }
        
        private void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            TxtLogs.AppendText($"[{timestamp}] {message}\n");
            TxtLogs.ScrollToEnd();
            
            TxtStatusBar.Text = message;
            
            // Also output to debug console
            System.Diagnostics.Debug.WriteLine($"[{timestamp}] {message}");
        }
        
        #region Browse Buttons Click Handlers
        
        private void BtnBrowseDev_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();
            
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TxtDevelopmentBasePath.Text = dialog.SelectedPath;
            }
        }
        
        private void BtnSaveBasePaths_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsToFile();
        }
        
        private void BtnBrowseTest_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();
            
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TxtTestBasePath.Text = dialog.SelectedPath;
            }
        }
        
        private void BtnBrowseProd_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();
            
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TxtProductionBasePath.Text = dialog.SelectedPath;
            }
        }
        
        #endregion
        
        #region Project Management Handlers
        
        private void BtnAddProject_Click(object sender, RoutedEventArgs e)
        {
            var projectWindow = new ProjectEditWindow()
            {
                Owner = this
            };
            
            if (projectWindow.ShowDialog() == true)
            {
                _projects.Add(projectWindow.Project);
                SaveSettingsToFile();
            }
        }
        
        private void BtnEditProject_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Project project)
            {
                var projectWindow = new ProjectEditWindow(project)
                {
                    Owner = this
                };
                
                if (projectWindow.ShowDialog() == true)
                {
                    // Project is updated by reference, just save settings
                    SaveSettingsToFile();
                    DgProjects.Items.Refresh();
                }
            }
        }
        
        private void BtnRemoveProject_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Project project)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to remove the project '{project.Name}'?",
                    "Confirm Project Removal",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                    
                if (result == MessageBoxResult.Yes)
                {
                    _projects.Remove(project);
                    SaveSettingsToFile();
                }
            }
        }
        
        #endregion
        
        #region Deployment Handlers
        
        private async void BtnDeployToDev_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Project project)
            {
                // Prompt for version increment
                var versionWindow = new VersionIncrementWindow(project.CurrentVersion)
                {
                    Owner = this
                };
                
                if (versionWindow.ShowDialog() != true)
                {
                    return;
                }
                
                // Get the new version
                string newVersion = versionWindow.NewVersion;
                
                LogMessage($"Building {project.Name} with new version: {newVersion}");
                
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                // Perform build with new version
                bool buildSuccess = await Task.Run(() => deploymentManager.BuildProject(newVersion).Result);
                
                if (!buildSuccess)
                {
                    LogMessage($"Build failed for {project.Name}. Deployment aborted.");
                    return;
                }
                
                LogMessage($"Build completed successfully for {project.Name}.");
                
                // Deploy to development
                LogMessage($"Deploying {project.Name} to development environment...");
                
                bool deploySuccess = await Task.Run(() => deploymentManager.DeployToDevelopment());
                
                if (deploySuccess)
                {
                    project.DeployedToDevelopment = true;
                    DgProjects.Items.Refresh();
                    SaveSettingsToFile();
                    LogMessage($"Deployment of {project.Name} to development completed successfully.");
                }
                else
                {
                    LogMessage($"Deployment of {project.Name} to development failed.");
                }
            }
        }
        
        private async void BtnDeployToTest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Project project)
            {
                if (!project.DeployedToDevelopment)
                {
                    MessageBox.Show(
                        $"Project {project.Name} must be deployed to development first.",
                        "Deployment Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                LogMessage($"Deploying {project.Name} to test environment...");
                
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                bool deploySuccess = await Task.Run(() => deploymentManager.DeployToTest());
                
                if (deploySuccess)
                {
                    project.DeployedToTest = true;
                    DgProjects.Items.Refresh();
                    SaveSettingsToFile();
                    LogMessage($"Deployment of {project.Name} to test completed successfully.");
                }
                else
                {
                    LogMessage($"Deployment of {project.Name} to test failed.");
                }
            }
        }
        
        private async void BtnDeployToProd_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Project project)
            {
                if (!project.DeployedToTest)
                {
                    MessageBox.Show(
                        $"Project {project.Name} must be deployed to test first.",
                        "Deployment Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                // Confirm deployment to production
                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to deploy {project.Name} to production?",
                    "Confirm Production Deployment",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                    
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
                
                LogMessage($"Deploying {project.Name} to production environment...");
                
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                bool deploySuccess = await Task.Run(() => deploymentManager.DeployToProduction());
                
                if (deploySuccess)
                {
                    project.DeployedToProduction = true;
                    DgProjects.Items.Refresh();
                    SaveSettingsToFile();
                    LogMessage($"Deployment of {project.Name} to production completed successfully.");
                }
                else
                {
                    LogMessage($"Deployment of {project.Name} to production failed.");
                }
            }
        }
        
        #endregion
    }
}