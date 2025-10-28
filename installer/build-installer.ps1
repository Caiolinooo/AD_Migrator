# Build AD Migration Suite Installer
# This script builds the MSI installer using WiX Toolset

param(
    [string]$Version = "1.0.0",
    [string]$OutputDir = ".\output"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "AD Migration Suite - Installer Builder" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if WiX is installed
Write-Host "Checking for WiX Toolset..." -ForegroundColor Yellow
$wixPath = "C:\Program Files (x86)\WiX Toolset v3.11\bin"
if (-not (Test-Path $wixPath)) {
    Write-Host "ERROR: WiX Toolset not found!" -ForegroundColor Red
    Write-Host "Please install WiX Toolset v3.11 from: https://wixtoolset.org/releases/" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Alternative: Using dotnet wix extension..." -ForegroundColor Yellow
    
    # Try dotnet wix
    try {
        dotnet tool install --global wix
        $wixPath = "$env:USERPROFILE\.dotnet\tools"
    } catch {
        Write-Host "Failed to install WiX via dotnet. Please install manually." -ForegroundColor Red
        exit 1
    }
}

$candle = Join-Path $wixPath "candle.exe"
$light = Join-Path $wixPath "light.exe"

Write-Host "✓ WiX Toolset found at: $wixPath" -ForegroundColor Green
Write-Host ""

# Create output directory
Write-Host "Creating output directory..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
Write-Host "✓ Output directory: $OutputDir" -ForegroundColor Green
Write-Host ""

# Build Manager (WPF App)
Write-Host "Building Manager application..." -ForegroundColor Yellow
Push-Location ..\ui-wpf
try {
    dotnet publish MigracaoAD.UI.csproj -c Release -r win-x64 --self-contained false -o bin\Release\net8.0-windows\
    if ($LASTEXITCODE -ne 0) { throw "Manager build failed" }
    Write-Host "✓ Manager built successfully" -ForegroundColor Green
} catch {
    Write-Host "✗ Manager build failed: $_" -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location
Write-Host ""

# Build Agent (Windows Service)
Write-Host "Building Agent service..." -ForegroundColor Yellow
Push-Location ..\agent\MigracaoAD.Agent
try {
    dotnet publish MigracaoAD.Agent.csproj -c Release -r win-x64 --self-contained true -o ..\..\agent\publish
    if ($LASTEXITCODE -ne 0) { throw "Agent build failed" }
    Write-Host "✓ Agent built successfully" -ForegroundColor Green
} catch {
    Write-Host "✗ Agent build failed: $_" -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location
Write-Host ""

# Compile WiX source
Write-Host "Compiling WiX source..." -ForegroundColor Yellow
$wixObj = Join-Path $OutputDir "ADMigrationSuite.wixobj"
try {
    & $candle ADMigrationSuite.wxs -out $wixObj -ext WixUIExtension -ext WixUtilExtension
    if ($LASTEXITCODE -ne 0) { throw "Candle compilation failed" }
    Write-Host "✓ WiX source compiled" -ForegroundColor Green
} catch {
    Write-Host "✗ WiX compilation failed: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Link MSI
Write-Host "Linking MSI installer..." -ForegroundColor Yellow
$msiFile = Join-Path $OutputDir "ADMigrationSuite-$Version.msi"
try {
    & $light $wixObj -out $msiFile -ext WixUIExtension -ext WixUtilExtension -cultures:en-US
    if ($LASTEXITCODE -ne 0) { throw "Light linking failed" }
    Write-Host "✓ MSI installer created" -ForegroundColor Green
} catch {
    Write-Host "✗ MSI linking failed: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Installer: $msiFile" -ForegroundColor Yellow
Write-Host "Size: $((Get-Item $msiFile).Length / 1MB) MB" -ForegroundColor Yellow
Write-Host ""
Write-Host "To install:" -ForegroundColor Cyan
Write-Host "  msiexec /i `"$msiFile`"" -ForegroundColor White
Write-Host ""
Write-Host "To install silently:" -ForegroundColor Cyan
Write-Host "  msiexec /i `"$msiFile`" /quiet /qn" -ForegroundColor White
Write-Host ""
Write-Host "To install only Manager:" -ForegroundColor Cyan
Write-Host "  msiexec /i `"$msiFile`" ADDLOCAL=MANAGER_FEATURE" -ForegroundColor White
Write-Host ""
Write-Host "To install only Agent:" -ForegroundColor Cyan
Write-Host "  msiexec /i `"$msiFile`" ADDLOCAL=AGENT_FEATURE AGENT_TOKEN=your-token-123" -ForegroundColor White
Write-Host ""

