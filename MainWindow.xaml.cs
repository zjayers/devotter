using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.IO;
using System.Text;
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
            
            // Initialize the logger
            if (string.IsNullOrEmpty(_settings.LogFilePath))
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "devotter", 
                    "logs");
                    
                Directory.CreateDirectory(logDir);
                
                // Use a safe date format for log filename (yyyy-MM-dd)
                string safeDateString = DateTime.Now.ToString("yyyy-MM-dd");
                _settings.LogFilePath = Path.Combine(logDir, $"devotter_log_{safeDateString}.log");
            }
            
            Logger.Initialize(_settings.LogFilePath, _settings.EnableFileLogging);
            Logger.Instance.LogInfo("Application started");
            
            // Subscribe to log events
            Logger.Instance.LogEntryAdded += OnLogEntryAdded;
            
            // Handle application closing
            this.Closing += MainWindow_Closing;
            
            // Bind projects to data grid
            DgProjects.ItemsSource = _projects;
            
            // Load settings into UI
            LoadSettingsToUI();
            
            // Update status of all projects
            UpdateAllProjectStatuses();
            
            // Set application version
            string appVersion = GetApplicationVersion();
            TxtAppVersion.Text = $"Devotter v{appVersion}";
        }
        
        private void OnLogEntryAdded(object? sender, LogEntry entry)
        {
            if (entry == null)
                return;
                
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnLogEntryAdded(sender, entry));
                return;
            }
            
            // Add log entry to the UI log
            TxtLogs.AppendText(entry.ToString() + Environment.NewLine);
            TxtLogs.ScrollToEnd();
        }
        
        private async Task UpdateAllProjectStatusesAsync()
        {
            try
            {
                var updateTasks = new List<Task>();
                var managersToDispose = new List<DeploymentManager>(); // This is thread-safe as it's not shared between threads
                
                try
                {
                    // Create all managers first in the main thread
                    foreach (var project in _projects)
                    {
                        var manager = new DeploymentManager(project, _settings);
                        managersToDispose.Add(manager);
                    }
                    
                    // Then start tasks concurrently
                    foreach (var manager in managersToDispose)
                    {
                        updateTasks.Add(manager.UpdateAllDeploymentStatus());
                    }
                    
                    // Wait for all tasks to complete
                    await Task.WhenAll(updateTasks);
                    
                    // Refresh the UI on the UI thread
                    if (!Dispatcher.CheckAccess())
                    {
                        await Dispatcher.InvokeAsync(() => DgProjects.Items.Refresh());
                    }
                    else
                    {
                        DgProjects.Items.Refresh();
                    }
                }
                finally
                {
                    // Dispose all managers
                    foreach (var manager in managersToDispose)
                    {
                        try
                        {
                            manager.Dispose();
                        }
                        catch (Exception disposeEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error disposing manager: {disposeEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating project statuses: {ex.Message}");
                Logger.Instance.LogError($"Status update error: {ex.Message}");
            }
        }
        
        // Backwards compatibility wrapper for synchronous callers
        private void UpdateAllProjectStatuses()
        {
            // Fire and forget, but log errors
            UpdateAllProjectStatusesAsync().ContinueWith(t => 
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    Logger.Instance.LogError($"Async project status update error: {t.Exception.InnerException?.Message ?? t.Exception.Message}");
                }
            }, TaskScheduler.Default);
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
            bool saveSuccess = _settings.Save();
            
            if (saveSuccess)
            {
                LogMessage("Settings saved successfully.");
            }
            else
            {
                LogMessage("Failed to save settings.");
            }
        }
        
        private void LogMessage(string message)
        {
            // Log to the logger (which will add to UI via event)
            Logger.Instance.LogInfo(message);
            
            // Update status bar
            TxtStatusBar.Text = message;
        }
        
        private void ShowProgress()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(ShowProgress);
                return;
            }
            
            ProgressIndicator.Visibility = Visibility.Visible;
        }
        
        private void HideProgress()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(HideProgress);
                return;
            }
            
            ProgressIndicator.Visibility = Visibility.Collapsed;
        }
        
        #region Browse Buttons Click Handlers
        
        private void BtnBrowseDev_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Development Base Directory";
                var result = dialog.ShowDialog();
                
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    TxtDevelopmentBasePath.Text = dialog.SelectedPath;
                }
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
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Test Base Directory";
                var result = dialog.ShowDialog();
                
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    TxtTestBasePath.Text = dialog.SelectedPath;
                }
            }
        }
        
        private void BtnBrowseProd_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Production Base Directory";
                var result = dialog.ShowDialog();
                
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    TxtProductionBasePath.Text = dialog.SelectedPath;
                }
            }
        }
        
        #endregion
        
        #region Project Management Handlers
        
        private bool ValidateEnvironmentPaths()
        {
            // Check all required paths
            if (string.IsNullOrEmpty(_settings.DevelopmentBasePath))
            {
                MessageBox.Show(
                    "Development environment path is not set. Please set up environment paths first.",
                    "Path Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            
            if (string.IsNullOrEmpty(_settings.TestBasePath))
            {
                MessageBox.Show(
                    "Test environment path is not set. Please set up environment paths first.",
                    "Path Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            
            if (string.IsNullOrEmpty(_settings.ProductionBasePath))
            {
                MessageBox.Show(
                    "Production environment path is not set. Please set up environment paths first.",
                    "Path Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            
            // Verify paths are absolute
            bool allAbsolute = true;
            StringBuilder relativePaths = new StringBuilder();
            
            if (!Path.IsPathRooted(_settings.DevelopmentBasePath))
            {
                relativePaths.AppendLine($"- Development path: {_settings.DevelopmentBasePath}");
                allAbsolute = false;
            }
            
            if (!Path.IsPathRooted(_settings.TestBasePath))
            {
                relativePaths.AppendLine($"- Test path: {_settings.TestBasePath}");
                allAbsolute = false;
            }
            
            if (!Path.IsPathRooted(_settings.ProductionBasePath))
            {
                relativePaths.AppendLine($"- Production path: {_settings.ProductionBasePath}");
                allAbsolute = false;
            }
            
            if (!allAbsolute)
            {
                MessageBox.Show(
                    $"The following paths must be absolute paths (not relative):\n\n{relativePaths}\nPlease use full paths starting with drive letter or root.",
                    "Invalid Paths",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            
            // Verify paths exist
            bool allValid = true;
            StringBuilder missingPaths = new StringBuilder();
            
            if (!Directory.Exists(_settings.DevelopmentBasePath))
            {
                missingPaths.AppendLine($"- Development path: {_settings.DevelopmentBasePath}");
                allValid = false;
            }
            
            if (!Directory.Exists(_settings.TestBasePath))
            {
                missingPaths.AppendLine($"- Test path: {_settings.TestBasePath}");
                allValid = false;
            }
            
            if (!Directory.Exists(_settings.ProductionBasePath))
            {
                missingPaths.AppendLine($"- Production path: {_settings.ProductionBasePath}");
                allValid = false;
            }
            
            if (!allValid)
            {
                var result = MessageBox.Show(
                    $"The following environment paths do not exist:\n\n{missingPaths}\nWould you like to create them now?",
                    "Missing Environment Paths",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                    
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (!Directory.Exists(_settings.DevelopmentBasePath))
                            Directory.CreateDirectory(_settings.DevelopmentBasePath);
                            
                        if (!Directory.Exists(_settings.TestBasePath))
                            Directory.CreateDirectory(_settings.TestBasePath);
                            
                        if (!Directory.Exists(_settings.ProductionBasePath))
                            Directory.CreateDirectory(_settings.ProductionBasePath);
                            
                        LogMessage("Created missing environment paths.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error creating directories: {ex.Message}");
                        MessageBox.Show(
                            $"Error creating environment directories: {ex.Message}",
                            "Directory Creation Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            
            return true;
        }

        private void BtnAddProject_Click(object sender, RoutedEventArgs e)
        {
            // Open a file dialog to select a .csproj file
            var dialog = new OpenFileDialog
            {
                Title = "Select Project File",
                Filter = "C# Project Files (*.csproj)|*.csproj",
                Multiselect = false
            };
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // Create a new project from the csproj file
                    var project = Project.FromCsprojFile(dialog.FileName);
                    
                    // Check if a project with the same file path already exists
                    if (_projects.Any(p => p.ProjectFilePath == project.ProjectFilePath))
                    {
                        MessageBox.Show(
                            "This project is already in the list.", 
                            "Duplicate Project", 
                            MessageBoxButtons.OK, 
                            MessageBoxIcon.Warning);
                        return;
                    }
                    
                    // Add the project to the list
                    _projects.Add(project);
                    SaveSettingsToFile();
                    
                    // Update the project status
                    UpdateAllProjectStatuses();
                    
                    LogMessage($"Added project: {project.Name} v{project.CurrentVersion}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error adding project: {ex.Message}", 
                        "Error", 
                        MessageBoxButtons.OK, 
                        MessageBoxIcon.Error);
                }
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
            // Validate environment paths
            if (!ValidateEnvironmentPaths())
            {
                return;
            }
            
            if (sender is System.Windows.Controls.Button button && button.Tag is Project project)
            {
                // Check if source path exists
                if (string.IsNullOrEmpty(project.SourcePath) || !Directory.Exists(project.SourcePath))
                {
                    MessageBox.Show(
                        $"Source path for project {project.Name} does not exist or is not set.",
                        "Deployment Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                // Update status before deployment
                deploymentManager.UpdateAllDeploymentStatus();
                
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
                bool buildSuccess = await deploymentManager.BuildProject(newVersion);
                
                if (!buildSuccess)
                {
                    LogMessage($"Build failed for {project.Name}. Deployment aborted.");
                    return;
                }
                
                LogMessage($"Build completed successfully for {project.Name}.");
                
                // Deploy to development
                LogMessage($"Deploying {project.Name} to development environment...");
                ShowProgress();
                
                try
                {
                    // Run deployment and status update together in background thread with cancellation support
                    bool deploySuccess = await Task.Run(async () => 
                    {
                        try
                        {
                            // Check if cancellation was requested
                            if (_shutdownCts.Token.IsCancellationRequested)
                                return false;
                                
                            bool success = deploymentManager.DeployToDevelopment();
                            if (success && !_shutdownCts.Token.IsCancellationRequested)
                            {
                                // Update status in the background thread
                                await deploymentManager.UpdateAllDeploymentStatus();
                            }
                            return success;
                        }
                        catch (OperationCanceledException)
                        {
                            // Operation was canceled, log and return false
                            Logger.Instance.LogWarning("Deployment operation was canceled during application shutdown");
                            return false;
                        }
                    }, _shutdownCts.Token);
                    
                    if (deploySuccess)
                    {
                        // UI updates in the UI thread
                        DgProjects.Items.Refresh();
                        SaveSettingsToFile();
                        LogMessage($"Deployment of {project.Name} to development completed successfully.");
                    }
                    else
                    {
                        LogMessage($"Deployment of {project.Name} to development failed.");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error during deployment: {ex.Message}");
                    MessageBox.Show(
                        ex.Message,
                        "Deployment Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    HideProgress();
                }
            }
        }
        
        private async void BtnDeployToTest_Click(object sender, RoutedEventArgs e)
        {
            // Validate environment paths
            if (!ValidateEnvironmentPaths())
            {
                return;
            }
            
            if (sender is System.Windows.Controls.Button button && button.Tag is Project project)
            {
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                // Update status before deployment
                deploymentManager.UpdateAllDeploymentStatus();
                
                // Check if deployed to development
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
                ShowProgress();
                
                try
                {
                    // Run deployment and status update together in background thread
                    bool deploySuccess = await Task.Run(() => 
                    {
                        bool success = deploymentManager.DeployToTest();
                        if (success)
                        {
                            // Update status in the background thread
                            deploymentManager.UpdateAllDeploymentStatus();
                        }
                        return success;
                    });
                    
                    if (deploySuccess)
                    {
                        // UI updates in the UI thread
                        DgProjects.Items.Refresh();
                        SaveSettingsToFile();
                        LogMessage($"Deployment of {project.Name} to test completed successfully.");
                    }
                    else
                    {
                        LogMessage($"Deployment of {project.Name} to test failed.");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error during test deployment: {ex.Message}");
                    MessageBox.Show(
                        ex.Message,
                        "Test Deployment Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    HideProgress();
                }
            }
        }
        
        private async void BtnDeployToProd_Click(object sender, RoutedEventArgs e)
        {
            // Validate environment paths
            if (!ValidateEnvironmentPaths())
            {
                return;
            }
            
            if (sender is System.Windows.Controls.Button button && button.Tag is Project project)
            {
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                // Update status before deployment
                deploymentManager.UpdateAllDeploymentStatus();
                
                // Check if deployed to development and test
                if (!project.DeployedToDevelopment)
                {
                    MessageBox.Show(
                        $"Project {project.Name} must be deployed to development first.",
                        "Deployment Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
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
                ShowProgress();
                
                try
                {
                    // Run deployment and status update together in background thread
                    bool deploySuccess = await Task.Run(() => 
                    {
                        bool success = deploymentManager.DeployToProduction();
                        if (success)
                        {
                            // Update status in the background thread
                            deploymentManager.UpdateAllDeploymentStatus();
                        }
                        return success;
                    });
                    
                    if (deploySuccess)
                    {
                        // UI updates in the UI thread
                        DgProjects.Items.Refresh();
                        SaveSettingsToFile();
                        LogMessage($"Deployment of {project.Name} to production completed successfully.");
                    }
                    else
                    {
                        LogMessage($"Deployment of {project.Name} to production failed.");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error during production deployment: {ex.Message}");
                    MessageBox.Show(
                        ex.Message,
                        "Production Deployment Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    HideProgress();
                }
            }
        }
        
        private async void BtnRemoveDevDeployment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is Project project)
            {
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                // Update status before removal
                deploymentManager.UpdateAllDeploymentStatus();
                
                // Only show dialog if project is actually deployed
                if (!project.DeployedToDevelopment)
                {
                    LogMessage($"{project.Name} is not currently deployed to Development.");
                    return;
                }
                
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
                
                LogMessage($"Removing {project.Name} from all environments...");
                ShowProgress();
                
                try
                {
                    bool removalSuccess = await Task.Run(() => deploymentManager.RemoveFromAllEnvironments());
                    
                    // Update project status after removal
                    deploymentManager.UpdateAllDeploymentStatus();
                    DgProjects.Items.Refresh();
                    SaveSettingsToFile();
                    
                    if (removalSuccess)
                    {
                        LogMessage($"{project.Name} has been removed from all environments.");
                    }
                    else
                    {
                        LogMessage($"No deployment files were found to remove for {project.Name}.");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error removing deployments: {ex.Message}");
                    MessageBox.Show(
                        ex.Message,
                        "Removal Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                        
                    // Still need to update status to show what was actually removed
                    deploymentManager.UpdateAllDeploymentStatus();
                    DgProjects.Items.Refresh();
                }
                finally
                {
                    HideProgress();
                }
            }
        }
        
        private async void BtnRemoveTestDeployment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is Project project)
            {
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                // Update status before removal
                deploymentManager.UpdateAllDeploymentStatus();
                
                // Only show dialog if project is actually deployed
                if (!project.DeployedToTest)
                {
                    LogMessage($"{project.Name} is not currently deployed to Test.");
                    return;
                }
                
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
                
                LogMessage($"Removing {project.Name} from Test and Production environments...");
                ShowProgress();
                
                bool testRemoved = false;
                bool prodRemoved = false;
                List<string> errors = new List<string>();
                
                try
                {
                    // Remove from production first
                    prodRemoved = await Task.Run(() => deploymentManager.RemoveFromProduction());
                }
                catch (Exception ex)
                {
                    errors.Add($"Production: {ex.Message}");
                    LogMessage($"Error removing from Production: {ex.Message}");
                }
                
                try
                {
                    // Remove from test second
                    testRemoved = await Task.Run(() => deploymentManager.RemoveFromTest());
                }
                catch (Exception ex)
                {
                    errors.Add($"Test: {ex.Message}");
                    LogMessage($"Error removing from Test: {ex.Message}");
                }
                
                // Update project status after removal
                deploymentManager.UpdateAllDeploymentStatus();
                DgProjects.Items.Refresh();
                SaveSettingsToFile();
                
                if (errors.Count > 0)
                {
                    string errorMessage = $"Errors occurred during removal: {string.Join("; ", errors)}";
                    MessageBox.Show(
                        errorMessage,
                        "Removal Errors",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                
                if (testRemoved || prodRemoved)
                {
                    LogMessage($"{project.Name} has been removed from Test and Production environments.");
                }
                else if (errors.Count == 0)
                {
                    LogMessage($"No deployment files were found to remove for {project.Name} in Test and Production.");
                }
                
                HideProgress();
            }
        }
        
        private async void BtnRemoveProdDeployment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is Project project)
            {
                // Create deployment manager for this project
                var deploymentManager = new DeploymentManager(project, _settings);
                
                // Update status before removal
                deploymentManager.UpdateAllDeploymentStatus();
                
                // Only show dialog if project is actually deployed
                if (!project.DeployedToProduction)
                {
                    LogMessage($"{project.Name} is not currently deployed to Production.");
                    return;
                }
                
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
                
                LogMessage($"Removing {project.Name} from Production environment...");
                ShowProgress();
                
                try
                {
                    // Run removal and status update together in background thread
                    bool removalSuccess = await Task.Run(() => 
                    {
                        bool success = deploymentManager.RemoveFromProduction();
                        // Update status in the background thread
                        deploymentManager.UpdateAllDeploymentStatus();
                        return success;
                    });
                    
                    // UI updates in the UI thread
                    DgProjects.Items.Refresh();
                    SaveSettingsToFile();
                    
                    if (removalSuccess)
                    {
                        LogMessage($"{project.Name} has been removed from Production environment.");
                    }
                    else
                    {
                        LogMessage($"No deployment files were found to remove for {project.Name} in Production.");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error removing from Production: {ex.Message}");
                    MessageBox.Show(
                        ex.Message,
                        "Production Removal Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                        
                    // Update status to reflect current state
                    deploymentManager.UpdateAllDeploymentStatus();
                    DgProjects.Items.Refresh();
                }
                finally
                {
                    HideProgress();
                }
            }
        }
        
        #endregion
        
        // Create a cancellation token source for shutdown
        private System.Threading.CancellationTokenSource _shutdownCts = new System.Threading.CancellationTokenSource();
        
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Signal all operations to cancel
                if (!_shutdownCts.IsCancellationRequested)
                {
                    _shutdownCts.Cancel();
                }
                
                // Check if there are any visible progress indicators, which would suggest operations in progress
                if (ProgressIndicator.Visibility == Visibility.Visible)
                {
                    var result = System.Windows.MessageBox.Show(
                        "Operations are still in progress. Are you sure you want to exit?",
                        "Confirm Exit",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                        
                    if (result == MessageBoxResult.No)
                    {
                        // Cancel the window closing
                        e.Cancel = true;
                        return;
                    }
                    
                    // Force hide progress indicator
                    ProgressIndicator.Visibility = Visibility.Collapsed;
                }
                
                // Dispose any remaining deployment managers
                // This is a safety measure to clean up resources
                foreach (var project in _projects)
                {
                    try
                    {
                        using (var manager = new DeploymentManager(project, _settings))
                        {
                            // Nothing to do here, just ensuring proper disposal
                        }
                    }
                    catch (Exception disposeEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error disposing manager during shutdown: {disposeEx.Message}");
                    }
                }
                
                // Save settings
                SaveSettingsToFile();
                
                // Thread-safe event unsubscription
                var logger = Logger.Instance;
                if (logger != null)
                {
                    // Thread-safe unsubscription in a synchronized context
                    EventHandler<LogEntry> handler = OnLogEntryAdded;
                    logger.LogEntryAdded -= handler;
                
                    // Shutdown the logger
                    logger.LogInfo("Application closing");
                    logger.Shutdown();
                }
                
                // Dispose the cancellation token source
                try
                {
                    _shutdownCts.Dispose();
                }
                catch (Exception ctsEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing cancellation token source: {ctsEx.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during shutdown: {ex.Message}");
            }
        }
        
        private string GetApplicationVersion()
        {
            try
            {
                // Get the assembly version
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                if (assembly == null)
                {
                    Logger.Instance.LogError("Failed to get executing assembly");
                    return "1.0.0"; // Fallback to default
                }
                
                if (string.IsNullOrEmpty(assembly.Location))
                {
                    Logger.Instance.LogError("Assembly location is null or empty");
                    return "1.0.0"; // Fallback to default
                }
                
                System.Diagnostics.FileVersionInfo fileVersionInfo;
                try
                {
                    fileVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError($"Error getting file version info: {ex.Message}");
                    return "1.0.0"; // Fallback to default
                }
                
                // Format version as major.minor.build
                if (!string.IsNullOrEmpty(fileVersionInfo.ProductVersion))
                {
                    return fileVersionInfo.ProductVersion;
                }
                
                // Fallback to assembly version
                Version? version = assembly.GetName().Version;
                if (version != null)
                {
                    return $"{version.Major}.{version.Minor}.{version.Build}";
                }
                
                // Default version if nothing else works
                return "1.0.0";
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Error getting application version: {ex.Message}");
                return "1.0.0";
            }
        }
    }
}