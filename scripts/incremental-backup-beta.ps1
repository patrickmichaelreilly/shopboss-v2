# ShopBoss Beta Incremental Backup Strategy
# Creates incremental backups for beta patches with minimal downtime

param(
    [string]$BackupDirectory = "C:\ShopBoss-Backups",
    [string]$DatabasePath = ".\src\ShopBoss.Web\shopboss.db",
    [string]$PatchVersion = "",
    [switch]$CreateBaseline = $false,
    [switch]$Verbose = $false
)

# Function to write log messages
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage -ForegroundColor $(switch ($Level) { "ERROR" { "Red" } "WARNING" { "Yellow" } "SUCCESS" { "Green" } default { "White" } })
    if ($Verbose) { $logMessage | Out-File "$BackupDirectory\incremental.log" -Append -Encoding UTF8 }
}

Write-Log "Starting ShopBoss Beta Incremental Backup" "INFO"

# Ensure backup directory exists
if (-not (Test-Path $BackupDirectory)) {
    New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
    Write-Log "Created backup directory: $BackupDirectory" "INFO"
}

# Create incremental backup subdirectory
$incrementalDir = Join-Path $BackupDirectory "incremental"
if (-not (Test-Path $incrementalDir)) {
    New-Item -ItemType Directory -Path $incrementalDir -Force | Out-Null
    Write-Log "Created incremental backup directory: $incrementalDir" "INFO"
}

# Resolve database path
try {
    $DbPath = (Resolve-Path $DatabasePath -ErrorAction Stop).Path
    Write-Log "Database path: $DbPath" "INFO"
} catch {
    Write-Log "Database file not found: $DatabasePath" "ERROR"
    exit 1
}

# Get current database info
$currentDbInfo = Get-Item $DbPath
$currentSize = $currentDbInfo.Length
$currentModified = $currentDbInfo.LastWriteTime

Write-Log "Current database size: $([math]::Round($currentSize / 1MB, 2)) MB" "INFO"
Write-Log "Current database modified: $currentModified" "INFO"

# Check for baseline backup
$baselinePattern = "shopboss_baseline_*.db.gz"
$baselineBackups = Get-ChildItem $incrementalDir -Filter $baselinePattern | Sort-Object LastWriteTime -Descending

if ($CreateBaseline -or $baselineBackups.Count -eq 0) {
    Write-Log "Creating baseline backup..." "INFO"
    
    $baselineTimestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $baselineName = "shopboss_baseline_$baselineTimestamp"
    $baselineFilePath = Join-Path $incrementalDir "$baselineName.db.gz"
    
    try {
        # Create compressed baseline backup
        $inputStream = [System.IO.File]::OpenRead($DbPath)
        $outputStream = [System.IO.File]::Create($baselineFilePath)
        $compressionStream = [System.IO.Compression.GZipStream]::new($outputStream, [System.IO.Compression.CompressionMode]::Compress)
        
        $inputStream.CopyTo($compressionStream)
        
        $compressionStream.Close()
        $outputStream.Close()
        $inputStream.Close()
        
        $baselineSize = (Get-Item $baselineFilePath).Length
        Write-Log "Baseline backup created: $baselineFilePath" "SUCCESS"
        Write-Log "Baseline size: $([math]::Round($baselineSize / 1MB, 2)) MB" "INFO"
        
        # Create baseline manifest
        $baselineManifest = @{
            Type = "baseline"
            Version = $PatchVersion
            CreatedDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss UTC")
            DatabasePath = $DbPath
            BaselineFile = $baselineFilePath
            OriginalSize = $currentSize
            CompressedSize = $baselineSize
            CompressionRatio = [math]::Round((1 - ($baselineSize / $currentSize)) * 100, 1)
            Checksum = (Get-FileHash $baselineFilePath -Algorithm SHA256).Hash
        }
        
        $baselineManifestPath = Join-Path $incrementalDir "$baselineName.manifest.json"
        $baselineManifest | ConvertTo-Json -Depth 3 | Out-File $baselineManifestPath -Encoding UTF8
        
        Write-Log "Baseline manifest created: $baselineManifestPath" "INFO"
        
        if (-not $CreateBaseline) {
            Write-Log "Baseline backup completed - no incremental needed" "SUCCESS"
            exit 0
        }
        
        $mostRecentBaseline = $baselineFilePath
        
    } catch {
        Write-Log "Failed to create baseline backup: $($_.Exception.Message)" "ERROR"
        exit 1
    }
} else {
    $mostRecentBaseline = $baselineBackups[0].FullName
    Write-Log "Using existing baseline: $mostRecentBaseline" "INFO"
}

# Load baseline manifest
$baselineManifestPath = $mostRecentBaseline -replace "\.db\.gz$", ".manifest.json"
$baselineManifest = $null
if (Test-Path $baselineManifestPath) {
    try {
        $baselineManifest = Get-Content $baselineManifestPath | ConvertFrom-Json
        Write-Log "Loaded baseline manifest from: $baselineManifestPath" "INFO"
    } catch {
        Write-Log "Failed to load baseline manifest: $($_.Exception.Message)" "WARNING"
    }
}

# Check if incremental backup is needed
$needsIncremental = $false
$changeReason = ""

if ($baselineManifest) {
    $baselineDate = [DateTime]::Parse($baselineManifest.CreatedDate)
    if ($currentModified -gt $baselineDate) {
        $needsIncremental = $true
        $changeReason = "Database modified since baseline"
    }
    
    if ($currentSize -ne $baselineManifest.OriginalSize) {
        $needsIncremental = $true
        $changeReason += " Database size changed"
    }
} else {
    $needsIncremental = $true
    $changeReason = "No baseline manifest found"
}

