$ErrorActionPreference = "Stop"

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$Source = Join-Path $ProjectDir "AutoInstaller.cs"
$Icon = Join-Path $ProjectDir "app_icon.ico"
$Output = Join-Path $ProjectDir "自动安装.exe"

if (-not (Test-Path -LiteralPath $Csc)) {
    throw "C# compiler not found: $Csc"
}

if (-not (Test-Path -LiteralPath $Source)) {
    throw "Source file not found: $Source"
}

$args = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/codepage:65001",
    "/out:$Output",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll"
)

if (Test-Path -LiteralPath $Icon) {
    $args += "/win32icon:$Icon"
}

$args += $Source

& $Csc @args

Write-Host "Built: $Output"
