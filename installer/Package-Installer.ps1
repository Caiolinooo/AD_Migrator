# Package existing builds into installer
param([string]$Version = "1.0.1")

Write-Host "Creating installer package..." -ForegroundColor Cyan

# Paths
$root = Split-Path $PSScriptRoot -Parent
$out = "$PSScriptRoot\output"
$pkg = "$out\package"

# Create dirs
New-Item -ItemType Directory -Path "$pkg\Manager" -Force | Out-Null
New-Item -ItemType Directory -Path "$pkg\Agent" -Force | Out-Null
New-Item -ItemType Directory -Path "$pkg\Documentation" -Force | Out-Null

# Copy Manager
Write-Host "Copying Manager..."
Copy-Item "$root\ui-wpf\bin\Release\net8.0-windows\*" "$pkg\Manager" -Recurse -Force
Write-Host "OK" -ForegroundColor Green

# Copy Agent
Write-Host "Copying Agent..."
Copy-Item "$root\agent\publish\MigracaoAD.Agent.exe" "$pkg\Agent" -Force
Copy-Item "$root\agent\install-agent.ps1" "$pkg\Agent" -Force
Copy-Item "$root\agent\uninstall-agent.ps1" "$pkg\Agent" -Force
Write-Host "OK" -ForegroundColor Green

# Copy Docs
Write-Host "Copying docs..."
Copy-Item "$root\README.md" "$pkg\Documentation" -Force
Copy-Item "$root\README_AGENTE.md" "$pkg\Documentation" -Force
Copy-Item "$root\COMO_FUNCIONA_AGENTE.md" "$pkg\Documentation" -Force
Copy-Item "$root\agent\INSTALACAO_RAPIDA.md" "$pkg\Documentation" -Force
Write-Host "OK" -ForegroundColor Green

# Create Install.ps1
$install = @'
# AD Migration Suite Installer
param([switch]$Manager,[switch]$Agent,[switch]$All,[string]$InstallPath="$env:ProgramFiles\AD Migration Suite",[string]$Token="default-token",[switch]$Launch)
if(-not([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){Write-Host "Run as Administrator!" -ForegroundColor Red;exit 1}
if(-not$Manager-and-not$Agent-and-not$All){$c=Read-Host "1=Manager 2=Agent 3=Both";if($c-eq"1"){$Manager=$true}elseif($c-eq"2"){$Agent=$true}else{$All=$true}}
if($All){$Manager=$true;$Agent=$true}
New-Item -ItemType Directory -Path $InstallPath -Force|Out-Null
if($Manager){Write-Host "Installing Manager...";$m="$InstallPath\Manager";New-Item -ItemType Directory -Path $m -Force|Out-Null;Copy-Item ".\Manager\*" $m -Recurse -Force;$s=New-Object -ComObject WScript.Shell;$sm="$env:ProgramData\Microsoft\Windows\Start Menu\Programs\AD Migration Suite";New-Item -ItemType Directory -Path $sm -Force|Out-Null;$l=$s.CreateShortcut("$sm\AD Migration Suite.lnk");$l.TargetPath="$m\MigracaoAD.UI.exe";$l.Save();$d=$s.CreateShortcut("$env:Public\Desktop\AD Migration Suite.lnk");$d.TargetPath="$m\MigracaoAD.UI.exe";$d.Save();Write-Host "Manager installed" -ForegroundColor Green}
if($Agent){Write-Host "Installing Agent...";$a="$InstallPath\Agent";New-Item -ItemType Directory -Path $a -Force|Out-Null;Copy-Item ".\Agent\*" $a -Recurse -Force;if($Token-eq"default-token"){$Token=Read-Host "Token"}[Environment]::SetEnvironmentVariable("AGENT_TOKEN",$Token,"Machine");New-NetFirewallRule -DisplayName "AD Migration Agent" -Direction Inbound -Protocol TCP -LocalPort 8765 -Action Allow -ErrorAction SilentlyContinue|Out-Null;$svc=Get-Service "MigracaoADAgent" -ErrorAction SilentlyContinue;if($svc){Stop-Service "MigracaoADAgent" -Force -ErrorAction SilentlyContinue};New-Service -Name "MigracaoADAgent" -BinaryPathName "$a\MigracaoAD.Agent.exe" -DisplayName "AD Migration Agent" -StartupType Automatic -ErrorAction SilentlyContinue|Out-Null;Start-Service "MigracaoADAgent" -ErrorAction SilentlyContinue;Write-Host "Agent installed (Token: $Token)" -ForegroundColor Green}
$doc="$InstallPath\Documentation";New-Item -ItemType Directory -Path $doc -Force|Out-Null;Copy-Item ".\Documentation\*" $doc -Recurse -Force
Write-Host "`nInstalled to: $InstallPath" -ForegroundColor Green
if($Launch-and$Manager){Start-Process "$InstallPath\Manager\MigracaoAD.UI.exe"}
'@
Set-Content "$pkg\Install.ps1" $install

# Create README
$readme = @"
AD Migration Suite v$Version

INSTALL:
1. Extract ZIP
2. Run Install.ps1 as Administrator

OPTIONS:
.\Install.ps1 -Manager          # Manager only
.\Install.ps1 -Agent -Token "x" # Agent only
.\Install.ps1 -All              # Both
.\Install.ps1 -Manager -Launch  # Launch after install

SUPPORT: https://admigration.example.com
"@
Set-Content "$pkg\README.txt" $readme

# Create ZIP
Write-Host "Creating ZIP..."
$zip = "$out\ADMigrationSuite-$Version-Setup.zip"
if(Test-Path $zip){Remove-Item $zip -Force}
Compress-Archive -Path "$pkg\*" -DestinationPath $zip -CompressionLevel Optimal
$size = [math]::Round((Get-Item $zip).Length/1MB,2)
Write-Host "Done!" -ForegroundColor Green
Write-Host "`nPackage: $zip"
Write-Host "Size: $size MB"

