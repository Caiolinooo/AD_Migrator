# AD Migration Suite - Installer Creator
# Creates a portable ZIP package with installation script

param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================"
Write-Host "AD Migration Suite - Installer Creator"
Write-Host "========================================"
Write-Host ""

# Paths
$rootDir = Split-Path $PSScriptRoot -Parent
$outputDir = Join-Path $PSScriptRoot "output"
$packageDir = Join-Path $outputDir "package"

# Create directories
Write-Host "Creating directories..."
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
New-Item -ItemType Directory -Path "$packageDir\Manager" -Force | Out-Null
New-Item -ItemType Directory -Path "$packageDir\Agent" -Force | Out-Null
New-Item -ItemType Directory -Path "$packageDir\Documentation" -Force | Out-Null
Write-Host "OK" -ForegroundColor Green
Write-Host ""

# Build Manager
Write-Host "Building Manager..."
Set-Location "$rootDir\ui-wpf"
dotnet publish MigracaoAD.UI.csproj -c Release -r win-x64 --self-contained false | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK" -ForegroundColor Green
} else {
    Write-Host "FAILED" -ForegroundColor Red
    exit 1
}
Set-Location $PSScriptRoot
Write-Host ""

# Build Agent
Write-Host "Building Agent..."
Set-Location "$rootDir\agent\MigracaoAD.Agent"
dotnet publish MigracaoAD.Agent.csproj -c Release -r win-x64 --self-contained true -o "$rootDir\agent\publish" | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK" -ForegroundColor Green
} else {
    Write-Host "FAILED" -ForegroundColor Red
    exit 1
}
Set-Location $PSScriptRoot
Write-Host ""

# Copy files
Write-Host "Copying Manager files..."
Copy-Item -Path "$rootDir\ui-wpf\bin\Release\net8.0-windows\*" -Destination "$packageDir\Manager" -Recurse -Force
Write-Host "OK" -ForegroundColor Green

Write-Host "Copying Agent files..."
Copy-Item -Path "$rootDir\agent\publish\MigracaoAD.Agent.exe" -Destination "$packageDir\Agent" -Force
Copy-Item -Path "$rootDir\agent\install-agent.ps1" -Destination "$packageDir\Agent" -Force
Copy-Item -Path "$rootDir\agent\uninstall-agent.ps1" -Destination "$packageDir\Agent" -Force
Write-Host "OK" -ForegroundColor Green

Write-Host "Copying documentation..."
Copy-Item -Path "$rootDir\README.md" -Destination "$packageDir\Documentation" -Force
Copy-Item -Path "$rootDir\CHANGELOG.md" -Destination "$packageDir\Documentation" -Force -ErrorAction SilentlyContinue
Copy-Item -Path "$rootDir\README_AGENTE.md" -Destination "$packageDir\Documentation" -Force
Copy-Item -Path "$rootDir\COMO_FUNCIONA_AGENTE.md" -Destination "$packageDir\Documentation" -Force
Copy-Item -Path "$rootDir\agent\INSTALACAO_RAPIDA.md" -Destination "$packageDir\Documentation" -Force
Write-Host "OK" -ForegroundColor Green
Write-Host ""

# Create Install.ps1
Write-Host "Creating installer script..."
$installScript = @'
# AD Migration Suite Installer
param(
    [switch]$Manager,
    [switch]$Agent,
    [switch]$All,
    [string]$InstallPath = "$env:ProgramFiles\AD Migration Suite",
    [string]$AgentToken = "default-token-change-me",
    [switch]$LaunchManager
)

# Check admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: Administrator privileges required!" -ForegroundColor Red
    exit 1
}

Write-Host "AD Migration Suite Installer" -ForegroundColor Cyan
Write-Host ""

# Determine components
if (-not $Manager -and -not $Agent -and -not $All) {
    Write-Host "Select components to install:"
    Write-Host "1. Management Console"
    Write-Host "2. Agent Service"
    Write-Host "3. Both"
    $choice = Read-Host "Choice (1-3)"
    switch ($choice) {
        "1" { $Manager = $true }
        "2" { $Agent = $true }
        "3" { $All = $true }
    }
}

if ($All) { $Manager = $true; $Agent = $true }

# Create install directory
New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null

