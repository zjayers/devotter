# Contributing to Devotter

Thank you for your interest in contributing to Devotter!

## Project Structure

The application follows a simple WPF structure:

```
devotter/
├── App.xaml                 # Application definition
├── App.xaml.cs              # Application entry point
├── MainWindow.xaml          # Main application window UI
├── MainWindow.xaml.cs       # Main window code-behind
├── VersionIncrementWindow.xaml    # Version selection dialog
├── VersionIncrementWindow.xaml.cs # Version dialog code-behind
├── Models/                  # Core business logic
│   ├── AppSettings.cs       # Application settings model
│   ├── DeploymentManager.cs # Deployment logic
│   └── VersionInfo.cs       # Version management
└── Properties/              # Assembly information
```

## Core Concepts

1. **Settings Management**: User preferences stored in AppData folder
2. **Deployment Pipeline**: Development → Test → Production 
3. **Version Management**: Semantic versioning with major.minor.patch
4. **Config Transformation**: Update app.config/web.config for each environment

## Development Workflow

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## Coding Guidelines

- Follow C# coding conventions
- Use WPF MVVM patterns where appropriate
- Separate UI code from business logic
- Add comments for complex logic
- Write unit tests for new functionality

## Building the Project

1. Open the solution in Visual Studio
2. Restore NuGet packages
3. Build the solution

## Testing

1. Run unit tests with Visual Studio Test Explorer
2. Manually test deployments with sample projects