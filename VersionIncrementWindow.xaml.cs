using System;
using System.Windows;
using devotter.Models;

namespace devotter
{
    public partial class VersionIncrementWindow : Window
    {
        private VersionInfo _versionInfo;
        
        public string NewVersion { get; private set; }
        
        public VersionIncrementWindow(string currentVersion)
        {
            InitializeComponent();
            
            _versionInfo = new VersionInfo(currentVersion);
            TxtCurrentVersion.Text = currentVersion;
            NewVersion = currentVersion;
            TxtNewVersion.Text = NewVersion;
        }
        
        private void BtnMajor_Click(object sender, RoutedEventArgs e)
        {
            NewVersion = _versionInfo.IncrementMajor();
            TxtNewVersion.Text = NewVersion;
        }
        
        private void BtnMinor_Click(object sender, RoutedEventArgs e)
        {
            NewVersion = _versionInfo.IncrementMinor();
            TxtNewVersion.Text = NewVersion;
        }
        
        private void BtnPatch_Click(object sender, RoutedEventArgs e)
        {
            NewVersion = _versionInfo.IncrementPatch();
            TxtNewVersion.Text = NewVersion;
        }
        
        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}