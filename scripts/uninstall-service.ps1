# ShopBoss Windows Service Uninstallation Script
# This script removes the ShopBoss Windows Service
# Run as Administrator

param(
    [string]$ServiceName = "ShopBoss",
    [string]$InstallPath = "C:\ShopBoss",
    [switch]$RemoveFiles,
    [switch]$Force
)

# Check if running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator. Please restart PowerShell as Administrator and try again."
    exit 1
}

Write-Host "ShopBoss Windows Service Uninstallation" -ForegroundColor Red
Write-Host "=======================================" -ForegroundColor Red

# Check if service exists
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $service) {
    Write-Warning "Service '$ServiceName' does not exist."
    if ($RemoveFiles -and (Test-Path $InstallPath)) {
        Write-Host "Removing installation files..." -ForegroundColor Yellow
        Remove-Item -Path $InstallPath -Recurse -Force
        Write-Host "Installation files removed." -ForegroundColor Green
    }
    exit 0
}

# Confirm uninstallation
if (-not $Force) {
    Write-Host "This will remove the ShopBoss Windows Service." -ForegroundColor Yellow
    if ($RemoveFiles) {
        Write-Host "Installation files will also be removed from: $InstallPath" -ForegroundColor Yellow
    }
    $confirmation = Read-Host "Are you sure you want to continue? (y/N)"
    if ($confirmation -notmatch "^[Yy]") {
        Write-Host "Uninstallation cancelled." -ForegroundColor Cyan
        exit 0
    }
}

# Stop service
Write-Host "Stopping ShopBoss service..." -ForegroundColor Yellow
if ($service.Status -eq "Running") {
    Stop-Service -Name $ServiceName -Force
    Write-Host "Service stopped." -ForegroundColor Green
} else {
    Write-Host "Service was not running." -ForegroundColor Gray
}

# Remove service
Write-Host "Removing Windows Service..." -ForegroundColor Yellow
sc.exe delete $ServiceName | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "Service removed successfully." -ForegroundColor Green
} else {
    Write-Warning "Failed to remove service. It may have already been removed."
}

# Remove firewall rule
Write-Host "Removing Windows Firewall rule..." -ForegroundColor Yellow
$firewallRuleName = "ShopBoss Web Server"
$existingRule = Get-NetFirewallRule -DisplayName $firewallRuleName -ErrorAction SilentlyContinue
if ($existingRule) {
    Remove-NetFirewallRule -DisplayName $firewallRuleName
    Write-Host "Firewall rule removed." -ForegroundColor Green
} else {
    Write-Host "Firewall rule not found." -ForegroundColor Gray
}

# Remove installation files if requested
if ($RemoveFiles -and (Test-Path $InstallPath)) {
    Write-Host "Removing installation files..." -ForegroundColor Yellow
    try {
        Remove-Item -Path $InstallPath -Recurse -Force
        Write-Host "Installation files removed." -ForegroundColor Green
    } catch {
        Write-Warning "Failed to remove some installation files. They may be in use."
        Write-Host "Path: $InstallPath" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Uninstallation completed!" -ForegroundColor Green