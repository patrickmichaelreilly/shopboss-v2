# ShopBoss Production Deployment Guide

This guide provides instructions for deploying ShopBoss to a Windows production server.

## Quick Start

### Automated Installation (Recommended)

1. **Download** the ShopBoss deployment package
2. **Extract** to a temporary directory
3. **Run Command Prompt as Administrator**
4. **Execute** the installation script:

```batch
scripts\install-shopboss.bat
```

The automated installer will:
- Build and publish the application
- Install as a Windows Service (default: "ShopBoss")
- Configure Windows Firewall (port 5000)
- Start the service automatically
- Verify the installation

### Custom Installation

For custom installation paths or ports:

```batch
scripts\install-shopboss.bat -InstallPath "D:\ShopBoss" -Port 8080
scripts\install-shopboss.bat -ServiceName "MyShopBoss" -SkipFirewall
scripts\install-shopboss.bat -Force
```

## Manual Installation

### Prerequisites

- Windows Server 2019 or later (or Windows 10/11)
- .NET 8.0 Runtime
- Administrator privileges

### Important: Database Location for Service Installation

**⚠️ CRITICAL**: When ShopBoss is installed as a Windows Service, the SQLite database file (`shopboss.db`) is created in the **System32** directory to resolve path issues when running as a service under the system account.

**Production Database Location**: `C:\Windows\System32\shopboss.db`

This location is used because:
- Windows Services run with different working directory contexts
- System32 provides reliable path resolution for service accounts
- Avoids permission issues with application-specific directories

**For Database Operations**:
- Backup: Copy from `C:\Windows\System32\shopboss.db`
- Migration: Apply changes to `C:\Windows\System32\shopboss.db`
- Recovery: Replace file at `C:\Windows\System32\shopboss.db`

### Step 1: Build and Publish

```bash
# From the project root directory
dotnet publish src/ShopBoss.Web/ShopBoss.Web.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o publish
```

### Step 2: Install Service

```powershell
# Run PowerShell as Administrator
.\scripts\install-service.ps1 -InstallPath "C:\ShopBoss"
```

### Step 3: Configure Firewall

```powershell
New-NetFirewallRule -DisplayName "ShopBoss Web Server" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow
```

## Service Management

### Using PowerShell Scripts

```powershell
# Check service status
.\scripts\manage-service.ps1 -Action status

# Start service
.\scripts\manage-service.ps1 -Action start

# Stop service
.\scripts\manage-service.ps1 -Action stop

# Restart service
.\scripts\manage-service.ps1 -Action restart

# View logs
.\scripts\manage-service.ps1 -Action logs
```

### Using Windows Commands

```powershell
# Start service
Start-Service -Name ShopBoss

# Stop service
Stop-Service -Name ShopBoss

# Check status
Get-Service -Name ShopBoss

# View Windows Event Logs
Get-WinEvent -FilterHashtable @{LogName='Application'; ProviderName='ShopBoss*'} -MaxEvents 50
```

## Configuration

### Production Settings

The production configuration is located at:
- `C:\ShopBoss\appsettings.Production.json`

Key settings:
- **Database**: SQLite database in the installation directory
- **Port**: 5000 (configurable)
- **Logging**: Optimized for production
- **Backup**: Enabled with 4-hour intervals
- **Health Monitoring**: Enabled

### Firewall Configuration

The service listens on `http://0.0.0.0:5000` by default, making it accessible from:
- Local machine: `http://localhost:5000`
- Network clients: `http://[server-ip]:5000`

### Security Considerations

1. **Firewall**: Only open port 5000 to trusted networks
2. **Database**: SQLite database is stored locally
3. **File Permissions**: Service runs under LocalSystem account
4. **HTTPS**: Consider adding SSL certificate for production use

## Backup and Maintenance

### Automatic Backups

