@echo off
REM Space Trade Engine Setup Script for Windows

echo.
echo ========================================
echo Space Trade Engine - Setup
echo ========================================
echo.

REM Check if .NET is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK is not installed!
    echo Please download .NET 6.0 from: https://dotnet.microsoft.com/
    pause
    exit /b 1
)

echo [1/4] Checking .NET installation...
dotnet --version

echo.
echo [2/4] Restoring NuGet packages...
dotnet restore
if errorlevel 1 (
    echo ERROR: Failed to restore packages
    pause
    exit /b 1
)

echo.
echo [3/4] Building project...
dotnet build
if errorlevel 1 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

echo.
echo [4/4] Setup complete!
echo.
echo ========================================
echo Setup Successful!
echo ========================================
echo.
echo You can now run the game with: dotnet run
echo For detailed instructions, see: QUICKSTART.md
echo.
pause
