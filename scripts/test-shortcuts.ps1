# ShopBoss Testing Shortcuts
# Quick commands for development and testing workflow

param(
    [string]$Command = "help"
)

function Show-Help {
    Write-Host "ShopBoss Testing Shortcuts" -ForegroundColor Green
    Write-Host "=========================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage: .\scripts\test-shortcuts.ps1 [command]" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Commands:" -ForegroundColor Yellow
    Write-Host "  build      - Build the ShopBoss application" -ForegroundColor White
    Write-Host "  run        - Run ShopBoss in development mode" -ForegroundColor White
    Write-Host "  watch      - Run ShopBoss with file watching" -ForegroundColor White
    Write-Host "  test       - Run all tests" -ForegroundColor White
    Write-Host "  clean      - Clean SQLite locks" -ForegroundColor White
    Write-Host "  backup     - Create manual backup" -ForegroundColor White
    Write-Host "  checkpoint - Create development checkpoint" -ForegroundColor White
    Write-Host "  reset      - Reset to fresh-install checkpoint" -ForegroundColor White
    Write-Host "  status     - Show git and application status" -ForegroundColor White
    Write-Host "  help       - Show this help message" -ForegroundColor White
    Write-Host ""
}

function Build-Application {
    Write-Host "Building ShopBoss..." -ForegroundColor Yellow
    dotnet build src/ShopBoss.Web/ShopBoss.Web.csproj
}

function Run-Application {
    Write-Host "Starting ShopBoss in development mode..." -ForegroundColor Yellow
    dotnet run --project src/ShopBoss.Web/ShopBoss.Web.csproj
}

function Watch-Application {
    Write-Host "Starting ShopBoss with file watching..." -ForegroundColor Yellow
    dotnet watch --project src/ShopBoss.Web/ShopBoss.Web.csproj
}

function Test-Application {
    Write-Host "Running all tests..." -ForegroundColor Yellow
    dotnet test
}

function Clean-Locks {
    Write-Host "Cleaning SQLite locks..." -ForegroundColor Yellow
    .\scripts\clean-sqlite-locks.ps1
}

function Create-Backup {
    Write-Host "Creating manual backup..." -ForegroundColor Yellow
    Write-Host "Note: This requires the application to be running" -ForegroundColor Cyan
    Write-Host "Navigate to Admin > Backup Management to create backup" -ForegroundColor Cyan
}

function Create-Checkpoint {
    $checkpointName = Read-Host "Enter checkpoint name"
    if (-not $checkpointName) {
        Write-Host "Checkpoint name required" -ForegroundColor Red
        return
    }
    
    $checkpointPath = "checkpoints\$checkpointName"
    if (Test-Path $checkpointPath) {
        Write-Host "Checkpoint already exists: $checkpointName" -ForegroundColor Red
        return
    }
    
    Write-Host "Creating checkpoint: $checkpointName" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $checkpointPath -Force | Out-Null
    
    if (Test-Path "src\ShopBoss.Web\shopboss.db") {
        Copy-Item "src\ShopBoss.Web\shopboss.db" "$checkpointPath\shopboss.db"
        Write-Host "Database copied to checkpoint" -ForegroundColor Green
    } else {
        Write-Host "Warning: Database file not found" -ForegroundColor Yellow
    }
    
    $description = Read-Host "Enter checkpoint description"
    if ($description) {
        $description | Out-File "$checkpointPath\description.txt" -Encoding UTF8
    }
    
    Get-Date | Out-File "$checkpointPath\timestamp.txt" -Encoding UTF8
    Write-Host "Checkpoint created successfully" -ForegroundColor Green
}

function Reset-ToFreshInstall {
    Write-Host "Resetting to fresh-install checkpoint..." -ForegroundColor Yellow
    
    if (-not (Test-Path "checkpoints\fresh-install\shopboss.db")) {
        Write-Host "Fresh install checkpoint not found" -ForegroundColor Red
        Write-Host "Please create a fresh-install checkpoint first" -ForegroundColor Yellow
        return
    }
    
    $confirm = Read-Host "This will replace the current database. Continue? (y/n)"
    if ($confirm -eq 'y' -or $confirm -eq 'Y') {
        Copy-Item "checkpoints\fresh-install\shopboss.db" "src\ShopBoss.Web\shopboss.db" -Force
        Write-Host "Database reset to fresh install state" -ForegroundColor Green
    } else {
        Write-Host "Reset cancelled" -ForegroundColor Yellow
    }
}

function Show-Status {
    Write-Host "ShopBoss Status" -ForegroundColor Green
    Write-Host "===============" -ForegroundColor Green
    Write-Host ""
    
    # Git status
    Write-Host "Git Status:" -ForegroundColor Yellow
    git status --porcelain
    
    # Check if application is running
    Write-Host ""
    Write-Host "Application Status:" -ForegroundColor Yellow
    $processes = Get-Process -Name "ShopBoss.Web" -ErrorAction SilentlyContinue
    if ($processes) {
        Write-Host "ShopBoss.Web is running (PID: $($processes.Id))" -ForegroundColor Green
    } else {
        Write-Host "ShopBoss.Web is not running" -ForegroundColor Red
    }
    
    # Database status
    Write-Host ""
    Write-Host "Database Status:" -ForegroundColor Yellow
    if (Test-Path "src\ShopBoss.Web\shopboss.db") {
        $dbInfo = Get-Item "src\ShopBoss.Web\shopboss.db"
        Write-Host "Database exists: $($dbInfo.Length) bytes, modified: $($dbInfo.LastWriteTime)" -ForegroundColor Green
    } else {
        Write-Host "Database file not found" -ForegroundColor Red
    }
}

# Main command dispatch
switch ($Command.ToLower()) {
    "build" { Build-Application }
    "run" { Run-Application }
    "watch" { Watch-Application }
    "test" { Test-Application }
    "clean" { Clean-Locks }
    "backup" { Create-Backup }
    "checkpoint" { Create-Checkpoint }
    "reset" { Reset-ToFreshInstall }
    "status" { Show-Status }
    "help" { Show-Help }
    default { 
        Write-Host "Unknown command: $Command" -ForegroundColor Red
        Show-Help 
    }
}