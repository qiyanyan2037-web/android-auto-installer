# Android 自动安装工具

一个用于 Android APK 安装和文件传输流程的 Windows 图形界面小工具。

## 下载和使用

如果只想直接使用，不需要下载源码或自己构建：

1. 下载仓库里的 [`自动安装.exe`](./自动安装.exe)。
2. 准备 Android SDK Platform-Tools，确保电脑能找到 `adb.exe`。程序会按下面顺序查找 ADB：
   - `自动安装.exe` 同目录下的 `adb.exe`；
   - `C:\app\platform-tools\adb.exe`；
   - 系统 `PATH` 里的 `adb.exe`。
3. 手机开启 USB 调试，并在手机上允许这台电脑调试。
4. 双击运行 `自动安装.exe`，选择 APK、文件或文件夹后按界面提示操作。

说明：仓库只提供 `自动安装.exe`，不包含 `adb.exe`、`AdbWinApi.dll` 和 `AdbWinUsbApi.dll`。请自行从 Google 官方 Android SDK Platform-Tools 获取 ADB 环境。

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

- 当前仓库单独提供 `自动安装.exe`，方便直接下载使用。
- 重新构建产生的其它 `.exe` 文件默认被 git 忽略。
- APK/ZIP 安装包和日志文件已被 git 忽略。
- APK 版本检测和安装仍然依赖 ADB。

---

# Android Auto Installer

A small Windows GUI tool for Android APK installation and file transfer workflows.

## Download and Use

If you only want to run the tool, you do not need to download the source code or build it yourself:

1. Download [`自动安装.exe`](./自动安装.exe) from this repository.
2. Prepare Android SDK Platform-Tools and make sure the computer can find `adb.exe`. The app searches for ADB in this order:
   - `adb.exe` next to `自动安装.exe`;
   - `C:\app\platform-tools\adb.exe`;
   - `adb.exe` from the system `PATH`.
3. Enable USB debugging on the phone, then allow this computer when prompted on the phone.
4. Double-click `自动安装.exe`, select APKs, files, or folders, and follow the UI.

Note: this repository only provides `自动安装.exe`. It does not include `adb.exe`, `AdbWinApi.dll`, or `AdbWinUsbApi.dll`. Please get ADB from Google's official Android SDK Platform-Tools.

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

- This repository provides `自动安装.exe` as a direct download.
- Other rebuilt `.exe` files are ignored by git by default.
- APK/ZIP packages and logs are ignored by git.
- ADB is still used for APK version detection and installation.

