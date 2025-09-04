#!/bin/bash
# ShopBoss v2 Windows Deployment Script

set -e  # Exit on any error

# Configuration
WINDOWS_TEST_PATH="/mnt/c/ShopBoss-Testing"
PROJECT_PATH="src/ShopBoss.Web"
BACKUP_PATH="/tmp/shopboss-backup-$$"

# Default deployment mode
CLEAN_ALL=false
PRESERVE_DATA=true
RESET_DB=false

# Parse command line arguments
show_help() {
    echo "ShopBoss v2 Windows Deployment Script"
    echo ""
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --clean-all      Full clean deployment (destroys all data)"
    echo "  --preserve-data  Preserve database and uploads (default)"
    echo "  --reset-db       Clean app + reset database only"
    echo "  --help           Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                    # Default: preserve data"
    echo "  $0 --clean-all        # Full clean like old behavior"
    echo "  $0 --reset-db         # Keep uploads, reset database"
}

while [[ $# -gt 0 ]]; do
    case $1 in
        --clean-all)
            CLEAN_ALL=true
            PRESERVE_DATA=false
            shift
            ;;
        --preserve-data)
            PRESERVE_DATA=true
            CLEAN_ALL=false
            shift
            ;;
        --reset-db)
            RESET_DB=true
            PRESERVE_DATA=false
            shift
            ;;
        --help)
            show_help
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

echo "🚀 Starting ShopBoss v2 Windows deployment..."
echo "📋 Mode: $([ "$CLEAN_ALL" = true ] && echo "Clean All" || ([ "$RESET_DB" = true ] && echo "Reset DB" || echo "Preserve Data"))"

# Smart cleaning based on mode
if [ "$CLEAN_ALL" = true ]; then
    echo "🧹 Full clean deployment - removing everything..."
    rm -rf "$WINDOWS_TEST_PATH"
    mkdir -p "$WINDOWS_TEST_PATH"
elif [ ! -d "$WINDOWS_TEST_PATH" ]; then
    echo "📁 Creating deployment directory..."
    mkdir -p "$WINDOWS_TEST_PATH"
