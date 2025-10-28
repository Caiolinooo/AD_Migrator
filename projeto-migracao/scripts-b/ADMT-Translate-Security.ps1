param(
    [Parameter(Mandatory=$true)][string]$SourceDomain,
    [Parameter(Mandatory=$true)][string]$TargetDomain,
    [string]$IncludeFile = "",
    [ValidateSet('Add','Remove','Replace')][string]$Mode = 'Add'
)

$ErrorActionPreference = 'Stop'

function Test-AdmtInstalled { if (Get-Command admt -ErrorAction SilentlyContinue) { $true } else { $false } }

if (-not (Test-AdmtInstalled)) { Write-Warning "ADMT n\u00e3o encontrado. Pulando Translate Security."; exit 0 }

if ([string]::IsNullOrWhiteSpace($IncludeFile)) {
    Write-Host "Sem IncludeFile (lista de m\u00e1quinas/paths). Etapa Translate Security SKIP." -ForegroundColor Yellow
    exit 0
}

$tmp = New-Item -ItemType Directory -Path (Join-Path $env:TEMP "admt") -Force
$opt = Join-Path $tmp.FullName "translate.opt"
$optContent = @()
$optContent += "[Migration]"
$optContent += 'IntraForest=No'
$optContent += "SourceDomain=\"$SourceDomain\""
$optContent += "TargetDomain=\"$TargetDomain\""
$optContent += ("TranslateOption={0}" -f $Mode)
$optContent += 'TranslateSecurity=Yes'
$optContent += 'TranslateFilesAndFolders=Yes'
$optContent += 'TranslateLocalGroups=Yes'
$optContent += 'TranslatePrinters=Yes'
$optContent += 'TranslateRegistry=Yes'
$optContent += 'TranslateUserProfiles=Yes'
$optContent += 'TranslateShares=Yes'
$optContent += 'TranslateServices=Yes'
$optContent += 'TranslateScheduledTasks=Yes'
Set-Content -Path $opt -Value ($optContent -join "`r`n") -Encoding ASCII

Write-Host "ADMT: Translate Security ($Mode) usando IncludeFile $IncludeFile" -ForegroundColor Cyan
admt security /o:"$opt" /f:"$IncludeFile"

Write-Host "Translate Security conclu\u00eddo." -ForegroundColor Green
exit 0

