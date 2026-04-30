# Android 自动安装工具

一个用于 Android APK 安装和文件传输流程的 Windows 图形界面小工具。

本工具和文档均由 GPT-5.5 开发。

## 最新更新（2026-04-30）

- 新增“解锁 BL”功能：自动执行 `adb reboot bootloader` 和 `fastboot flashing unlock`，并在操作前提示解锁会清空手机数据。
- 新增“刷入修补 boot”功能：选择修补好的 `.img` 文件后，自动进入 fastboot、刷入 boot 分区并重启手机。
- 新增 `fastboot.exe` 自动查找：优先查找程序同目录，其次查找 `C:\app\platform-tools\fastboot.exe`，最后查找系统 `PATH`。
- 优化日志框颜色：成功信息显示绿色，错误/失败显示红色，警告提示显示橙色，操作流程显示蓝色，方便快速判断执行状态。

> 注意：解锁 BL 会清空手机数据；刷入错误的 boot 镜像可能导致手机无法正常启动。请确认已备份数据，并使用当前机型、当前系统版本对应的 boot 镜像。

## 一、直接使用 `自动安装.exe`

这个方式适合只想下载后直接运行的用户，不需要下载源码，也不需要自己构建。

### 下载

1. 打开本仓库首页。
2. 点击根目录里的 [`自动安装.exe`](./自动安装.exe)。
3. 在 GitHub 文件页面中点击下载按钮，或点击 `View raw` / `Download raw file` 下载。
4. 下载完成后，把 `自动安装.exe` 放到你想运行的位置。

### 使用前准备

本仓库只提供 `自动安装.exe`，不包含 ADB 文件。请自行准备 Android SDK Platform-Tools。

程序会按下面顺序查找 `adb.exe`：

1. `自动安装.exe` 同目录下的 `adb.exe`
2. `C:\app\platform-tools\adb.exe`
3. 系统 `PATH` 里的 `adb.exe`

你可以任选一种方式配置 ADB。常见做法是安装 Android SDK Platform-Tools，然后把 platform-tools 目录加入系统 `PATH`。

如需使用“解锁 BL”或“刷入修补 boot”功能，还需要准备 `fastboot.exe`。程序会按下面顺序查找：

1. `自动安装.exe` 同目录下的 `fastboot.exe`
2. `C:\app\platform-tools\fastboot.exe`
3. 系统 `PATH` 里的 `fastboot.exe`

### 使用步骤

1. 手机开启 USB 调试。
2. 用数据线连接手机和电脑。
3. 手机弹出调试授权时，选择允许这台电脑调试。
4. 双击运行 `自动安装.exe`。
5. 在工具里选择 APK、文件或文件夹，然后按界面提示安装、复制或导出文件。

## 二、使用 `构建` 文件夹自行构建

这个方式适合想查看源码、修改程序，或自己重新生成 `自动安装.exe` 的用户。

### 下载

1. 打开本仓库首页。
2. 点击绿色 `Code` 按钮。
3. 选择 `Download ZIP` 下载整个项目，或使用 git clone 克隆仓库。
4. 解压后可以看到根目录下的 `构建` 文件夹。

`构建` 文件夹包含：

- `AutoInstaller.cs`：主程序源码
- `build.ps1`：构建脚本
- `app_icon.ico`：程序图标
- `app_icon.png`：图标源图
- `background.png`：界面背景图

### 构建要求

- Windows
- .NET Framework 4.x
- Windows 自带的 .NET Framework C# 编译器

构建脚本默认使用：

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

### 构建步骤

在项目根目录打开 PowerShell，运行：

```powershell
.\构建\build.ps1
```

构建完成后，会在项目根目录生成或更新：

```text
自动安装.exe
```

重新构建只会生成工具本体，不会生成或附带 `adb.exe`、`AdbWinApi.dll`、`AdbWinUsbApi.dll`。运行工具时仍然需要按上面的说明自行准备 ADB 环境。

## 功能概览

- 通过 ADB 检测已连接的 Android 设备。
- 读取 APK 的包名、versionCode 和 versionName。
- 根据设备已安装版本智能选择安装、升级或复制 APK。
- 支持选择单个文件、多个文件或整个文件夹。
- 支持将非 APK 文件复制到指定 Android 目录。
- 支持通过 ADB 浏览 Android 文件夹和文件。
- 支持将 Android 文件或文件夹导出到电脑。
- 支持一键进入 fastboot 并执行 BL 解锁流程。
- 支持选择修补后的 boot 镜像并刷入 boot 分区。
- 支持按日志类型显示不同颜色，区分成功、错误、警告和操作流程。
- 自动记住上次使用的本地源目录、Android 目标目录和导出目录。

## 说明

- 根目录的 `自动安装.exe` 是给普通用户直接下载使用的版本。
- `构建` 文件夹是给开发者或需要自行构建的用户使用的版本。
- APK/ZIP 安装包和日志文件不会上传到仓库。
- ADB 版本检测、APK 安装、文件复制和导出都依赖 ADB；BL 解锁和刷入 boot 依赖 fastboot。
