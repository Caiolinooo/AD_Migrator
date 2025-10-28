param(
    [Parameter(Mandatory=$true)][string]$SourceDomain,
    [Parameter(Mandatory=$true)][string]$TargetDomain,
    [string]$SourceOU = "",
    [string]$TargetOU = "",
    [string]$IncludeFile = ""
)

$ErrorActionPreference = 'Stop'

function Test-AdmtInstalled { if (Get-Command admt -ErrorAction SilentlyContinue) { $true } else { $false } }

if (-not (Test-AdmtInstalled)) { Write-Warning "ADMT n\u00e3o encontrado. Pulando migra\u00e7\u00e3o de computadores."; exit 0 }

if ([string]::IsNullOrWhiteSpace($IncludeFile) -and [string]::IsNullOrWhiteSpace($SourceOU)) {
    Write-Host "Sem IncludeFile/SourceOU. Etapa ADMT Computers SKIP." -ForegroundColor Yellow
    exit 0
}

$tmp = New-Item -ItemType Directory -Path (Join-Path $env:TEMP "admt") -Force
$opt = Join-Path $tmp.FullName "computers.opt"
$optContent = @()
$optContent += "[Migration]"
$optContent += 'IntraForest=No'
$optContent += 'MigrateSIDs=Yes'
$optContent += "SourceDomain=\"$SourceDomain\""
$optContent += "TargetDomain=\"$TargetDomain\""
if (-not [string]::IsNullOrWhiteSpace($SourceOU)) { $optContent += "SourceOu=\"$SourceOU\"" }
if (-not [string]::IsNullOrWhiteSpace($TargetOU)) { $optContent += "TargetOu=\"$TargetOU\"" }
$optContent += 'TranslateSecurity=Yes'
$optContent += 'RebootOption=Later'
$optContent += 'UpdatePreviouslyMigratedObjects=Yes'
Set-Content -Path $opt -Value ($optContent -join "`r`n") -Encoding ASCII

if (-not [string]::IsNullOrWhiteSpace($IncludeFile)) {
    Write-Host "ADMT: Migrando computadores via IncludeFile: $IncludeFile" -ForegroundColor Cyan
    admt computer /o:"$opt" /f:"$IncludeFile"
} else {
    Write-Host "ADMT: Gerando include de computadores a partir do OU $SourceOU" -ForegroundColor Cyan
    try {
        Import-Module ActiveDirectory -ErrorAction Stop
        $tmpInc = Join-Path $tmp.FullName "computers.inc"
        $lines = @('SourceName') + ((Get-ADComputer -Filter * -SearchBase $SourceOU -SearchScope Subtree | Select-Object -ExpandProperty Name) | ForEach-Object { $_ })
        Set-Content -Path $tmpInc -Value ($lines -join "`r`n") -Encoding ASCII
        admt computer /o:"$opt" /f:"$tmpInc"
    } catch {
        Write-Warning "Falha ao gerar include (AD RSAT n\u00e3o encontrado). Forne\u00e7a -IncludeFile."
        exit 0
    }
}

Write-Host "Migra\u00e7\u00e3o de computadores ADMT finalizada." -ForegroundColor Green
exit 0

