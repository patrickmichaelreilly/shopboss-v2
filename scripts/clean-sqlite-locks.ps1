# SQLite Lock Cleanup Script
# Resolves common "weird state" issues with SQLite database locks

param(
    [string]$DatabasePath = ".\src\ShopBoss.Web\shopboss.db",
    [switch]$Force = $false
)

Write-Host "ShopBoss SQLite Lock Cleanup Script" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Green

# Get full path to database
$DbPath = (Resolve-Path $DatabasePath -ErrorAction SilentlyContinue).Path
if (-not $DbPath) {
    Write-Host "Database file not found at: $DatabasePath" -ForegroundColor Red
    Write-Host "Please ensure the path is correct and the database exists." -ForegroundColor Yellow
    exit 1
}

Write-Host "Database Path: $DbPath" -ForegroundColor Cyan

# Check if ShopBoss is running
$shopBossProcesses = Get-Process -Name "ShopBoss.Web" -ErrorAction SilentlyContinue
if ($shopBossProcesses) {
    Write-Host "WARNING: ShopBoss.Web process is running!" -ForegroundColor Yellow
    Write-Host "Found processes:" -ForegroundColor Yellow
    $shopBossProcesses | ForEach-Object { Write-Host "  PID: $($_.Id) - $($_.ProcessName)" -ForegroundColor Yellow }
    
    if (-not $Force) {
        $response = Read-Host "Stop ShopBoss.Web processes before continuing? (y/n)"
        if ($response -eq 'y' -or $response -eq 'Y') {
            Write-Host "Stopping ShopBoss.Web processes..." -ForegroundColor Yellow
            $shopBossProcesses | Stop-Process -Force
            Start-Sleep -Seconds 2
        } else {
            Write-Host "Cannot clean locks while ShopBoss is running. Use -Force to override." -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "Force mode enabled - stopping processes..." -ForegroundColor Yellow
        $shopBossProcesses | Stop-Process -Force
        Start-Sleep -Seconds 2
    }
}

# List of potential lock files to clean
$lockFiles = @(
    "$DbPath-wal",
    "$DbPath-shm",
    "$DbPath-journal"
)

Write-Host "`nChecking for lock files..." -ForegroundColor Cyan

$foundLocks = $false
foreach ($lockFile in $lockFiles) {
    if (Test-Path $lockFile) {
        $foundLocks = $true
        $fileInfo = Get-Item $lockFile
        Write-Host "Found: $($fileInfo.Name) ($($fileInfo.Length) bytes, modified: $($fileInfo.LastWriteTime))" -ForegroundColor Yellow
    }
}

if (-not $foundLocks) {
    Write-Host "No lock files found. Database appears to be in a clean state." -ForegroundColor Green
    exit 0
}

# Clean up lock files
Write-Host "`nCleaning up lock files..." -ForegroundColor Cyan

foreach ($lockFile in $lockFiles) {
    if (Test-Path $lockFile) {
        try {
            Remove-Item $lockFile -Force
            Write-Host "Removed: $(Split-Path $lockFile -Leaf)" -ForegroundColor Green
        } catch {
            Write-Host "Failed to remove: $(Split-Path $lockFile -Leaf) - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

# Verify database integrity
Write-Host "`nVerifying database integrity..." -ForegroundColor Cyan

try {
    # Use SQLite command line tool if available, otherwise skip verification
    $sqliteCmd = Get-Command sqlite3 -ErrorAction SilentlyContinue
    if ($sqliteCmd) {
        $integrityResult = & sqlite3 $DbPath "PRAGMA integrity_check;"
        if ($integrityResult -eq "ok") {
            Write-Host "Database integrity check: PASSED" -ForegroundColor Green
        } else {
            Write-Host "Database integrity check: FAILED" -ForegroundColor Red
            Write-Host "Result: $integrityResult" -ForegroundColor Red
        }
    } else {
        Write-Host "sqlite3 command not found - skipping integrity check" -ForegroundColor Yellow
        Write-Host "Database locks cleaned successfully" -ForegroundColor Green
    }
} catch {
    Write-Host "Error during integrity check: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nCleanup complete!" -ForegroundColor Green
Write-Host "You can now restart ShopBoss." -ForegroundColor Cyan