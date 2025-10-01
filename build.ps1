# BatterySystemMod Build Script
# Builds both client (C#) and server (TypeScript) portions into proper SPT mod structure

$ErrorActionPreference = "Stop"
$ModName = "Jiro-BatterySystem"
$OutputDir = ".\dist"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "BatterySystemMod Build Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Always clean output directory for fresh build
if (Test-Path $OutputDir) {
    Write-Host "`nCleaning output directory..." -ForegroundColor Yellow
    Remove-Item -Path $OutputDir -Recurse -Force
}

# Create output directory structure
Write-Host "`nCreating output directory structure..." -ForegroundColor Yellow
$Paths = @(
    "$OutputDir\BepInEx\plugins\$ModName",
    "$OutputDir\user\mods\$ModName\src",
    "$OutputDir\user\mods\$ModName\config",
    "$OutputDir\user\mods\$ModName\bundles"
)

foreach ($Path in $Paths) {
    if (-not (Test-Path $Path)) {
        New-Item -Path $Path -ItemType Directory -Force | Out-Null
    }
}

Write-Host "Output structure created at: $OutputDir" -ForegroundColor Green

# Build Client (C# DLL)
Write-Host "`n=====================================" -ForegroundColor Cyan
Write-Host "Building Client (C# Plugin)" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

Push-Location "BatterySystemClient"

try {
    Write-Host "Building C# project..." -ForegroundColor Yellow

    # Try to find MSBuild
    $MSBuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2>$null | Select-Object -First 1

    if ($MSBuildPath) {
        Write-Host "Using MSBuild: $MSBuildPath" -ForegroundColor Gray
        # Disable post-build event since it references a hardcoded path
        & $MSBuildPath BatterySystemClient.csproj /p:Configuration=Release /p:Platform=AnyCPU /p:PostBuildEvent= /v:minimal /nologo
    } else {
        Write-Host "MSBuild not found. Trying dotnet build..." -ForegroundColor Yellow
        # Disable post-build event since it references a hardcoded path
        dotnet build BatterySystemClient.csproj -c Release /p:PostBuildEvent=
    }

    # Check if DLL was actually built (ignore post-build event errors)
    $DllPath = "bin\Release\$ModName.dll"
    if (-not (Test-Path $DllPath)) {
        throw "Client build failed - DLL not found"
    }

    # Copy DLL to output
    $DllPath = "bin\Release\$ModName.dll"
    if (Test-Path $DllPath) {
        Copy-Item -Path $DllPath -Destination "..\$OutputDir\BepInEx\plugins\$ModName\$ModName.dll" -Force
        Write-Host "Client DLL copied to output" -ForegroundColor Green
    } else {
        throw "Client DLL not found at $DllPath"
    }
} catch {
    Write-Host "Error building client: $_" -ForegroundColor Red
    Pop-Location
    exit 1
} finally {
    Pop-Location
}

# Build Server (TypeScript)
Write-Host "`n=====================================" -ForegroundColor Cyan
Write-Host "Building Server (TypeScript Mod)" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

Push-Location "BatterySystemServer"

try {
    # Check if node_modules exists
    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing dependencies..." -ForegroundColor Yellow
        npm install
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install server dependencies"
        }
    }

    # Compile TypeScript
    Write-Host "Compiling TypeScript..." -ForegroundColor Yellow
    npx tsc 2>&1 | Out-Null  # Suppress tsyringe warnings

    # Check if output exists
    if (-not (Test-Path "tmp\src\batterySystem.js")) {
        throw "TypeScript compilation failed - output file not found"
    }

    Write-Host "TypeScript compiled successfully" -ForegroundColor Green

    # Copy server files to output
    Write-Host "Copying server files to output..." -ForegroundColor Yellow

    # Copy TypeScript source (SPT needs the .ts file in src/)
    Copy-Item -Path "src\batterySystem.ts" -Destination "..\$OutputDir\user\mods\$ModName\src\batterySystem.ts" -Force

    # Copy compiled JavaScript (SPT uses this)
    Copy-Item -Path "tmp\src\batterySystem.js" -Destination "..\$OutputDir\user\mods\$ModName\src\batterySystem.js" -Force

    # Copy config folder
    if (Test-Path "config") {
        Copy-Item -Path "config\*" -Destination "..\$OutputDir\user\mods\$ModName\config\" -Recurse -Force
    }

    # Copy bundles folder
    if (Test-Path "bundles") {
        Copy-Item -Path "bundles\*" -Destination "..\$OutputDir\user\mods\$ModName\bundles\" -Recurse -Force
    }

    # Copy bundles.json
    if (Test-Path "bundles.json") {
        Copy-Item -Path "bundles.json" -Destination "..\$OutputDir\user\mods\$ModName\bundles.json" -Force
    }

    # Copy package.json
    Copy-Item -Path "package.json" -Destination "..\$OutputDir\user\mods\$ModName\package.json" -Force

    Write-Host "Server files copied to output" -ForegroundColor Green

} catch {
    Write-Host "Error building server: $_" -ForegroundColor Red
    Pop-Location
    exit 1
} finally {
    Pop-Location
}

# Build complete
Write-Host "`n=====================================" -ForegroundColor Green
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host "`nOutput location: $OutputDir" -ForegroundColor Cyan
Write-Host "`nMod structure:" -ForegroundColor Cyan
Write-Host "  BepInEx\plugins\$ModName\$ModName.dll (Client)" -ForegroundColor White
Write-Host "  user\mods\$ModName\ (Server)" -ForegroundColor White
Write-Host "`nTo install: Copy the contents of '$OutputDir' to your SPT installation folder" -ForegroundColor Yellow
