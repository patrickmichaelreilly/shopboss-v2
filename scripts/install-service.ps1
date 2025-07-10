# ShopBoss Windows Service Installation Script
# This script installs ShopBoss as a Windows Service
# Run as Administrator

param(
    [string]$InstallPath = "C:\ShopBoss",
    [string]$ServiceName = "ShopBoss",
    [string]$DisplayName = "ShopBoss Manufacturing System",
    [string]$Description = "ShopBoss v2 Manufacturing Workflow Management System",
    [int]$Port = 5000,
    [switch]$Force
)

# Check if running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator. Please restart PowerShell as Administrator and try again."
    exit 1
}

Write-Host "ShopBoss Windows Service Installation" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green

# Check if service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService -and -not $Force) {
    Write-Warning "Service '$ServiceName' already exists. Use -Force to reinstall."
    exit 1
}

# Stop and remove existing service if it exists
if ($existingService) {
    Write-Host "Stopping existing service..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    
    Write-Host "Removing existing service..." -ForegroundColor Yellow
    sc.exe delete $ServiceName | Out-Null
    
    # Wait for service to be fully removed
    Start-Sleep -Seconds 2
}

# Create installation directory
if (-not (Test-Path $InstallPath)) {
    Write-Host "Creating installation directory: $InstallPath" -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

# Get current script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$publishDir = Join-Path $projectRoot "src\ShopBoss.Web\bin\Release\net8.0\win-x64\publish"

# Check if published files exist
if (-not (Test-Path $publishDir)) {
    Write-Host "Published files not found. Building and publishing..." -ForegroundColor Yellow
    
    $projectFile = Join-Path $projectRoot "src\ShopBoss.Web\ShopBoss.Web.csproj"
    
    # Build and publish
    dotnet publish $projectFile -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o $publishDir
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish application. Please check the build output."
        exit 1
    }
}

# Copy files to installation directory
Write-Host "Copying application files to $InstallPath..." -ForegroundColor Cyan
Copy-Item -Path "$publishDir\*" -Destination $InstallPath -Recurse -Force

# Ensure tools directory exists and copy importer
$toolsSource = Join-Path $projectRoot "tools"
$toolsDestination = Join-Path $InstallPath "tools"
if (Test-Path $toolsSource) {
    Write-Host "Copying tools directory..." -ForegroundColor Cyan
    Copy-Item -Path $toolsSource -Destination $InstallPath -Recurse -Force
}

# Create service
$exePath = Join-Path $InstallPath "ShopBoss.Web.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "ShopBoss.Web.exe not found at $exePath"
    exit 1
}

Write-Host "Installing Windows Service..." -ForegroundColor Cyan
$serviceCreateCmd = "sc.exe create `"$ServiceName`" binPath=`"$exePath`" DisplayName=`"$DisplayName`" start=auto"
Write-Host "Command: $serviceCreateCmd" -ForegroundColor Gray
Invoke-Expression $serviceCreateCmd

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create service. Please check the output above."
    exit 1
}

# Set service description
sc.exe description $ServiceName $Description | Out-Null

# Set service to restart automatically on failure
sc.exe failure $ServiceName reset=0 actions=restart/60000/restart/60000/restart/60000 | Out-Null

# Configure firewall rule
Write-Host "Configuring Windows Firewall..." -ForegroundColor Cyan
$firewallRuleName = "ShopBoss Web Server"
$existingRule = Get-NetFirewallRule -DisplayName $firewallRuleName -ErrorAction SilentlyContinue
if ($existingRule) {
    Remove-NetFirewallRule -DisplayName $firewallRuleName
}

New-NetFirewallRule -DisplayName $firewallRuleName -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -Description "Allow inbound traffic for ShopBoss Web Server" | Out-Null

# Start service
Write-Host "Starting ShopBoss service..." -ForegroundColor Cyan
Start-Service -Name $ServiceName

# Wait a moment and check service status
Start-Sleep -Seconds 5
$service = Get-Service -Name $ServiceName
if ($service.Status -eq "Running") {
    Write-Host "SUCCESS: ShopBoss service is running!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Service Details:" -ForegroundColor Yellow
    Write-Host "  Name: $ServiceName" -ForegroundColor White
    Write-Host "  Display Name: $DisplayName" -ForegroundColor White
    Write-Host "  Status: $($service.Status)" -ForegroundColor White
    Write-Host "  Install Path: $InstallPath" -ForegroundColor White
    Write-Host "  Web Interface: http://localhost:$Port" -ForegroundColor White
    Write-Host "  Network Access: http://[server-ip]:$Port" -ForegroundColor White
    Write-Host ""
    Write-Host "The ShopBoss service will start automatically when Windows starts." -ForegroundColor Cyan
} else {
    Write-Warning "Service was created but failed to start. Status: $($service.Status)"
    Write-Host "Check Windows Event Log for error details." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Installation completed!" -ForegroundColor Green