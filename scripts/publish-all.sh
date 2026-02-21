#!/bin/bash
# Publish DevMemory MCP for Windows and macOS
# Usage: ./scripts/publish-all.sh

set -e
cd "$(dirname "$0")/.."

PUBLISH_DIR="./publish"
rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"

echo "🔨 Publishing DevMemory MCP..."

# Windows x64
echo "  → Windows x64..."
dotnet publish src/DevMemory.Mcp -c Release -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$PUBLISH_DIR/win-x64" \
    --nologo -v q

# macOS ARM (M1/M2/M3/M4)
echo "  → macOS ARM (Apple Silicon)..."
dotnet publish src/DevMemory.Mcp -c Release -r osx-arm64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$PUBLISH_DIR/osx-arm64" \
    --nologo -v q

# macOS Intel
echo "  → macOS Intel..."
dotnet publish src/DevMemory.Mcp -c Release -r osx-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$PUBLISH_DIR/osx-x64" \
    --nologo -v q

echo ""
echo "✅ Published executables:"
echo ""
ls -lh "$PUBLISH_DIR"/*/devmemory-mcp* 2>/dev/null || ls -lh "$PUBLISH_DIR"/*/*
echo ""
echo "📦 Creating ZIP archives..."

cd "$PUBLISH_DIR"
zip -j devmemory-mcp-win-x64.zip win-x64/devmemory-mcp.exe
zip -j devmemory-mcp-osx-arm64.zip osx-arm64/devmemory-mcp
zip -j devmemory-mcp-osx-x64.zip osx-x64/devmemory-mcp

echo ""
echo "✅ Archives created:"
ls -lh *.zip
echo ""
echo "📋 Instructions for users: See README-DISTRIBUTION.md"
