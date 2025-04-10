# Devotter - Deployment Manager Tool

Devotter is a Windows application designed to streamline the deployment of .NET applications across multiple environments (Development, Test, Production).

## Features

- **Environment Path Management**: Configure base paths for Development, Test, and Production environments.
- **Project Management**: Add multiple projects to manage their deployments.
- **Version Control**: Increment version numbers (major, minor, patch) when deploying.
- **Build Integration**: Run build commands before deployment.
- **Deployment Workflow**: Enforce deployment progression from Development → Test → Production.
- **Configuration Management**: Automatically update configuration files per environment.
  - Supports both XML (app.config) and JSON (appsettings.json) configuration files.
- **Deployment Status**: Visual indicators for deployed projects.
- **Logging**: Comprehensive logging to file and UI.
- **Project File Integration**: Extract project information from .csproj files.

## Getting Started

1. **Configure Environment Paths**: Set up the base paths for each environment.
2. **Add Projects**: Select project files (.csproj) to manage.
3. **Configure Settings**: Add configuration settings that need to change between environments.
4. **Deploy**: Deploy to Development, then to Test, then to Production as needed.

## Deployment Process

The deployment process follows this workflow:

1. **Development Deployment**:
   - Prompts for version increment (major, minor, patch)
   - Updates version in project file
   - Optionally runs the build command
   - Copies files to the Development environment
   - Updates configuration files

2. **Test Deployment**:
   - Copies files from Development to Test
   - Updates configuration files for Test environment

3. **Production Deployment**:
   - Copies files from Test to Production
   - Updates configuration files for Production environment

## Configuration Management

For each project, you can define configuration settings with environment-specific values:

- **Key Name**: The configuration key
- **Development Value**: Value for Development environment
- **Test Value**: Value for Test environment
- **Production Value**: Value for Production environment

These settings are automatically applied to:
- XML config files (*.config)
- JSON config files (appsettings*.json)

## Requirements

- .NET 8.0
- Windows 10/11

## Installation

1. Download the latest release
2. Run the installer
3. Launch the application

## Building from Source

1. Clone the repository
2. Open the solution in Visual Studio 2022
3. Build the solution

## License

MIT License