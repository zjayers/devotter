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
            dialog.Description = "Select Development Base Directory";
            var result = dialog.ShowDialog();
            
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TxtDevelopmentBasePath.Text = dialog.SelectedPath;
            }
        }
        
        private void BtnSaveBasePaths_Click(object sender, RoutedEventArgs e)
        {
            // Check if paths exist and create them if they don't
            string[] paths = new string[] 
            { 
                TxtDevelopmentBasePath.Text, 
                TxtTestBasePath.Text, 
                TxtProductionBasePath.Text 
            };
            
            foreach (string path in paths)
            {
                if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
                {
                    try
                    {
                        Directory.CreateDirectory(path);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error creating directory: {ex.Message}");
                    }
                }
            }
            
            SaveSettingsToFile();
            LogMessage("Base paths saved and validated.");
        }
        
        private void BtnBrowseTest_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.Description = "Select Test Base Directory";
            var result = dialog.ShowDialog();
            
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TxtTestBasePath.Text = dialog.SelectedPath;
            }
        }
        
        private void BtnBrowseProd_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.Description = "Select Production Base Directory";
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
        
        #region Deployment Handlers and Removal
        
        private async void BtnDeployToDev_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is Project project)
            {
                // Check if source path exists
                if (string.IsNullOrEmpty(project.SourcePath) || !Directory.Exists(project.SourcePath))
                {
                    MessageBox.Show(
                        $"Source path for project {project.Name} does not exist or is not set.\nPlease set a valid source path.",
                        "Deployment Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
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
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                // Check if deployed to development
                bool deployedToDev = deploymentManager.CheckIfDeployedToDevelopment();
                if (!deployedToDev)
                {
                    project.DeployedToDevelopment = false;
                    DgProjects.Items.Refresh();
                    SaveSettingsToFile();
                    
                    MessageBox.Show(
                        $"Project {project.Name} must be deployed to development first.",
                        "Deployment Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                // Make sure status is updated
                project.DeployedToDevelopment = true;
                
                LogMessage($"Deploying {project.Name} to test environment...");
                
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
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                // Check if deployed to development and test
                bool deployedToDev = deploymentManager.CheckIfDeployedToDevelopment();
                if (!deployedToDev)
                {
                    project.DeployedToDevelopment = false;
                    project.DeployedToTest = false;
                    DgProjects.Items.Refresh();
                    SaveSettingsToFile();
                    
                    MessageBox.Show(
                        $"Project {project.Name} must be deployed to development first.",
                        "Deployment Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                bool deployedToTest = deploymentManager.CheckIfDeployedToTest();
                if (!deployedToTest)
                {
                    project.DeployedToTest = false;
                    DgProjects.Items.Refresh();
                    SaveSettingsToFile();
                    
                    MessageBox.Show(
                        $"Project {project.Name} must be deployed to test first.",
                        "Deployment Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                // Make sure status is updated
                project.DeployedToDevelopment = true;
                project.DeployedToTest = true;
                
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
        
        private async void BtnRemoveDevDeployment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Project project)
            {
                // Confirm removal from development
                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to remove {project.Name} v{project.CurrentVersion} from Development environment?\n\nThis will also remove it from Test and Production environments.",
                    "Confirm Removal",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                    
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
                
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                LogMessage($"Removing {project.Name} from all environments...");
                
                bool removalSuccess = await Task.Run(() => deploymentManager.RemoveFromAllEnvironments());
                
                if (removalSuccess)
                {
                    // Update project status
                    project.DeployedToDevelopment = false;
                    project.DeployedToTest = false;
                    project.DeployedToProduction = false;
                    
                    DgProjects.Items.Refresh();
                    SaveSettingsToFile();
                    LogMessage($"{project.Name} has been removed from all environments.");
                }
                else
                {
                    LogMessage($"Failed to remove {project.Name} from environments.");
                }
            }
        }
        
        private async void BtnRemoveTestDeployment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Project project)
            {
                // Confirm removal from test
                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to remove {project.Name} v{project.CurrentVersion} from Test environment?\n\nThis will also remove it from Production environment.",
                    "Confirm Removal",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                    
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
                
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                LogMessage($"Removing {project.Name} from Test and Production environments...");
                
                // Remove from test
                bool testRemoved = await Task.Run(() => deploymentManager.RemoveFromTest());
                
                // Remove from production
                bool prodRemoved = await Task.Run(() => deploymentManager.RemoveFromProduction());
                
                if (testRemoved || prodRemoved)
                {
                    // Update project status
                    project.DeployedToTest = false;
                    project.DeployedToProduction = false;
                    
                    DgProjects.Items.Refresh();
                    SaveSettingsToFile();
                    LogMessage($"{project.Name} has been removed from Test and Production environments.");
                }
                else
                {
                    LogMessage($"Failed to remove {project.Name} from Test and Production environments.");
                }
            }
        }
        
        private async void BtnRemoveProdDeployment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Project project)
            {
                // Confirm removal from production
                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to remove {project.Name} v{project.CurrentVersion} from Production environment?",
                    "Confirm Removal",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                    
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
                
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                LogMessage($"Removing {project.Name} from Production environment...");
                
                bool removalSuccess = await Task.Run(() => deploymentManager.RemoveFromProduction());
                
                if (removalSuccess)
                {
                    // Update project status
                    project.DeployedToProduction = false;
                    
                    DgProjects.Items.Refresh();
                    SaveSettingsToFile();
                    LogMessage($"{project.Name} has been removed from Production environment.");
                }
                else
                {
                    LogMessage($"Failed to remove {project.Name} from Production environment.");
                }
            }
        }
        
        #endregion
    }
}