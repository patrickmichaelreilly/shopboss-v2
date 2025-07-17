@echo off
REM ShopBoss Complete Installation Script
REM This script provides a complete automated installation of ShopBoss
REM Run as Administrator

setlocal enabledelayedexpansion

REM Default settings
set INSTALL_PATH=C:\ShopBoss
set SERVICE_NAME=ShopBoss
set PORT=5000
set SKIP_FIREWALL=false
set FORCE_INSTALL=false

REM Parse command line arguments
:parse_args
if "%~1"=="" goto start_install
if /i "%~1"=="-InstallPath" (
    set INSTALL_PATH=%~2
    shift
    shift
    goto parse_args
)
if /i "%~1"=="-ServiceName" (
    set SERVICE_NAME=%~2
    shift
    shift
    goto parse_args
)
if /i "%~1"=="-Port" (
    set PORT=%~2
    shift
    shift
    goto parse_args
)
if /i "%~1"=="-SkipFirewall" (
    set SKIP_FIREWALL=true
    shift
    goto parse_args
)
if /i "%~1"=="-Force" (
    set FORCE_INSTALL=true
    shift
    goto parse_args
)
if /i "%~1"=="-Help" (
    goto show_help
)
echo Unknown option: %~1
goto show_help

:show_help
echo ShopBoss Installation Script
echo ============================
echo.
echo Usage: install-shopboss.bat [OPTIONS]
echo.
echo Options:
echo   -InstallPath ^<path^>    Installation directory (default: C:\ShopBoss)
echo   -ServiceName ^<name^>    Windows service name (default: ShopBoss)
echo   -Port ^<number^>         HTTP port (default: 5000)
echo   -SkipFirewall          Skip firewall configuration
echo   -Force                 Force reinstallation
echo   -Help                  Show this help message
echo.
echo Examples:
echo   install-shopboss.bat
echo   install-shopboss.bat -InstallPath "D:\ShopBoss" -Port 8080
echo   install-shopboss.bat -Force -SkipFirewall
echo.
goto end

:start_install
echo ShopBoss Complete Installation
echo ==============================
echo.

REM Check if running as Administrator
net session >nul 2>&1
if %errorLevel% NEQ 0 (
    echo ERROR: This script must be run as Administrator.
    echo Right-click on Command Prompt and select "Run as administrator"
    pause
    exit /b 1
)

REM Display installation parameters
echo Installation Parameters:
echo   Install Path: %INSTALL_PATH%
echo   Service Name: %SERVICE_NAME%
echo   HTTP Port: %PORT%
echo   Skip Firewall: %SKIP_FIREWALL%
echo   Force Reinstall: %FORCE_INSTALL%
echo.

REM Check system requirements
echo Checking system requirements...

REM Check .NET runtime
dotnet --version >nul 2>&1
if %errorLevel% NEQ 0 (
    echo ERROR: .NET Runtime not found. Please install .NET 8.0 Runtime.
    echo Download from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)
for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo √ .NET Runtime found: %DOTNET_VERSION%

REM Check available disk space (simplified check)
for /f "tokens=3" %%i in ('dir /-c %INSTALL_PATH:~0,2%\ ^| find "bytes free"') do set FREE_SPACE=%%i
if %FREE_SPACE% LSS 1000000000 (
    echo ERROR: Insufficient disk space. At least 1GB free space is required.
    pause
    exit /b 1
)
echo √ Sufficient disk space available

REM Check port availability (simplified - just check if not in use by common services)
netstat -an | find ":%PORT% " >nul 2>&1
if %errorLevel% EQU 0 (
    if "%FORCE_INSTALL%"=="false" (
        echo ERROR: Port %PORT% is already in use. Use -Force to override or choose a different port.
        pause
        exit /b 1
    )
)
echo √ Port %PORT% is available

echo.

REM Get current script directory and project structure
set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%..\

REM Check if we're in the correct directory structure
set PROJECT_FILE=%PROJECT_ROOT%src\ShopBoss.Web\ShopBoss.Web.csproj
if not exist "%PROJECT_FILE%" (
    echo ERROR: ShopBoss project not found. Please run this script from the project root directory.
    echo Expected project file: %PROJECT_FILE%
    pause
    exit /b 1
)

REM Build and publish the application
echo Building and publishing ShopBoss...
set PUBLISH_DIR=%PROJECT_ROOT%publish

REM Clean previous publish directory
if exist "%PUBLISH_DIR%" (
    rmdir /s /q "%PUBLISH_DIR%"
)

REM Publish the application (override project settings for proper web deployment)
echo Command: dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%PUBLISH_DIR%"
dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%PUBLISH_DIR%"

if %errorLevel% NEQ 0 (
    echo ERROR: Failed to publish application. Please check the build output.
    pause
    exit /b 1
)

echo √ Application published successfully

