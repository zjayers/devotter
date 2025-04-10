# Building and Packaging Devotter

This document provides instructions for building, debugging, and packaging the Devotter application.

## Prerequisites

- Visual Studio 2022 (or later)
- .NET 8.0 SDK
- Windows 10/11 (for building and running)

## Building the Application

### From Visual Studio

1. Open `devotter.csproj` in Visual Studio
2. Select the desired configuration (Debug/Release)
3. Build the solution (F6 or Build > Build Solution)

### From Command Line

```
dotnet build -c Release
```

## Debugging

1. Set the startup project to `devotter`
2. Press F5 to start debugging
3. The application will start with the debugger attached

## Creating a Release Package

### From Visual Studio

1. Right-click on the project in Solution Explorer
2. Select "Publish..."
3. Follow the wizard to create a self-contained deployment:
   - Select "Folder" as the target
   - Choose "Self-contained" deployment mode
   - Select "Windows" as the target OS
   - Finish the wizard

### From Command Line

For a single-file application:

```
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

## Installation Package

To create an installer:

1. Install the WiX Toolset v3.11 or later
2. Use the following script to create an MSI installer:

```powershell
$version = "1.0.0"
$sourceDir = ".\bin\Release\net8.0-windows\win-x64\publish"
$outputDir = ".\installer"

# Create output directory if it doesn't exist
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir
}

# Create a WiX project
candle.exe -out "$outputDir\devotter.wixobj" -ext WixUIExtension -arch x64 installer.wxs
light.exe -out "$outputDir\Devotter-$version.msi" -ext WixUIExtension "$outputDir\devotter.wixobj"
```

## Versioning

The application follows semantic versioning:

- MAJOR version when making incompatible API changes
- MINOR version when adding functionality in a backward compatible manner
- PATCH version when making backward compatible bug fixes

Version numbers are stored in:
- The project file (devotter.csproj)
- Displayed in the application's status bar

## Testing

Run the unit tests using:

```
dotnet test
```

## Continuous Integration

The project uses GitHub Actions for CI/CD:

- On push to main: Build and run tests
- On tag creation (vX.Y.Z): Build, run tests, and create release artifacts

## Project Structure

- `/Models`: Data models and business logic
- `/MainWindow.xaml`: Main application UI
- `/ProjectEditWindow.xaml`: Project editing UI
- `/VersionIncrementWindow.xaml`: Version increment dialog
- `/Resources`: Icons and resources