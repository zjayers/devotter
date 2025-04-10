# Devotter

A simple deployment tool for Windows applications to manage development, test, and production environments.

## Features

- **Environment Management**: Configure paths for Development, Test, and Production environments
- **Version Control**: Automatically increment major, minor, or patch versions
- **Build Integration**: Execute build commands before deployment
- **Configuration Management**: Automatically update application configuration for each environment
- **Deployment Pipeline**: Simple one-click deployment between environments

## Requirements

- Windows 10/11
- .NET 8.0 Runtime

## Usage

1. Set up your project settings, including the source path and build command
2. Configure your deployment paths for Development, Test, and Production
3. Add any configuration keys that need different values per environment
4. Use the Deployment tab to deploy your application:
   - Deploy to Development (includes build and version increment)
   - Deploy to Test (copies from Development to Test)
   - Deploy to Production (copies from Test to Production)

## Configuration Management

The tool automatically updates *.config files in your application, changing the appSettings values based on your environment configuration.

## Building from Source

1. Clone this repository
2. Open the solution in Visual Studio 2022
3. Build the solution (Ctrl+Shift+B)
4. Run the application (F5)

## License

This project is licensed under the MIT License - see the LICENSE file for details.