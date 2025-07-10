# ShopBoss Complete Installation Script
# This script provides a complete automated installation of ShopBoss
# Run as Administrator

param(
    [string]$InstallPath = "C:\ShopBoss",
    [string]$ServiceName = "ShopBoss",
    [int]$Port = 5000,
    [switch]$SkipFirewall,
    [switch]$Force,
    [switch]$Help
)

if ($Help) {
    Write-Host "ShopBoss Installation Script" -ForegroundColor Green
    Write-Host "============================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage: .\install-shopboss.ps1 [OPTIONS]" -ForegroundColor White
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  -InstallPath <path>    Installation directory (default: C:\ShopBoss)" -ForegroundColor White
    Write-Host "  -ServiceName <name>    Windows service name (default: ShopBoss)" -ForegroundColor White
    Write-Host "  -Port <number>         HTTP port (default: 5000)" -ForegroundColor White
    Write-Host "  -SkipFirewall          Skip firewall configuration" -ForegroundColor White
    Write-Host "  -Force                 Force reinstallation" -ForegroundColor White
    Write-Host "  -Help                  Show this help message" -ForegroundColor White
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  .\install-shopboss.ps1" -ForegroundColor White
    Write-Host "  .\install-shopboss.ps1 -InstallPath 'D:\ShopBoss' -Port 8080" -ForegroundColor White
    Write-Host "  .\install-shopboss.ps1 -Force -SkipFirewall" -ForegroundColor White
    Write-Host ""
    exit 0
}

# Check if running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator. Please restart PowerShell as Administrator and try again."
    Write-Host "Right-click on PowerShell and select 'Run as administrator'" -ForegroundColor Yellow
    exit 1
}

Write-Host "ShopBoss Complete Installation" -ForegroundColor Green
Write-Host "==============================" -ForegroundColor Green
Write-Host ""

# Display installation parameters
Write-Host "Installation Parameters:" -ForegroundColor Cyan
Write-Host "  Install Path: $InstallPath" -ForegroundColor White
Write-Host "  Service Name: $ServiceName" -ForegroundColor White
Write-Host "  HTTP Port: $Port" -ForegroundColor White
Write-Host "  Skip Firewall: $SkipFirewall" -ForegroundColor White
Write-Host "  Force Reinstall: $Force" -ForegroundColor White
Write-Host ""

# Check system requirements
Write-Host "Checking system requirements..." -ForegroundColor Cyan

# Check .NET runtime
try {
    $dotnetVersion = dotnet --version 2>$null
    if ($dotnetVersion) {
        Write-Host "✓ .NET Runtime found: $dotnetVersion" -ForegroundColor Green
    } else {
        Write-Warning ".NET Runtime not found. Please install .NET 8.0 Runtime."
        Write-Host "Download from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Warning ".NET Runtime not found. Please install .NET 8.0 Runtime."
    Write-Host "Download from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    exit 1
}

# Check available disk space
$drive = Split-Path -Qualifier $InstallPath
$driveInfo = Get-WmiObject -Class Win32_LogicalDisk -Filter "DeviceID='$drive'"
if ($driveInfo.FreeSpace -lt 1GB) {
    Write-Warning "Insufficient disk space. At least 1GB free space is required."
    exit 1
}
Write-Host "✓ Sufficient disk space available" -ForegroundColor Green

# Check port availability
$portInUse = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
if ($portInUse -and -not $Force) {
    Write-Warning "Port $Port is already in use. Use -Force to override or choose a different port."
    exit 1
}
Write-Host "✓ Port $Port is available" -ForegroundColor Green

Write-Host ""

# Get current script directory and project structure
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

# Check if we're in the correct directory structure
$projectFile = Join-Path $projectRoot "src\ShopBoss.Web\ShopBoss.Web.csproj"
if (-not (Test-Path $projectFile)) {
    Write-Error "ShopBoss project not found. Please run this script from the project root directory."
    Write-Host "Expected project file: $projectFile" -ForegroundColor Gray
    exit 1
}

# Build and publish the application
Write-Host "Building and publishing ShopBoss..." -ForegroundColor Cyan
$publishDir = Join-Path $projectRoot "publish"

# Clean previous publish directory
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}

# Publish the application
$publishCommand = "dotnet publish `"$projectFile`" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o `"$publishDir`""
Write-Host "Command: $publishCommand" -ForegroundColor Gray

Invoke-Expression $publishCommand

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish application. Please check the build output."
    exit 1
}

Write-Host "✓ Application published successfully" -ForegroundColor Green

# Check if service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    if ($Force) {
        Write-Host "Removing existing service..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 2
    } else {
        Write-Warning "Service '$ServiceName' already exists. Use -Force to reinstall."
        exit 1
    }
}