REM Check if service already exists
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorLevel% EQU 0 (
    if "%FORCE_INSTALL%"=="true" (
        echo Removing existing service...
        sc stop "%SERVICE_NAME%" >nul 2>&1
        sc delete "%SERVICE_NAME%" >nul 2>&1
        timeout /t 2 /nobreak >nul
    ) else (
        echo ERROR: Service '%SERVICE_NAME%' already exists. Use -Force to reinstall.
        pause
        exit /b 1
    )
)

REM Create installation directory
echo Creating installation directory...
if exist "%INSTALL_PATH%" (
    if "%FORCE_INSTALL%"=="true" (
        rmdir /s /q "%INSTALL_PATH%"
    ) else (
        echo ERROR: Installation directory already exists. Use -Force to overwrite.
        pause
        exit /b 1
    )
)

mkdir "%INSTALL_PATH%"

REM Copy application files
echo Installing application files...
xcopy "%PUBLISH_DIR%\*" "%INSTALL_PATH%\" /s /e /y >nul

REM Copy wwwroot directory explicitly (sometimes not included in publish)
set WWWROOT_SOURCE=%PROJECT_ROOT%src\ShopBoss.Web\wwwroot
if exist "%WWWROOT_SOURCE%" (
    xcopy "%WWWROOT_SOURCE%" "%INSTALL_PATH%\wwwroot\" /s /e /y >nul
    echo √ wwwroot directory copied
)

REM Copy tools directory if it exists
set TOOLS_SOURCE=%PROJECT_ROOT%tools
if exist "%TOOLS_SOURCE%" (
    xcopy "%TOOLS_SOURCE%" "%INSTALL_PATH%\tools\" /s /e /y >nul
    echo √ Tools directory copied
)

REM Create data directories
echo Creating data directories...
mkdir "%INSTALL_PATH%\Backups" 2>nul
mkdir "%INSTALL_PATH%\Logs" 2>nul
mkdir "%INSTALL_PATH%\temp\uploads" 2>nul

REM Install Windows Service
echo Installing Windows Service...
set EXE_PATH=%INSTALL_PATH%\ShopBoss.Web.exe
set DISPLAY_NAME=ShopBoss Manufacturing System
set DESCRIPTION=ShopBoss v2 Manufacturing Workflow Management System

sc create "%SERVICE_NAME%" binPath= "%EXE_PATH%" DisplayName= "%DISPLAY_NAME%" start= auto >nul

if %errorLevel% NEQ 0 (
    echo ERROR: Failed to create service.
    pause
    exit /b 1
)

REM Set service description and failure recovery
sc description "%SERVICE_NAME%" "%DESCRIPTION%" >nul
sc failure "%SERVICE_NAME%" reset= 0 actions= restart/60000/restart/60000/restart/60000 >nul

echo √ Windows Service installed

REM Configure firewall
if "%SKIP_FIREWALL%"=="false" (
    echo Configuring Windows Firewall...
    set FIREWALL_RULE_NAME=ShopBoss Web Server
    
    REM Remove existing rule if it exists
    netsh advfirewall firewall delete rule name="!FIREWALL_RULE_NAME!" >nul 2>&1
    
    REM Create new rule
    netsh advfirewall firewall add rule name="!FIREWALL_RULE_NAME!" dir=in action=allow protocol=TCP localport=%PORT% description="Allow inbound traffic for ShopBoss Web Server" >nul
    echo √ Firewall configured for port %PORT%
)

REM Start the service
echo Starting ShopBoss service...
sc start "%SERVICE_NAME%" >nul

REM Wait and verify service status
timeout /t 5 /nobreak >nul
sc query "%SERVICE_NAME%" | find "RUNNING" >nul
if %errorLevel% EQU 0 (
    echo √ ShopBoss service is running!
) else (
    echo WARNING: Service was created but failed to start.
    echo Check Windows Event Log for error details.
)

REM Installation complete
echo.
echo Installation Complete!
echo =====================
echo.
echo Service Information:
echo   Name: %SERVICE_NAME%
echo   Display Name: %DISPLAY_NAME%
echo   Install Path: %INSTALL_PATH%
echo.
echo Web Interface:
echo   Local: http://localhost:%PORT%
echo   Network: http://[server-ip]:%PORT%
echo.
echo Management Commands:
echo   Start Service: sc start "%SERVICE_NAME%"
echo   Stop Service: sc stop "%SERVICE_NAME%"
echo   Check Status: sc query "%SERVICE_NAME%"
echo.
echo The service will start automatically when Windows starts.
echo.

REM Test web interface
echo Testing web interface...
timeout /t 3 /nobreak >nul
curl -s http://localhost:%PORT% >nul 2>&1
if %errorLevel% EQU 0 (
    echo √ Web interface is responding
) else (
    echo WARNING: Web interface test failed. The service may still be starting up.
    echo Try accessing http://localhost:%PORT% in a few minutes.
)

echo.
echo Installation completed successfully!

:end
pause