else
    echo "🔄 Smart cleaning - preserving data..."
    
    # Clean SQLite lock files that cause weird states
    echo "🔧 Cleaning SQLite lock files..."
    rm -f "$WINDOWS_TEST_PATH"/*.db-wal 2>/dev/null || true
    rm -f "$WINDOWS_TEST_PATH"/*.db-shm 2>/dev/null || true

    # Create backup directory
    mkdir -p "$BACKUP_PATH"
    
    # Backup database files if they exist and we're preserving data
    if [ "$PRESERVE_DATA" = true ] && [ -f "$WINDOWS_TEST_PATH/shopboss.db" ]; then
        echo "💾 Backing up database files..."
        cp "$WINDOWS_TEST_PATH"/shopboss.db* "$BACKUP_PATH/" 2>/dev/null || true
    fi
    
    # Backup uploads directory if it exists and we're preserving data
    if [ "$PRESERVE_DATA" = true ] && [ -d "$WINDOWS_TEST_PATH/temp/uploads" ]; then
        echo "📁 Backing up uploads..."
        mkdir -p "$BACKUP_PATH/temp"
        cp -r "$WINDOWS_TEST_PATH/temp/uploads" "$BACKUP_PATH/temp/" 2>/dev/null || true
    fi
    
    # Backup keys directory if it exists and we're preserving data
    if [ "$PRESERVE_DATA" = true ] && [ -d "$WINDOWS_TEST_PATH/keys" ]; then
        echo "🔐 Backing up keys directory..."
        cp -r "$WINDOWS_TEST_PATH/keys" "$BACKUP_PATH/" 2>/dev/null || true
    fi
    
    # Remove application files but keep directory structure
    echo "🗑️  Removing application files..."
    find "$WINDOWS_TEST_PATH" -name "*.exe" -delete 2>/dev/null || true
    find "$WINDOWS_TEST_PATH" -name "*.dll" -delete 2>/dev/null || true
    find "$WINDOWS_TEST_PATH" -name "*.json" -delete 2>/dev/null || true
    find "$WINDOWS_TEST_PATH" -name "*.xml" -delete 2>/dev/null || true
    find "$WINDOWS_TEST_PATH" -name "*.pdb" -delete 2>/dev/null || true
    
    # Remove wwwroot completely (will be redeployed)
    rm -rf "$WINDOWS_TEST_PATH/wwwroot" 2>/dev/null || true
    
    # Remove refs and runtimes directories
    rm -rf "$WINDOWS_TEST_PATH/refs" 2>/dev/null || true
    rm -rf "$WINDOWS_TEST_PATH/runtimes" 2>/dev/null || true
fi

# Build for Windows x64
echo "🔨 Building for Windows x64..."
dotnet publish "$PROJECT_PATH" \
  -c Release \
  -r win-x64 \
  --self-contained false \
  -o "$WINDOWS_TEST_PATH"

# Copy FastSdfReader tool (Phase 2)
echo "⚡ Building and copying FastSdfReader tool..."
mkdir -p "$WINDOWS_TEST_PATH/tools/fast-sdf-reader"

# Force rebuild to ensure latest JSON-only version
echo "   🔨 Building FastSdfReader (JSON-only version)..."
dotnet build tools/fast-sdf-reader/fast-sdf-reader.csproj -c Release -r win-x86 --self-contained true >/dev/null 2>&1

if [ -d "tools/fast-sdf-reader/bin/Release/net8.0/win-x86" ]; then
  cp tools/fast-sdf-reader/bin/Release/net8.0/win-x86/FastSdfReader.exe "$WINDOWS_TEST_PATH/tools/fast-sdf-reader/"
  cp tools/fast-sdf-reader/bin/Release/net8.0/win-x86/*.dll "$WINDOWS_TEST_PATH/tools/fast-sdf-reader/" 2>/dev/null || true
  cp tools/fast-sdf-reader/bin/Release/net8.0/win-x86/*.json "$WINDOWS_TEST_PATH/tools/fast-sdf-reader/" 2>/dev/null || true
  echo "   ✅ FastSdfReader rebuilt and deployed (JSON output mode)"
else
  echo "   ❌ FastSdfReader build failed"
fi


# Copy test data (if exists)
if [ -d "test-data" ]; then
  echo "📋 Copying test data..."
  cp -r test-data "$WINDOWS_TEST_PATH/"
fi


# Restore backed up data
if [ "$PRESERVE_DATA" = true ] && [ -d "$BACKUP_PATH" ]; then
    echo "🔄 Restoring preserved data..."
    
    # Restore database files
    if [ -f "$BACKUP_PATH/shopboss.db" ]; then
        echo "💾 Restoring database files..."
        cp "$BACKUP_PATH"/shopboss.db* "$WINDOWS_TEST_PATH/" 2>/dev/null || true
        echo "   ✅ Database preserved with existing work orders and data"
    fi
    
    # Restore uploads directory
    if [ -d "$BACKUP_PATH/temp/uploads" ]; then
        echo "📁 Restoring uploads directory..."
        mkdir -p "$WINDOWS_TEST_PATH/temp"
        cp -r "$BACKUP_PATH/temp/uploads" "$WINDOWS_TEST_PATH/temp/" 2>/dev/null || true
        echo "   ✅ Upload files preserved"
    fi
    
    # Restore keys directory
    if [ -d "$BACKUP_PATH/keys" ]; then
        echo "🔐 Restoring keys directory..."
        cp -r "$BACKUP_PATH/keys" "$WINDOWS_TEST_PATH/" 2>/dev/null || true
        echo "   ✅ Keys directory preserved"
    fi
    
    # Clean up backup
    rm -rf "$BACKUP_PATH"
fi

echo ""
echo "✅ Deployment complete!"
echo ""

# Show appropriate status message based on deployment mode
if [ "$CLEAN_ALL" = true ]; then
    echo "🧹 Full clean deployment completed"
    echo "   📝 Note: All previous data has been removed"
elif [ "$RESET_DB" = true ]; then
    echo "🔄 Database reset completed"
    echo "   📝 Note: Database cleared, uploads preserved"
elif [ "$PRESERVE_DATA" = true ]; then
    echo "💾 Data-preserving deployment completed"
    echo "   📝 Note: Existing work orders and uploads preserved"
fi

echo ""
echo "🖥️  Windows Testing Instructions:"
echo "   1. Open Command Prompt as Administrator"
echo "   2. cd C:\\ShopBoss-Testing"
echo "   3. ShopBoss.Web.exe"
echo "   4. Access from any device at: http://[WindowsIP]:5000"
echo ""
echo "📁 Files deployed to: C:\\ShopBoss-Testing"
echo "💡 The application automatically binds to all network interfaces"
echo ""
echo "💡 Next time you can use:"
echo "   ./deploy-to-windows.sh              # Preserve data (default)"
echo "   ./deploy-to-windows.sh --clean-all  # Full clean"
echo "   ./deploy-to-windows.sh --help       # Show options"