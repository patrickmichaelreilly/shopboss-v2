# ShopBoss Beta Restore Script
# Restore from external backup with validation

param(
    [string]$BackupFilePath = "",
    [string]$BackupDirectory = "C:\ShopBoss-Backups",
    [string]$DatabasePath = ".\src\ShopBoss.Web\shopboss.db",
    [switch]$Force = $false,
    [switch]$Verbose = $false
)

# Function to write log messages
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage -ForegroundColor $(switch ($Level) { "ERROR" { "Red" } "WARNING" { "Yellow" } "SUCCESS" { "Green" } default { "White" } })
    if ($Verbose) { $logMessage | Out-File "$BackupDirectory\restore.log" -Append -Encoding UTF8 }
}

Write-Log "Starting ShopBoss Beta Restore" "INFO"

# If no backup file specified, show available backups
if (-not $BackupFilePath) {
    Write-Log "Available backups in $BackupDirectory:" "INFO"
    
    $backups = Get-ChildItem $BackupDirectory -Filter "shopboss_beta_backup_*.db*" | 
               Where-Object { $_.Name -match "shopboss_beta_backup_\d{8}_\d{6}\.db(\.gz)?$" } |
               Sort-Object LastWriteTime -Descending
    
    if ($backups.Count -eq 0) {
        Write-Log "No backup files found in $BackupDirectory" "ERROR"
        exit 1
    }
    
    Write-Host ""
    Write-Host "Available backups:" -ForegroundColor Yellow
    for ($i = 0; $i -lt $backups.Count; $i++) {
        $backup = $backups[$i]
        $manifestFile = $backup.FullName -replace "\.db(\.gz)?$", ".manifest.json"
        
        $info = "[$($i + 1)] $($backup.Name) - $([math]::Round($backup.Length / 1MB, 2)) MB - $($backup.LastWriteTime)"
        
        if (Test-Path $manifestFile) {
            try {
                $manifest = Get-Content $manifestFile | ConvertFrom-Json
                $info += " - Type: $($manifest.BackupType)"
            } catch {
                # Ignore manifest errors
            }
        }
        
        Write-Host $info -ForegroundColor White
    }
    
    Write-Host ""
    $selection = Read-Host "Enter backup number to restore (1-$($backups.Count)) or 'q' to quit"
    
    if ($selection -eq 'q') {
        Write-Log "Restore cancelled by user" "INFO"
        exit 0
    }
    
    $selectionIndex = [int]$selection - 1
    if ($selectionIndex -ge 0 -and $selectionIndex -lt $backups.Count) {
        $BackupFilePath = $backups[$selectionIndex].FullName
        Write-Log "Selected backup: $BackupFilePath" "INFO"
    } else {
        Write-Log "Invalid selection: $selection" "ERROR"
        exit 1
    }
}

# Validate backup file
if (-not (Test-Path $BackupFilePath)) {
    Write-Log "Backup file not found: $BackupFilePath" "ERROR"
    exit 1
}

# Load manifest if available
$manifestPath = $BackupFilePath -replace "\.db(\.gz)?$", ".manifest.json"
$manifest = $null
if (Test-Path $manifestPath) {
    try {
        $manifest = Get-Content $manifestPath | ConvertFrom-Json
        Write-Log "Manifest loaded: $manifestPath" "INFO"
        Write-Log "Backup Type: $($manifest.BackupType)" "INFO"
        Write-Log "Created: $($manifest.CreatedDate)" "INFO"
        Write-Log "Original Size: $([math]::Round($manifest.OriginalSize / 1MB, 2)) MB" "INFO"
    } catch {
        Write-Log "Failed to load manifest: $($_.Exception.Message)" "WARNING"
    }
}

# Validate checksum if available
if ($manifest -and $manifest.Checksum) {
    try {
        Write-Log "Validating backup integrity..." "INFO"
        $currentHash = (Get-FileHash $BackupFilePath -Algorithm SHA256).Hash
        if ($currentHash -eq $manifest.Checksum) {
            Write-Log "Backup integrity verified" "SUCCESS"
        } else {
            Write-Log "Backup integrity check failed!" "ERROR"
            if (-not $Force) {
                Write-Log "Use -Force to restore anyway" "WARNING"
                exit 1
            }
        }
    } catch {
        Write-Log "Failed to validate backup integrity: $($_.Exception.Message)" "WARNING"
    }
}

# Resolve database path
$DbPath = if (Test-Path $DatabasePath) { (Resolve-Path $DatabasePath).Path } else { $DatabasePath }
Write-Log "Target database: $DbPath" "INFO"

