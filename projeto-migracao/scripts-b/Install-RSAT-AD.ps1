$ErrorActionPreference = 'Stop'
Write-Host "Garantindo RSAT-AD-PowerShell..." -ForegroundColor Cyan
try {
    Import-Module ActiveDirectory -ErrorAction Stop
    Write-Host "RSAT-AD j\u00e1 dispon\u00edvel." -ForegroundColor Green
    exit 0
} catch {}

# Server feature name on 2016/2019/2022
try {
    Add-WindowsFeature RSAT-AD-PowerShell -ErrorAction Stop | Out-Null
} catch {
    try { Install-WindowsFeature RSAT-AD-PowerShell -ErrorAction Stop | Out-Null } catch {}
}

try { Import-Module ActiveDirectory -ErrorAction Stop; Write-Host "RSAT-AD instalado." -ForegroundColor Green } catch { Write-Warning $_ }
exit 0

