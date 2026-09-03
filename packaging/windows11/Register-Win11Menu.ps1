<#
.SYNOPSIS
    Registers AIHelper into the Windows 11 modern top-level File Explorer context menu via MSIX Sparse Package.
.DESCRIPTION
    This script registers the Sparse Package defined by AppxManifest.xml, granting AIHelper
    the required Package Identity to appear directly in the first-level Windows 11 context menu.
.PARAMETER TargetDirectory
    The path to the folder containing AIHelper.exe. Defaults to standard build output or current directory.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$TargetDirectory = ""
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  AIHelper - Windows 11 Top-Level Context Menu Registration" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Check Windows Version
$buildNumber = [int](Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion").CurrentBuild
if ($buildNumber -lt 22000) {
    Write-Warning "This script is designed for Windows 11 (Build 22000+). Current build: $buildNumber."
    Write-Warning "Windows 10 already supports the first-level context menu via traditional registry keys."
}

# 2. Locate AppxManifest.xml
$manifestPath = Join-Path $PSScriptRoot "AppxManifest.xml"
if (-not (Test-Path $manifestPath)) {
    throw "AppxManifest.xml not found at: $manifestPath"
}

# 3. Locate Target AIHelper.exe Directory
if ([string]::IsNullOrWhiteSpace($TargetDirectory)) {
    $candidatePaths = @(
        (Join-Path $PSScriptRoot "..\..\AIHelper\bin\Release\net48"),
        (Join-Path $PSScriptRoot "..\..\AIHelper\bin\Debug\net48"),
        (Get-Location).Path,
        $PSScriptRoot
    )
    foreach ($path in $candidatePaths) {
        $resolved = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($path)
        if (Test-Path (Join-Path $resolved "AIHelper.exe")) {
            $TargetDirectory = (Resolve-Path $resolved).Path
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($TargetDirectory) -or -not (Test-Path (Join-Path $TargetDirectory "AIHelper.exe"))) {
    Write-Warning "Could not automatically locate AIHelper.exe."
    Write-Host "Please specify the directory containing AIHelper.exe using -TargetDirectory, e.g.:" -ForegroundColor Yellow
    Write-Host "  .\Register-Win11Menu.ps1 -TargetDirectory 'C:\Path\To\AIHelper'" -ForegroundColor Yellow
    exit 1
}

Write-Host "[1/3] Target directory: $TargetDirectory" -ForegroundColor Green
Write-Host "[2/3] Registering MSIX Sparse Package..." -ForegroundColor Green

try {
    Add-AppxPackage -Path $manifestPath -Register -ExternalLocation $TargetDirectory
    Write-Host "      MSIX Sparse Package registered successfully." -ForegroundColor Green
}
catch {
    Write-Warning "Sparse package registration failed: $($_.Exception.Message)"
    Write-Host ""
    Write-Host "Tip: If you encounter an 'AllowDevelopmentWithoutDevLicense' or signing policy error," -ForegroundColor Yellow
    Write-Host "     please enable Windows 'Developer Mode' (开发者模式) in Windows Settings:" -ForegroundColor Yellow
    Write-Host "     Settings -> System -> For developers -> Developer Mode: ON" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# 4. Also register traditional registry fallback so both modern and classic menus work
Write-Host "[3/3] Registering traditional registry fallback..." -ForegroundColor Green
$exePath = Join-Path $TargetDirectory "AIHelper.exe"
if (Test-Path $exePath) {
    Start-Process -FilePath $exePath -ArgumentList "--register-context-menu" -Wait -WindowStyle Hidden
}

Write-Host ""
Write-Host "[SUCCESS] AIHelper has been registered to the Windows 11 context menu!" -ForegroundColor Cyan
Write-Host "Right-click on any supported file to see '使用 AIHelper 处理' directly in the first menu." -ForegroundColor Cyan
Write-Host ""