# Check if ShopBoss is running
$shopBossProcesses = Get-Process -Name "ShopBoss.Web" -ErrorAction SilentlyContinue
if ($shopBossProcesses) {
    Write-Log "WARNING: ShopBoss.Web is running!" "WARNING"
    if (-not $Force) {
        $response = Read-Host "Stop ShopBoss.Web processes before continuing? (y/n)"
        if ($response -eq 'y' -or $response -eq 'Y') {
            Write-Log "Stopping ShopBoss.Web processes..." "INFO"
            $shopBossProcesses | Stop-Process -Force
            Start-Sleep -Seconds 2
        } else {
            Write-Log "Cannot restore while ShopBoss is running. Use -Force to override." "ERROR"
            exit 1
        }
    } else {
        Write-Log "Force mode enabled - stopping processes..." "INFO"
        $shopBossProcesses | Stop-Process -Force
        Start-Sleep -Seconds 2
    }
}

# Create backup of current database
if (Test-Path $DbPath) {
    $currentBackupPath = "$DbPath.restore-backup-$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    try {
        Copy-Item $DbPath $currentBackupPath -Force
        Write-Log "Current database backed up to: $currentBackupPath" "INFO"
    } catch {
        Write-Log "Failed to backup current database: $($_.Exception.Message)" "WARNING"
    }
}

# Restore database
try {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    $isCompressed = $BackupFilePath.EndsWith(".gz")
    
    if ($isCompressed) {
        Write-Log "Restoring from compressed backup..." "INFO"
        
        # Decompress backup
        $inputStream = [System.IO.File]::OpenRead($BackupFilePath)
        $decompressionStream = [System.IO.Compression.GZipStream]::new($inputStream, [System.IO.Compression.CompressionMode]::Decompress)
        $outputStream = [System.IO.File]::Create($DbPath)
        
        $decompressionStream.CopyTo($outputStream)
        
        $outputStream.Close()
        $decompressionStream.Close()
        $inputStream.Close()
        
    } else {
        Write-Log "Restoring from uncompressed backup..." "INFO"
        Copy-Item $BackupFilePath $DbPath -Force
    }
    
    $stopwatch.Stop()
    
    # Verify restored database
    $restoredSize = (Get-Item $DbPath).Length
    Write-Log "Database restored in $($stopwatch.Elapsed.TotalSeconds) seconds" "SUCCESS"
    Write-Log "Restored database size: $([math]::Round($restoredSize / 1MB, 2)) MB" "INFO"
    
    if ($manifest -and $manifest.OriginalSize) {
        if ($restoredSize -eq $manifest.OriginalSize) {
            Write-Log "Restored size matches original size" "SUCCESS"
        } else {
            Write-Log "Warning: Restored size ($restoredSize) differs from original size ($($manifest.OriginalSize))" "WARNING"
        }
    }
    
} catch {
    Write-Log "Restore failed: $($_.Exception.Message)" "ERROR"
    
    # Try to restore from current backup
    if (Test-Path $currentBackupPath) {
        try {
            Copy-Item $currentBackupPath $DbPath -Force
            Write-Log "Restored original database from backup" "INFO"
        } catch {
            Write-Log "Failed to restore original database: $($_.Exception.Message)" "ERROR"
        }
    }
    
    exit 1
}

# Verify database integrity if SQLite tools are available
try {
    $sqliteCmd = Get-Command sqlite3 -ErrorAction SilentlyContinue
    if ($sqliteCmd) {
        Write-Log "Verifying database integrity..." "INFO"
        $integrityResult = & sqlite3 $DbPath "PRAGMA integrity_check;"
        if ($integrityResult -eq "ok") {
            Write-Log "Database integrity check: PASSED" "SUCCESS"
        } else {
            Write-Log "Database integrity check: FAILED - $integrityResult" "ERROR"
        }
    } else {
        Write-Log "sqlite3 command not found - skipping integrity check" "INFO"
    }
} catch {
    Write-Log "Error during integrity check: $($_.Exception.Message)" "WARNING"
}

Write-Log "Restore process completed successfully" "SUCCESS"
Write-Log "Database restored from: $BackupFilePath" "INFO"
Write-Log "You can now restart ShopBoss" "INFO"

# Return restore information
return @{
    Success = $true
    BackupFile = $BackupFilePath
    DatabasePath = $DbPath
    RestoredSize = $restoredSize
    Duration = $stopwatch.Elapsed.TotalSeconds
}