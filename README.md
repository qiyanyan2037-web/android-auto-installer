# Android 自动安装工具

一个用于 Android APK 安装和文件传输流程的 Windows 图形界面小工具。

## 功能

- 通过 ADB 检测已连接的 Android 设备。
- 读取 APK 的包名、versionCode 和 versionName。
- 智能处理 APK：
  - 设备未安装该应用时自动安装；
  - APK versionCode 更高时自动升级；
  - 已安装版本相同或更新时，将 APK 复制到指定目录。
- 支持选择单个文件、多个文件或整个文件夹。
- 支持将非 APK 文件复制到指定的 Android 目录。
- 支持通过 ADB 浏览 Android 文件夹和文件。
- 支持将 Android 文件或文件夹导出到电脑。
- 自动记住上次使用的本地源目录、Android 目标目录和导出目录。

## 运行要求

- Windows
- .NET Framework 4.x
- Android 已开启 USB 调试
- Android SDK Platform-Tools

本仓库不包含 Google 的 platform-tools 二进制文件。请从 Google 官方下载，并将以下文件放到生成的可执行文件旁边：

- `adb.exe`
- `AdbWinApi.dll`
- `AdbWinUsbApi.dll`

## 构建

在当前文件夹中运行 PowerShell：

```powershell
.\build.ps1
```

构建脚本会使用 Windows .NET Framework 的 C# 编译器：

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

## 说明

- 生成的 `自动安装.exe` 已被 git 忽略。
- APK/ZIP 安装包和日志文件已被 git 忽略。
- APK 版本检测和安装仍然依赖 ADB。

---

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

