<#
.SYNOPSIS
    Unregisters AIHelper from the Windows 11 context menu and removes the Sparse Package.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$TargetDirectory = ""
)

$ErrorActionPreference = "Continue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  AIHelper - Windows 11 Context Menu Unregistration" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Unregister Sparse Package
Write-Host "[1/2] Removing MSIX Sparse Package (chgblog.AIHelper)..." -ForegroundColor Green
$packages = Get-AppxPackage -Name "*AIHelper*"
if ($packages) {
    foreach ($pkg in $packages) {
        try {
            Remove-AppxPackage -Package $pkg.PackageFullName
            Write-Host "      Removed package: $($pkg.PackageFullName)" -ForegroundColor Green
        }
        catch {
            Write-Warning "Failed to remove package $($pkg.PackageFullName): $($_.Exception.Message)"
        }
    }
}
else {
    Write-Host "      No active AIHelper AppX packages found." -ForegroundColor Yellow
}

# 2. Unregister traditional registry keys
Write-Host "[2/2] Cleaning up traditional registry context menu..." -ForegroundColor Green

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

if (-not [string]::IsNullOrWhiteSpace($TargetDirectory) -and (Test-Path (Join-Path $TargetDirectory "AIHelper.exe"))) {
    $exePath = Join-Path $TargetDirectory "AIHelper.exe"
    Start-Process -FilePath $exePath -ArgumentList "--unregister-context-menu" -Wait -WindowStyle Hidden
}

Write-Host ""
Write-Host "[SUCCESS] AIHelper context menu has been unregistered." -ForegroundColor Cyan
Write-Host ""
