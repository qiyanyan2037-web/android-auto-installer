# Android Auto Installer

A small Windows GUI tool for Android APK installation and file transfer workflows.

## Features

- Detects connected Android devices through ADB.
- Reads APK package name, versionCode, and versionName.
- Smart APK handling:
  - install when the app is not installed;
  - upgrade when the APK versionCode is higher;
  - copy the APK when the installed version is the same or newer.
- Supports selecting one file, multiple files, or a whole folder.
- Copies non-APK files to a selected Android directory.
- Lets you browse Android folders/files through ADB.
- Supports exporting Android files/folders to the PC.
- Remembers the last local source folder, Android target folder, and export folder.

## Requirements

- Windows
- .NET Framework 4.x
- Android USB debugging enabled
- Android SDK Platform-Tools

This repository does not include Google's platform-tools binaries. Download them from Google and place these files next to the built executable:

- `adb.exe`
- `AdbWinApi.dll`
- `AdbWinUsbApi.dll`

## Build

Run PowerShell in this folder:

```powershell
.\build.ps1
```

The build script uses the Windows .NET Framework C# compiler:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

## Notes

- The generated `自动安装.exe` is ignored by git.
- APK/ZIP packages and logs are ignored by git.
- ADB is still used for APK version detection and installation.

