# MdReader

MdReader is an offline Markdown library built with .NET MAUI for Windows and Android.

## Add a document

1. Drag any `.md` file into the top-level `Documents` folder. Subfolders are supported.
2. Rebuild the app.

That is all—every Markdown file under `Documents` is embedded and discovered automatically. No catalog or project-file edit is required.

MdReader uses the first `# Heading` as the document title and the first regular paragraph as its library description. If a document has no level-one heading, its filename becomes the title. A subfolder name becomes the document category.

## Run on Windows

```powershell
dotnet build -f net10.0-windows10.0.19041.0
dotnet run -f net10.0-windows10.0.19041.0
```

## Publish to the Windows desktop

```powershell
$mdReaderDesktopFolder = Join-Path ([Environment]::GetFolderPath("Desktop")) "MdReader"

dotnet publish .\MdReader.csproj `
  -f net10.0-windows10.0.19041.0 `
  -c Release `
  -p:RuntimeIdentifierOverride=win-x64 `
  -p:WindowsPackageType=None `
  -p:WindowsAppSDKSelfContained=true `
  -o $mdReaderDesktopFolder
```

Run `MdReader.exe` from the resulting Desktop folder. Keep the whole folder because the app has supporting runtime files.

## Build for Android

```powershell
dotnet build -f net10.0-android
```

To run on Android, select an emulator or connected device in Visual Studio and start the `net10.0-android` target.

## Project structure

- `Documents`: drag-and-drop Markdown library
- `Services/DocumentLibrary.cs`: automatic discovery and Markdown-to-HTML rendering
- `MainPage`: searchable document library
- `ReaderPage`: theme-aware document reader with text-size controls
