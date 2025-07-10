# ShopBoss Windows Service Management Script
# This script provides common service management operations
# Run as Administrator

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("start", "stop", "restart", "status", "logs")]
    [string]$Action,
    
    [string]$ServiceName = "ShopBoss",
    [int]$LogLines = 50
)

# Check if running as Administrator for service operations
if ($Action -ne "status" -and $Action -ne "logs") {
    if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
        Write-Error "This script must be run as Administrator for service operations. Please restart PowerShell as Administrator and try again."
        exit 1
    }
}

function Show-ServiceStatus {
    param([string]$ServiceName)
    
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $service) {
        Write-Host "Service '$ServiceName' not found." -ForegroundColor Red
        return $false
    }
    
    Write-Host "ShopBoss Service Status" -ForegroundColor Cyan
    Write-Host "======================" -ForegroundColor Cyan
    Write-Host "Name: $($service.Name)" -ForegroundColor White
    Write-Host "Display Name: $($service.DisplayName)" -ForegroundColor White
    Write-Host "Status: $($service.Status)" -ForegroundColor $(if ($service.Status -eq "Running") { "Green" } else { "Yellow" })
    Write-Host "Start Type: $($service.StartType)" -ForegroundColor White
    
    if ($service.Status -eq "Running") {
        Write-Host "Web Interface: http://localhost:5000" -ForegroundColor Green
        Write-Host "Network Access: http://[server-ip]:5000" -ForegroundColor Green
    }
    
    return $true
}

function Start-ShopBossService {
    param([string]$ServiceName)
    
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $service) {
        Write-Host "Service '$ServiceName' not found." -ForegroundColor Red
        return $false
    }
    
    if ($service.Status -eq "Running") {
        Write-Host "Service is already running." -ForegroundColor Green
        return $true
    }
    
    Write-Host "Starting ShopBoss service..." -ForegroundColor Cyan
    Start-Service -Name $ServiceName
    
    # Wait and check status
    Start-Sleep -Seconds 3
    $service = Get-Service -Name $ServiceName
    if ($service.Status -eq "Running") {
        Write-Host "Service started successfully!" -ForegroundColor Green
        return $true
    } else {
        Write-Host "Service failed to start. Status: $($service.Status)" -ForegroundColor Red
        return $false
    }
}

function Stop-ShopBossService {
    param([string]$ServiceName)
    
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $service) {
        Write-Host "Service '$ServiceName' not found." -ForegroundColor Red
        return $false
    }
    
    if ($service.Status -eq "Stopped") {
        Write-Host "Service is already stopped." -ForegroundColor Yellow
        return $true
    }
    
    Write-Host "Stopping ShopBoss service..." -ForegroundColor Cyan
    Stop-Service -Name $ServiceName -Force
    
    # Wait and check status
    Start-Sleep -Seconds 3
    $service = Get-Service -Name $ServiceName
    if ($service.Status -eq "Stopped") {
        Write-Host "Service stopped successfully!" -ForegroundColor Green
        return $true
    } else {
        Write-Host "Service failed to stop. Status: $($service.Status)" -ForegroundColor Red
        return $false
    }
}

function Restart-ShopBossService {
    param([string]$ServiceName)
    
    Write-Host "Restarting ShopBoss service..." -ForegroundColor Cyan
    
    if (-not (Stop-ShopBossService -ServiceName $ServiceName)) {
        return $false
    }
    
    Start-Sleep -Seconds 2
    
    if (-not (Start-ShopBossService -ServiceName $ServiceName)) {
        return $false
    }
    
    Write-Host "Service restarted successfully!" -ForegroundColor Green
    return $true
}

function Show-ServiceLogs {
    param([string]$ServiceName, [int]$Lines)
    
    Write-Host "ShopBoss Service Logs (Last $Lines entries)" -ForegroundColor Cyan
    Write-Host "==========================================" -ForegroundColor Cyan
    
    # Get Application logs related to ShopBoss
    $logs = Get-WinEvent -FilterHashtable @{LogName='Application'; ProviderName='ShopBoss*'} -MaxEvents $Lines -ErrorAction SilentlyContinue
    
    if ($logs) {
        $logs | Sort-Object TimeCreated -Descending | ForEach-Object {
            $timestamp = $_.TimeCreated.ToString("yyyy-MM-dd HH:mm:ss")
            $level = $_.LevelDisplayName
            $message = $_.Message
            
            $color = switch ($level) {
                "Error" { "Red" }
                "Warning" { "Yellow" }
                "Information" { "White" }
                default { "Gray" }
            }
            
            Write-Host "[$timestamp] $level`: $message" -ForegroundColor $color
        }
    } else {
        Write-Host "No ShopBoss service logs found in Application log." -ForegroundColor Yellow
        Write-Host "Try checking System logs or increase verbosity in appsettings.json" -ForegroundColor Gray
    }
    
    # Also show recent System events for the service
    Write-Host ""
    Write-Host "Recent Service Control Events:" -ForegroundColor Cyan
    Write-Host "=============================" -ForegroundColor Cyan
    
    $serviceLogs = Get-WinEvent -FilterHashtable @{LogName='System'; ID=7034,7035,7036} -MaxEvents 10 -ErrorAction SilentlyContinue | Where-Object { $_.Message -like "*$ServiceName*" }
    
    if ($serviceLogs) {
        $serviceLogs | Sort-Object TimeCreated -Descending | ForEach-Object {
            $timestamp = $_.TimeCreated.ToString("yyyy-MM-dd HH:mm:ss")
            $message = $_.Message
            Write-Host "[$timestamp] $message" -ForegroundColor White
        }
    } else {
        Write-Host "No recent service control events found." -ForegroundColor Gray
    }
}

# Main execution
Write-Host "ShopBoss Service Management" -ForegroundColor Green
Write-Host "===========================" -ForegroundColor Green

switch ($Action) {
    "start" {
        Start-ShopBossService -ServiceName $ServiceName | Out-Null
        Show-ServiceStatus -ServiceName $ServiceName | Out-Null
    }
    "stop" {
        Stop-ShopBossService -ServiceName $ServiceName | Out-Null
        Show-ServiceStatus -ServiceName $ServiceName | Out-Null
    }
    "restart" {
        Restart-ShopBossService -ServiceName $ServiceName | Out-Null
        Show-ServiceStatus -ServiceName $ServiceName | Out-Null
    }
    "status" {
        Show-ServiceStatus -ServiceName $ServiceName | Out-Null
    }
    "logs" {
        Show-ServiceLogs -ServiceName $ServiceName -Lines $LogLines
    }
}