# Install Manager
if ($Manager) {
    Write-Host "Installing Manager..." -ForegroundColor Yellow
    $managerPath = "$InstallPath\Manager"
    New-Item -ItemType Directory -Path $managerPath -Force | Out-Null
    Copy-Item -Path ".\Manager\*" -Destination $managerPath -Recurse -Force
    
    # Shortcuts
    $WshShell = New-Object -ComObject WScript.Shell
    $startMenu = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\AD Migration Suite"
    New-Item -ItemType Directory -Path $startMenu -Force | Out-Null
    
    $shortcut = $WshShell.CreateShortcut("$startMenu\AD Migration Suite.lnk")
    $shortcut.TargetPath = "$managerPath\MigracaoAD.UI.exe"
    $shortcut.Save()
    
    $desktop = $WshShell.CreateShortcut("$env:Public\Desktop\AD Migration Suite.lnk")
    $desktop.TargetPath = "$managerPath\MigracaoAD.UI.exe"
    $desktop.Save()
    
    Write-Host "Manager installed" -ForegroundColor Green
}

# Install Agent
if ($Agent) {
    Write-Host "Installing Agent..." -ForegroundColor Yellow
    $agentPath = "$InstallPath\Agent"
    New-Item -ItemType Directory -Path $agentPath -Force | Out-Null
    Copy-Item -Path ".\Agent\*" -Destination $agentPath -Recurse -Force
    
    if ($AgentToken -eq "default-token-change-me") {
        $AgentToken = Read-Host "Enter agent token"
    }
    
    [Environment]::SetEnvironmentVariable("AGENT_TOKEN", $AgentToken, "Machine")
    
    # Firewall
    New-NetFirewallRule -DisplayName "AD Migration Agent" -Direction Inbound -Protocol TCP -LocalPort 8765 -Action Allow -ErrorAction SilentlyContinue | Out-Null
    
    # Service
    $svc = Get-Service -Name "MigracaoADAgent" -ErrorAction SilentlyContinue
    if ($svc) {
        Stop-Service -Name "MigracaoADAgent" -Force -ErrorAction SilentlyContinue
    }
    
    New-Service -Name "MigracaoADAgent" -BinaryPathName "$agentPath\MigracaoAD.Agent.exe" -DisplayName "AD Migration Agent" -StartupType Automatic -ErrorAction SilentlyContinue | Out-Null
    Start-Service -Name "MigracaoADAgent" -ErrorAction SilentlyContinue
    
    Write-Host "Agent installed (Token: $AgentToken)" -ForegroundColor Green
}

# Documentation
$docsPath = "$InstallPath\Documentation"
New-Item -ItemType Directory -Path $docsPath -Force | Out-Null
Copy-Item -Path ".\Documentation\*" -Destination $docsPath -Recurse -Force

Write-Host ""
Write-Host "Installation complete!" -ForegroundColor Green
Write-Host "Path: $InstallPath"

if ($LaunchManager -and $Manager) {
    Start-Process "$InstallPath\Manager\MigracaoAD.UI.exe"
}
'@

Set-Content -Path "$packageDir\Install.ps1" -Value $installScript -Force
Write-Host "OK" -ForegroundColor Green
Write-Host ""

# Create README
$readme = @"
AD Migration Suite - Installation Package
Version: $Version

INSTALLATION:
1. Extract this ZIP file
2. Right-click Install.ps1 and select "Run with PowerShell"
3. Follow the prompts

COMMAND LINE OPTIONS:
- Install Manager only:  .\Install.ps1 -Manager
- Install Agent only:    .\Install.ps1 -Agent -AgentToken "your-token"
- Install both:          .\Install.ps1 -All
- Launch after install:  .\Install.ps1 -Manager -LaunchManager

CONTENTS:
- Manager/         - Management Console
- Agent/           - Agent Service
- Documentation/   - User guides
- Install.ps1      - Installation script

SUPPORT:
- Website: https://admigration.example.com
- Email: support@admigration.example.com

Copyright © 2025 AD Migration Suite Team
"@

Set-Content -Path "$packageDir\README.txt" -Value $readme -Force

# Create ZIP
Write-Host "Creating ZIP package..."
$zipPath = Join-Path $outputDir "ADMigrationSuite-$Version-Setup.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path "$packageDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "OK" -ForegroundColor Green
Write-Host ""

# Summary
$zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host "========================================"
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "========================================"
Write-Host ""
Write-Host "Package: $zipPath"
Write-Host "Size: $zipSize MB"
Write-Host ""
Write-Host "To install:"
Write-Host "  1. Extract ZIP"
Write-Host "  2. Run Install.ps1 as Administrator"
Write-Host ""

