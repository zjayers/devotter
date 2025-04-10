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
        private ObservableCollection<ConfigSetting> _configSettings;
        private DeploymentManager? _deploymentManager;
        
        public MainWindow()
        {
            InitializeComponent();
            
            // Load settings
            _settings = AppSettings.Load();
            _configSettings = new ObservableCollection<ConfigSetting>(_settings.ConfigSettings);
            
            // Initialize deployment manager
            _deploymentManager = new DeploymentManager(_settings);
            
            // Bind UI elements to settings
            DataContext = _settings;
            LstConfigSettings.ItemsSource = _configSettings;
            
            // Load settings into UI
            LoadSettingsToUI();
        }
        
        private void LoadSettingsToUI()
        {
            TxtProjectName.Text = _settings.ProjectName;
            TxtCurrentVersion.Text = _settings.CurrentVersion;
            TxtSourcePath.Text = _settings.SourcePath;
            TxtBuildCommand.Text = _settings.BuildCommand;
            TxtDevelopmentPath.Text = _settings.DevelopmentPath;
            TxtTestPath.Text = _settings.TestPath;
            TxtProductionPath.Text = _settings.ProductionPath;
        }
        
        private void SaveSettingsFromUI()
        {
            _settings.ProjectName = TxtProjectName.Text;
            _settings.SourcePath = TxtSourcePath.Text;
            _settings.BuildCommand = TxtBuildCommand.Text;
            _settings.DevelopmentPath = TxtDevelopmentPath.Text;
            _settings.TestPath = TxtTestPath.Text;
            _settings.ProductionPath = TxtProductionPath.Text;
            
            // Update config settings list
            _settings.ConfigSettings.Clear();
            foreach (ConfigSetting setting in _configSettings)
            {
                _settings.ConfigSettings.Add(setting);
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
        
        private void BtnBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            var result = dialog.ShowDialog();
            
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TxtSourcePath.Text = dialog.SelectedPath;
            }
        }
        
        private void BtnBrowseDev_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            var result = dialog.ShowDialog();
            
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TxtDevelopmentPath.Text = dialog.SelectedPath;
            }
        }
        
        private void BtnBrowseTest_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            var result = dialog.ShowDialog();
            
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TxtTestPath.Text = dialog.SelectedPath;
            }
        }
        
        private void BtnBrowseProd_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            var result = dialog.ShowDialog();
            
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TxtProductionPath.Text = dialog.SelectedPath;
            }
        }
        
        #endregion
        
        #region Config Settings Handlers
        
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
            if (sender is Button button && button.Tag is ConfigSetting setting)
            {
                _configSettings.Remove(setting);
            }
        }
        
        #endregion
        
        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUI();
        }
        
        #region Deployment Handlers
        
        private async void BtnDeployToDev_Click(object sender, RoutedEventArgs e)
        {
            // Prompt for version increment
            var versionWindow = new VersionIncrementWindow(_settings.CurrentVersion)
            {
                Owner = this
            };
            if (versionWindow.ShowDialog() != true)
            {
                return;
            }
            
            // Get the new version
            string newVersion = versionWindow.NewVersion;
            
            LogMessage($"Building project with new version: {newVersion}");
            
            // Perform build with new version
            bool buildSuccess = await Task.Run(() => _deploymentManager?.BuildProject(newVersion).Result ?? false);
            
            if (!buildSuccess)
            {
                LogMessage("Build failed. Deployment aborted.");
                return;
            }
            
            LogMessage("Build completed successfully.");
            
            // Update version display
            TxtCurrentVersion.Text = newVersion;
            
            // Deploy to development
            LogMessage("Deploying to development environment...");
            
            bool deploySuccess = await Task.Run(() => _deploymentManager?.DeployToDevelopment() ?? false);
            
            if (deploySuccess)
            {
                LogMessage("Deployment to development completed successfully.");
            }
            else
            {
                LogMessage("Deployment to development failed.");
            }
        }
        
        private async void BtnDeployToTest_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("Deploying to test environment...");
            
            bool deploySuccess = await Task.Run(() => _deploymentManager?.DeployToTest() ?? false);
            
            if (deploySuccess)
            {
                LogMessage("Deployment to test completed successfully.");
            }
            else
            {
                LogMessage("Deployment to test failed.");
            }
        }
        
        private async void BtnDeployToProd_Click(object sender, RoutedEventArgs e)
        {
            // Confirm deployment to production
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to deploy to production?",
                "Confirm Production Deployment",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
            
            LogMessage("Deploying to production environment...");
            
            bool deploySuccess = await Task.Run(() => _deploymentManager?.DeployToProduction() ?? false);
            
            if (deploySuccess)
            {
                LogMessage("Deployment to production completed successfully.");
            }
            else
            {
                LogMessage("Deployment to production failed.");
            }
        }
        
        #endregion
    }
}