if ($PatchVersion) {
    $needsIncremental = $true
    $changeReason += " Patch version specified: $PatchVersion"
}

if (-not $needsIncremental) {
    Write-Log "No incremental backup needed - database unchanged since baseline" "INFO"
    exit 0
}

Write-Log "Incremental backup needed: $changeReason" "INFO"

# Create incremental backup
$incrementalTimestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$incrementalName = "shopboss_incremental_$incrementalTimestamp"
if ($PatchVersion) {
    $incrementalName += "_$PatchVersion"
}

$incrementalFilePath = Join-Path $incrementalDir "$incrementalName.db.gz"

try {
    Write-Log "Creating incremental backup..." "INFO"
    
    # Create compressed incremental backup
    $inputStream = [System.IO.File]::OpenRead($DbPath)
    $outputStream = [System.IO.File]::Create($incrementalFilePath)
    $compressionStream = [System.IO.Compression.GZipStream]::new($outputStream, [System.IO.Compression.CompressionMode]::Compress)
    
    $inputStream.CopyTo($compressionStream)
    
    $compressionStream.Close()
    $outputStream.Close()
    $inputStream.Close()
    
    $incrementalSize = (Get-Item $incrementalFilePath).Length
    Write-Log "Incremental backup created: $incrementalFilePath" "SUCCESS"
    Write-Log "Incremental size: $([math]::Round($incrementalSize / 1MB, 2)) MB" "INFO"
    
    # Create incremental manifest
    $incrementalManifest = @{
        Type = "incremental"
        Version = $PatchVersion
        CreatedDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss UTC")
        DatabasePath = $DbPath
        IncrementalFile = $incrementalFilePath
        BaselineFile = $mostRecentBaseline
        OriginalSize = $currentSize
        CompressedSize = $incrementalSize
        CompressionRatio = [math]::Round((1 - ($incrementalSize / $currentSize)) * 100, 1)
        Checksum = (Get-FileHash $incrementalFilePath -Algorithm SHA256).Hash
        ChangeReason = $changeReason
    }
    
    $incrementalManifestPath = Join-Path $incrementalDir "$incrementalName.manifest.json"
    $incrementalManifest | ConvertTo-Json -Depth 3 | Out-File $incrementalManifestPath -Encoding UTF8
    
    Write-Log "Incremental manifest created: $incrementalManifestPath" "INFO"
    
} catch {
    Write-Log "Failed to create incremental backup: $($_.Exception.Message)" "ERROR"
    exit 1
}

# Cleanup old incremental backups (keep last 10)
try {
    $allIncrementals = Get-ChildItem $incrementalDir -Filter "shopboss_incremental_*.db.gz" | 
                      Sort-Object LastWriteTime -Descending
    
    if ($allIncrementals.Count -gt 10) {
        $incrementalsToDelete = $allIncrementals | Select-Object -Skip 10
        Write-Log "Cleaning up $($incrementalsToDelete.Count) old incremental backups..." "INFO"
        
        foreach ($incremental in $incrementalsToDelete) {
            Remove-Item $incremental.FullName -Force
            
            # Also remove corresponding manifest
            $manifestFile = $incremental.FullName -replace "\.db\.gz$", ".manifest.json"
            if (Test-Path $manifestFile) {
                Remove-Item $manifestFile -Force
            }
            
            Write-Log "Deleted old incremental: $($incremental.Name)" "INFO"
        }
    }
    
} catch {
    Write-Log "Failed to cleanup old incremental backups: $($_.Exception.Message)" "WARNING"
}

# Create incremental backup strategy summary
$summaryPath = Join-Path $incrementalDir "backup-strategy-summary.txt"
$summary = @"
ShopBoss Beta Incremental Backup Strategy Summary
Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

Current Status:
- Baseline backup: $mostRecentBaseline
- Latest incremental: $incrementalFilePath
- Database size: $([math]::Round($currentSize / 1MB, 2)) MB
- Backup compression ratio: $([math]::Round((1 - ($incrementalSize / $currentSize)) * 100, 1))%

Backup Strategy:
1. Baseline backups created on major releases or when requested
2. Incremental backups created automatically when:
   - Database is modified since last baseline
   - Database size changes
   - Patch version is specified
3. Retention policy: Keep last 10 incremental backups
4. All backups are compressed using GZip

Usage:
- Create baseline: .\incremental-backup-beta.ps1 -CreateBaseline
- Create incremental: .\incremental-backup-beta.ps1 -PatchVersion "1.2.3"
- Restore: Use .\restore-shopboss-beta.ps1 with appropriate backup file

Files in incremental directory:
$(Get-ChildItem $incrementalDir -Filter "*.db.gz" | Sort-Object LastWriteTime -Descending | ForEach-Object { "- $($_.Name) ($([math]::Round($_.Length / 1MB, 2)) MB)" })
"@

$summary | Out-File $summaryPath -Encoding UTF8
Write-Log "Strategy summary updated: $summaryPath" "INFO"

Write-Log "Incremental backup process completed successfully" "SUCCESS"
Write-Log "Backup file: $incrementalFilePath" "INFO"
Write-Log "Use '.\restore-shopboss-beta.ps1 -BackupFilePath `"$incrementalFilePath`"' to restore" "INFO"

# Return backup information
return @{
    Success = $true
    BackupType = "incremental"
    BackupPath = $incrementalFilePath
    ManifestPath = $incrementalManifestPath
    BaselinePath = $mostRecentBaseline
    OriginalSize = $currentSize
    BackupSize = $incrementalSize
    CompressionRatio = [math]::Round((1 - ($incrementalSize / $currentSize)) * 100, 1)
    ChangeReason = $changeReason
}