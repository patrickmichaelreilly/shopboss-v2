#!/bin/bash
# ShopBoss v2 Windows Deployment Script

set -e  # Exit on any error

echo "🚀 Starting ShopBoss v2 Windows deployment..."

# Configuration
WINDOWS_TEST_PATH="/mnt/c/ShopBoss-Testing"
PROJECT_PATH="src/ShopBoss.Web"

# Clean previous deployment
echo "🧹 Cleaning previous deployment..."
rm -rf "$WINDOWS_TEST_PATH"
mkdir -p "$WINDOWS_TEST_PATH"

# Build for Windows x64
echo "🔨 Building for Windows x64..."
dotnet publish "$PROJECT_PATH" \
  -c Release \
  -r win-x64 \
  --self-contained false \
  -o "$WINDOWS_TEST_PATH"

# Copy importer tool
echo "📦 Copying importer tool..."
mkdir -p "$WINDOWS_TEST_PATH/tools/importer"
if [ -d "tools/importer" ]; then
  cp -r tools/importer/* "$WINDOWS_TEST_PATH/tools/importer/"
else
  echo "⚠️  Warning: tools/importer directory not found - skipping importer copy"
fi

# Copy test data (if exists)
if [ -d "test-data" ]; then
  echo "📋 Copying test data..."
  cp -r test-data "$WINDOWS_TEST_PATH/"
fi

echo "✅ Deployment complete!"
echo ""
echo "🖥️  Windows Testing Instructions:"
echo "   1. Open Command Prompt as Administrator"
echo "   2. cd C:\\ShopBoss-Testing"
echo "   3. ShopBoss.Web.exe"
echo "   4. Open browser to: http://localhost:5000"
echo ""
echo "📁 Files deployed to: C:\\ShopBoss-Testing"