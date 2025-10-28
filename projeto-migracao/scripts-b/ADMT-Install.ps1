param(
    [string]$AdmtMsiPath = "..\installers\ADMT\ADMT.msi"
)

$ErrorActionPreference = 'Stop'

function Test-AdmtInstalled { if (Get-Command admt -ErrorAction SilentlyContinue) { $true } else { $false } }

if (Test-AdmtInstalled) { Write-Host "ADMT já instalado." -ForegroundColor Green; exit 0 }

if (-not (Test-Path -LiteralPath $AdmtMsiPath)) {
    Write-Warning "ADMT MSI não encontrado em $AdmtMsiPath. Coloque o instalador nesta pasta e reexecute."
    exit 0
}

Write-Host "Instalando ADMT (silencioso)..." -ForegroundColor Cyan
Start-Process msiexec.exe -ArgumentList "/i `"$AdmtMsiPath`" /qn" -Wait

Start-Sleep -Seconds 3
if (Test-AdmtInstalled) { Write-Host "ADMT instalado com sucesso." -ForegroundColor Green; exit 0 }
Write-Warning "Falha ao detectar ADMT após instalação. Verifique o MSI/Prereqs (SQL Express, etc.)."
exit 0

