# ShopBoss Beta Backup Script
# External backup with compression and manifest generation

param(
    [string]$BackupDirectory = "C:\ShopBoss-Backups",
    [string]$DatabasePath = ".\src\ShopBoss.Web\shopboss.db",
    [string]$BackupType = "manual",
    [switch]$Compress = $true,
    [switch]$Verbose = $false
)

# Function to write log messages
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage -ForegroundColor $(switch ($Level) { "ERROR" { "Red" } "WARNING" { "Yellow" } "SUCCESS" { "Green" } default { "White" } })
    if ($Verbose) { $logMessage | Out-File "$BackupDirectory\backup.log" -Append -Encoding UTF8 }
}

Write-Log "Starting ShopBoss Beta Backup" "INFO"
Write-Log "Backup Directory: $BackupDirectory" "INFO"
Write-Log "Database Path: $DatabasePath" "INFO"
Write-Log "Backup Type: $BackupType" "INFO"
Write-Log "Compression: $Compress" "INFO"

# Ensure backup directory exists
try {
    if (-not (Test-Path $BackupDirectory)) {
        New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
        Write-Log "Created backup directory: $BackupDirectory" "SUCCESS"
    }
} catch {
    Write-Log "Failed to create backup directory: $($_.Exception.Message)" "ERROR"
    exit 1
}

# Resolve database path
try {
    $DbPath = (Resolve-Path $DatabasePath -ErrorAction Stop).Path
    Write-Log "Database resolved to: $DbPath" "INFO"
} catch {
    Write-Log "Database file not found: $DatabasePath" "ERROR"
    exit 1
}

# Generate backup filename with timestamp
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupName = "shopboss_beta_backup_$timestamp"
$backupFileName = "$backupName.db"
if ($Compress) {
    $backupFileName += ".gz"
}

$backupFilePath = Join-Path $BackupDirectory $backupFileName
Write-Log "Backup file: $backupFilePath" "INFO"

# Create backup
try {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    # Get original file size
    $originalSize = (Get-Item $DbPath).Length
    Write-Log "Original database size: $([math]::Round($originalSize / 1MB, 2)) MB" "INFO"
    
    if ($Compress) {
        Write-Log "Creating compressed backup..." "INFO"
        
        # Read database and compress
        $inputStream = [System.IO.File]::OpenRead($DbPath)
        $outputStream = [System.IO.File]::Create($backupFilePath)
        $compressionStream = [System.IO.Compression.GZipStream]::new($outputStream, [System.IO.Compression.CompressionMode]::Compress)
        
        $inputStream.CopyTo($compressionStream)
        
        $compressionStream.Close()
        $outputStream.Close()
        $inputStream.Close()
        
    } else {
        Write-Log "Creating uncompressed backup..." "INFO"
        Copy-Item $DbPath $backupFilePath -Force
    }
    
    $stopwatch.Stop()
    
    # Get backup file size
    $backupSize = (Get-Item $backupFilePath).Length
    $compressionRatio = if ($Compress) { [math]::Round((1 - ($backupSize / $originalSize)) * 100, 1) } else { 0 }
    
    Write-Log "Backup completed in $($stopwatch.Elapsed.TotalSeconds) seconds" "SUCCESS"
    Write-Log "Backup size: $([math]::Round($backupSize / 1MB, 2)) MB" "INFO"
    if ($Compress) {
        Write-Log "Compression ratio: $compressionRatio%" "INFO"
    }
    
} catch {
    Write-Log "Backup failed: $($_.Exception.Message)" "ERROR"
    if (Test-Path $backupFilePath) {
        Remove-Item $backupFilePath -Force
    }
    exit 1
}

# Generate manifest file
try {
    $manifestPath = Join-Path $BackupDirectory "$backupName.manifest.json"
    
    $manifest = @{
        BackupName = $backupName
        BackupType = $BackupType
        CreatedDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss UTC")
        DatabasePath = $DbPath
        BackupFilePath = $backupFilePath
        OriginalSize = $originalSize
        BackupSize = $backupSize
        IsCompressed = $Compress
        CompressionRatio = $compressionRatio
        Duration = $stopwatch.Elapsed.TotalSeconds
        Version = "1.0"
        Checksum = (Get-FileHash $backupFilePath -Algorithm SHA256).Hash
    }
    
    $manifest | ConvertTo-Json -Depth 3 | Out-File $manifestPath -Encoding UTF8
    Write-Log "Manifest created: $manifestPath" "SUCCESS"
    
} catch {
    Write-Log "Failed to create manifest: $($_.Exception.Message)" "WARNING"
}

# Cleanup old backups (keep last 10)
try {
    $allBackups = Get-ChildItem $BackupDirectory -Filter "shopboss_beta_backup_*.db*" | 
                  Where-Object { $_.Name -match "shopboss_beta_backup_\d{8}_\d{6}\.db(\.gz)?$" } |
                  Sort-Object LastWriteTime -Descending
    
    if ($allBackups.Count -gt 10) {
        $backupsToDelete = $allBackups | Select-Object -Skip 10
        Write-Log "Cleaning up $($backupsToDelete.Count) old backups..." "INFO"
        
        foreach ($backup in $backupsToDelete) {
            Remove-Item $backup.FullName -Force
            
            # Also remove corresponding manifest
            $manifestFile = $backup.FullName -replace "\.db(\.gz)?$", ".manifest.json"
            if (Test-Path $manifestFile) {
                Remove-Item $manifestFile -Force
            }
            
            Write-Log "Deleted old backup: $($backup.Name)" "INFO"
        }
    }
    
} catch {
    Write-Log "Failed to cleanup old backups: $($_.Exception.Message)" "WARNING"
}

Write-Log "Backup process completed successfully" "SUCCESS"
Write-Log "Backup file: $backupFilePath" "INFO"

# Return backup information
return @{
    Success = $true
    BackupPath = $backupFilePath
    ManifestPath = $manifestPath
    OriginalSize = $originalSize
    BackupSize = $backupSize
    CompressionRatio = $compressionRatio
    Duration = $stopwatch.Elapsed.TotalSeconds
}