ShopBoss includes automatic backup functionality:
- **Frequency**: Every 4 hours (configurable)
- **Location**: `C:\ShopBoss\Backups\`
- **Retention**: 168 backups (1 week at 4-hour intervals)
- **Compression**: Enabled (reduces file size by 60-80%)

### Manual Backup

```powershell
# Create immediate backup via web interface
# Navigate to: http://localhost:5000/Admin/BackupManagement
# Click "Create Backup"
```

### Database Maintenance

```powershell
# The application handles database maintenance automatically
# SQLite VACUUM operations are performed during backups
```

## Monitoring and Health Checks

### Built-in Health Monitoring

Access the health dashboard at:
`http://localhost:5000/Admin/HealthDashboard`

Monitors:
- Database connectivity
- Disk space usage
- Memory consumption
- Application response time

### Log Files

- **Application Logs**: Windows Event Log (Application)
- **Service Logs**: Windows Event Log (System)
- **Health Monitoring**: Real-time dashboard

## Troubleshooting

### Service Won't Start

1. **Check Event Logs**:
   ```powershell
   Get-WinEvent -FilterHashtable @{LogName='System'; ID=7034,7035,7036} -MaxEvents 10
   ```

2. **Verify Files**:
   ```powershell
   Test-Path "C:\ShopBoss\ShopBoss.Web.exe"
   ```

3. **Check Permissions**:
   ```powershell
   icacls "C:\ShopBoss" /grant "NT AUTHORITY\SYSTEM:(OI)(CI)F"
   ```

### Port Access Issues

1. **Check Firewall**:
   ```powershell
   Get-NetFirewallRule -DisplayName "ShopBoss Web Server"
   ```

2. **Test Port**:
   ```powershell
   Test-NetConnection -ComputerName localhost -Port 5000
   ```

3. **Check Port Usage**:
   ```powershell
   Get-NetTCPConnection -LocalPort 5000
   ```

### Database Issues

1. **Check Database File**:
   ```powershell
   Test-Path "C:\ShopBoss\shopboss.db"
   ```

2. **Check File Permissions**:
   ```powershell
   icacls "C:\ShopBoss\shopboss.db"
   ```

3. **Restore from Backup**:
   - Use the web interface: `/Admin/BackupManagement`
   - Or manually copy backup file to replace `shopboss.db`

## Uninstallation

### Complete Removal

```powershell
# Run PowerShell as Administrator
.\scripts\uninstall-service.ps1 -RemoveFiles
```

### Service Only

```powershell
.\scripts\uninstall-service.ps1
```

## Performance Tuning

### Hardware Recommendations

- **Minimum**: 2 CPU cores, 4GB RAM, 10GB disk
- **Recommended**: 4 CPU cores, 8GB RAM, 50GB disk
- **Network**: 100 Mbps for multiple concurrent users

### Configuration Tuning

1. **Adjust Kestrel Limits** (in `appsettings.Production.json`):
   - `MaxConcurrentConnections`: Increase for more users
   - `MaxRequestBodySize`: Increase for larger SDF files

2. **Database Optimization**:
   - Regular SQLite VACUUM (automatic during backups)
   - Monitor database file size growth

3. **Memory Management**:
   - Monitor memory usage via health dashboard
   - Restart service if memory usage exceeds thresholds

## Support and Maintenance

### Regular Maintenance Tasks

1. **Monitor Health Dashboard**: Weekly check of system health
2. **Review Backups**: Ensure backups are completing successfully
3. **Check Disk Space**: Monitor available disk space
4. **Update Application**: Follow upgrade procedures when available

### Getting Help

1. **Check Health Dashboard**: Real-time system status
2. **Review Event Logs**: Detailed error information
3. **Backup and Restore**: Use built-in backup functionality
4. **Service Management**: Use provided PowerShell scripts

## Version Information

- **ShopBoss Version**: v2.0
- **Target Framework**: .NET 8.0
- **Database**: SQLite
- **Web Server**: Kestrel (ASP.NET Core)
- **Platform**: Windows x64