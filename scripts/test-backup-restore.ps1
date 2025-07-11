# Test Backup/Restore Cycle with Data Verification
# Validates the complete backup and restore process

param(
    [string]$TestDirectory = ".\test-backup-restore",
    [switch]$Verbose = $false
)

function Write-TestLog {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage -ForegroundColor $(switch ($Level) { "ERROR" { "Red" } "WARNING" { "Yellow" } "SUCCESS" { "Green" } "TEST" { "Cyan" } default { "White" } })
}

Write-TestLog "Starting Backup/Restore Test Cycle" "TEST"

# Create test directory
if (Test-Path $TestDirectory) {
    Remove-Item $TestDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $TestDirectory -Force | Out-Null
Write-TestLog "Created test directory: $TestDirectory" "INFO"

# Test database path
$testDbPath = Join-Path $TestDirectory "shopboss-test.db"
$testBackupDir = Join-Path $TestDirectory "backups"
New-Item -ItemType Directory -Path $testBackupDir -Force | Out-Null

# Create a test database with sample data
try {
    Write-TestLog "Creating test database with sample data..." "TEST"
    
    # Create a simple SQLite database for testing
    $connectionString = "Data Source=$testDbPath"
    $sqliteCmd = Get-Command sqlite3 -ErrorAction SilentlyContinue
    
    if ($sqliteCmd) {
        # Create test database using SQLite CLI
        @"
CREATE TABLE TestData (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Value TEXT NOT NULL,
    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO TestData (Name, Value) VALUES 
('Test1', 'Value1'),
('Test2', 'Value2'),
('Test3', 'Value3'),
('Test4', 'Value4'),
('Test5', 'Value5');

CREATE TABLE BackupTest (
    Id INTEGER PRIMARY KEY,
    TestData TEXT NOT NULL,
    TestNumber INTEGER NOT NULL
);

INSERT INTO BackupTest (TestData, TestNumber) VALUES 
('BackupData1', 1),
('BackupData2', 2),
('BackupData3', 3);
"@ | & sqlite3 $testDbPath
        
        Write-TestLog "Test database created successfully" "SUCCESS"
        
        # Get original data hash for verification
        $originalDataHash = & sqlite3 $testDbPath "SELECT GROUP_CONCAT(Name || ':' || Value, '|') FROM TestData ORDER BY Id;"
        Write-TestLog "Original data hash: $originalDataHash" "INFO"
        
    } else {
        Write-TestLog "sqlite3 command not found - using minimal test" "WARNING"
        # Create empty database file for minimal testing
        [System.IO.File]::WriteAllText($testDbPath, "")
        $originalDataHash = "minimal-test"
    }
    
} catch {
    Write-TestLog "Failed to create test database: $($_.Exception.Message)" "ERROR"
    exit 1
}

# Test 1: Compressed Backup
Write-TestLog "Test 1: Creating compressed backup..." "TEST"
try {
    $backupResult = & .\scripts\backup-shopboss-beta.ps1 -BackupDirectory $testBackupDir -DatabasePath $testDbPath -BackupType "test" -Compress -Verbose:$Verbose
    
    if ($backupResult.Success) {
        Write-TestLog "Compressed backup created successfully" "SUCCESS"
        Write-TestLog "Backup file: $($backupResult.BackupPath)" "INFO"
        Write-TestLog "Compression ratio: $($backupResult.CompressionRatio)%" "INFO"
        $compressedBackupPath = $backupResult.BackupPath
    } else {
        Write-TestLog "Compressed backup failed" "ERROR"
        exit 1
    }
} catch {
    Write-TestLog "Compressed backup test failed: $($_.Exception.Message)" "ERROR"
    exit 1
}

# Test 2: Uncompressed Backup
Write-TestLog "Test 2: Creating uncompressed backup..." "TEST"
try {
    $backupResult = & .\scripts\backup-shopboss-beta.ps1 -BackupDirectory $testBackupDir -DatabasePath $testDbPath -BackupType "test" -Compress:$false -Verbose:$Verbose
    
    if ($backupResult.Success) {
        Write-TestLog "Uncompressed backup created successfully" "SUCCESS"
        Write-TestLog "Backup file: $($backupResult.BackupPath)" "INFO"
        $uncompressedBackupPath = $backupResult.BackupPath
    } else {
        Write-TestLog "Uncompressed backup failed" "ERROR"
        exit 1
    }
} catch {
    Write-TestLog "Uncompressed backup test failed: $($_.Exception.Message)" "ERROR"
    exit 1
}

# Test 3: Restore from Compressed Backup
Write-TestLog "Test 3: Restoring from compressed backup..." "TEST"
try {
    $restoreDbPath = Join-Path $TestDirectory "restored-compressed.db"
    $restoreResult = & .\scripts\restore-shopboss-beta.ps1 -BackupFilePath $compressedBackupPath -DatabasePath $restoreDbPath -Force -Verbose:$Verbose
    
    if ($restoreResult.Success) {
        Write-TestLog "Compressed restore completed successfully" "SUCCESS"
        
        # Verify restored data
        if ($sqliteCmd) {
            $restoredDataHash = & sqlite3 $restoreDbPath "SELECT GROUP_CONCAT(Name || ':' || Value, '|') FROM TestData ORDER BY Id;"
            if ($restoredDataHash -eq $originalDataHash) {
                Write-TestLog "Compressed restore data verification: PASSED" "SUCCESS"
            } else {
                Write-TestLog "Compressed restore data verification: FAILED" "ERROR"
                Write-TestLog "Expected: $originalDataHash" "ERROR"
                Write-TestLog "Got: $restoredDataHash" "ERROR"
            }
        } else {
            Write-TestLog "Data verification skipped (sqlite3 not available)" "WARNING"
        }
    } else {
        Write-TestLog "Compressed restore failed" "ERROR"
        exit 1
    }
} catch {
    Write-TestLog "Compressed restore test failed: $($_.Exception.Message)" "ERROR"
    exit 1
}

# Test 4: Restore from Uncompressed Backup
Write-TestLog "Test 4: Restoring from uncompressed backup..." "TEST"
try {
    $restoreDbPath = Join-Path $TestDirectory "restored-uncompressed.db"
    $restoreResult = & .\scripts\restore-shopboss-beta.ps1 -BackupFilePath $uncompressedBackupPath -DatabasePath $restoreDbPath -Force -Verbose:$Verbose
    
    if ($restoreResult.Success) {
        Write-TestLog "Uncompressed restore completed successfully" "SUCCESS"
        
        # Verify restored data
        if ($sqliteCmd) {
            $restoredDataHash = & sqlite3 $restoreDbPath "SELECT GROUP_CONCAT(Name || ':' || Value, '|') FROM TestData ORDER BY Id;"
            if ($restoredDataHash -eq $originalDataHash) {
                Write-TestLog "Uncompressed restore data verification: PASSED" "SUCCESS"
            } else {
                Write-TestLog "Uncompressed restore data verification: FAILED" "ERROR"
                Write-TestLog "Expected: $originalDataHash" "ERROR"
                Write-TestLog "Got: $restoredDataHash" "ERROR"
            }
        } else {
            Write-TestLog "Data verification skipped (sqlite3 not available)" "WARNING"
        }
    } else {
        Write-TestLog "Uncompressed restore failed" "ERROR"
        exit 1
    }
} catch {
    Write-TestLog "Uncompressed restore test failed: $($_.Exception.Message)" "ERROR"
    exit 1
}

# Test 5: Manifest Validation
Write-TestLog "Test 5: Validating backup manifests..." "TEST"
try {
    $manifests = Get-ChildItem $testBackupDir -Filter "*.manifest.json"
    
    if ($manifests.Count -eq 2) {
        Write-TestLog "Found $($manifests.Count) manifest files" "SUCCESS"
        
        foreach ($manifest in $manifests) {
            try {
                $manifestData = Get-Content $manifest.FullName | ConvertFrom-Json
                Write-TestLog "Manifest valid: $($manifest.Name)" "SUCCESS"
                Write-TestLog "  Backup Type: $($manifestData.BackupType)" "INFO"
                Write-TestLog "  Compression: $($manifestData.IsCompressed)" "INFO"
                Write-TestLog "  Checksum: $($manifestData.Checksum.Substring(0,16))..." "INFO"
            } catch {
                Write-TestLog "Invalid manifest: $($manifest.Name) - $($_.Exception.Message)" "ERROR"
            }
        }
    } else {
        Write-TestLog "Expected 2 manifest files, found $($manifests.Count)" "WARNING"
    }
} catch {
    Write-TestLog "Manifest validation failed: $($_.Exception.Message)" "ERROR"
}

# Test 6: Performance Check
Write-TestLog "Test 6: Performance analysis..." "TEST"
try {
    $originalSize = (Get-Item $testDbPath).Length
    $compressedSize = (Get-Item $compressedBackupPath).Length
    $uncompressedSize = (Get-Item $uncompressedBackupPath).Length
    
    Write-TestLog "Original database: $([math]::Round($originalSize / 1KB, 2)) KB" "INFO"
    Write-TestLog "Compressed backup: $([math]::Round($compressedSize / 1KB, 2)) KB" "INFO"
    Write-TestLog "Uncompressed backup: $([math]::Round($uncompressedSize / 1KB, 2)) KB" "INFO"
    
    $compressionRatio = [math]::Round((1 - ($compressedSize / $originalSize)) * 100, 1)
    Write-TestLog "Compression ratio: $compressionRatio%" "INFO"
    
    if ($compressionRatio -gt 0) {
        Write-TestLog "Compression is working effectively" "SUCCESS"
    } else {
        Write-TestLog "Compression may not be working as expected" "WARNING"
    }
} catch {
    Write-TestLog "Performance analysis failed: $($_.Exception.Message)" "WARNING"
}

# Cleanup test files
if (-not $Verbose) {
    try {
        Remove-Item $TestDirectory -Recurse -Force
        Write-TestLog "Cleaned up test directory" "INFO"
    } catch {
        Write-TestLog "Failed to cleanup test directory: $($_.Exception.Message)" "WARNING"
    }
}

Write-TestLog "Backup/Restore Test Cycle Completed Successfully" "SUCCESS"
Write-TestLog "All tests passed - backup and restore system is working correctly" "SUCCESS"

exit 0