# Create installation directory
Write-Host "Creating installation directory..." -ForegroundColor Cyan
if (Test-Path $InstallPath) {
    if ($Force) {
        Remove-Item -Path $InstallPath -Recurse -Force
    } else {
        Write-Warning "Installation directory already exists. Use -Force to overwrite."
        exit 1
    }
}

New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null

# Copy application files
Write-Host "Installing application files..." -ForegroundColor Cyan
Copy-Item -Path "$publishDir\*" -Destination $InstallPath -Recurse -Force

# Copy tools directory if it exists
$toolsSource = Join-Path $projectRoot "tools"
if (Test-Path $toolsSource) {
    Copy-Item -Path $toolsSource -Destination $InstallPath -Recurse -Force
    Write-Host "✓ Tools directory copied" -ForegroundColor Green
}

# Create data directories
$dataDirectories = @("Backups", "Logs", "temp\uploads")
foreach ($dir in $dataDirectories) {
    $fullPath = Join-Path $InstallPath $dir
    if (-not (Test-Path $fullPath)) {
        New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
    }
}

# Install Windows Service
Write-Host "Installing Windows Service..." -ForegroundColor Cyan
$exePath = Join-Path $InstallPath "ShopBoss.Web.exe"
$displayName = "ShopBoss Manufacturing System"
$description = "ShopBoss v2 Manufacturing Workflow Management System"

$serviceCreateCmd = "sc.exe create `"$ServiceName`" binPath=`"$exePath`" DisplayName=`"$displayName`" start=auto"
Invoke-Expression $serviceCreateCmd

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create service."
    exit 1
}

# Set service description and failure recovery
sc.exe description $ServiceName $description | Out-Null
sc.exe failure $ServiceName reset=0 actions=restart/60000/restart/60000/restart/60000 | Out-Null

Write-Host "✓ Windows Service installed" -ForegroundColor Green

# Configure firewall
if (-not $SkipFirewall) {
    Write-Host "Configuring Windows Firewall..." -ForegroundColor Cyan
    $firewallRuleName = "ShopBoss Web Server"
    
    # Remove existing rule if it exists
    $existingRule = Get-NetFirewallRule -DisplayName $firewallRuleName -ErrorAction SilentlyContinue
    if ($existingRule) {
        Remove-NetFirewallRule -DisplayName $firewallRuleName
    }
    
    # Create new rule
    New-NetFirewallRule -DisplayName $firewallRuleName -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -Description "Allow inbound traffic for ShopBoss Web Server" | Out-Null
    Write-Host "✓ Firewall configured for port $Port" -ForegroundColor Green
}

# Start the service
Write-Host "Starting ShopBoss service..." -ForegroundColor Cyan
Start-Service -Name $ServiceName

# Wait and verify service status
Start-Sleep -Seconds 5
$service = Get-Service -Name $ServiceName
if ($service.Status -eq "Running") {
    Write-Host "✓ ShopBoss service is running!" -ForegroundColor Green
} else {
    Write-Warning "Service was created but failed to start. Status: $($service.Status)"
    Write-Host "Check Windows Event Log for error details." -ForegroundColor Yellow
}

# Installation complete
Write-Host ""
Write-Host "Installation Complete!" -ForegroundColor Green
Write-Host "=====================" -ForegroundColor Green
Write-Host ""
Write-Host "Service Information:" -ForegroundColor Yellow
Write-Host "  Name: $ServiceName" -ForegroundColor White
Write-Host "  Display Name: $displayName" -ForegroundColor White
Write-Host "  Status: $($service.Status)" -ForegroundColor White
Write-Host "  Install Path: $InstallPath" -ForegroundColor White
Write-Host ""
Write-Host "Web Interface:" -ForegroundColor Yellow
Write-Host "  Local: http://localhost:$Port" -ForegroundColor White
Write-Host "  Network: http://[server-ip]:$Port" -ForegroundColor White
Write-Host ""
Write-Host "Management Commands:" -ForegroundColor Yellow
Write-Host "  Start Service: Start-Service -Name $ServiceName" -ForegroundColor White
Write-Host "  Stop Service: Stop-Service -Name $ServiceName" -ForegroundColor White
Write-Host "  Check Status: Get-Service -Name $ServiceName" -ForegroundColor White
Write-Host ""
Write-Host "The service will start automatically when Windows starts." -ForegroundColor Cyan
Write-Host ""

# Test web interface
Write-Host "Testing web interface..." -ForegroundColor Cyan
try {
    $response = Invoke-WebRequest -Uri "http://localhost:$Port" -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Host "✓ Web interface is responding" -ForegroundColor Green
    }
} catch {
    Write-Warning "Web interface test failed. The service may still be starting up."
    Write-Host "Try accessing http://localhost:$Port in a few minutes." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Installation completed successfully!" -ForegroundColor Green