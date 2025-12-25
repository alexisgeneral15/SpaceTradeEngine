#!/bin/bash
# Space Trade Engine Setup Script for Linux/Mac

echo ""
echo "========================================"
echo "Space Trade Engine - Setup"
echo "========================================"
echo ""

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK is not installed!"
    echo "Please download .NET 6.0 from: https://dotnet.microsoft.com/"
    exit 1
fi

echo "[1/4] Checking .NET installation..."
dotnet --version

echo ""
echo "[2/4] Restoring NuGet packages..."
dotnet restore
if [ $? -ne 0 ]; then
    echo "ERROR: Failed to restore packages"
    exit 1
fi

echo ""
echo "[3/4] Building project..."
dotnet build
if [ $? -ne 0 ]; then
    echo "ERROR: Build failed"
    exit 1
fi

echo ""
echo "[4/4] Setup complete!"
echo ""
echo "========================================"
echo "Setup Successful!"
echo "========================================"
echo ""
echo "You can now run the game with: dotnet run"
echo "For detailed instructions, see: QUICKSTART.md"
echo ""
