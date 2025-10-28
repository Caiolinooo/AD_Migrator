param(
  [Parameter(Mandatory=$true)][string]$SourceDomain,   # ex.: abzservicos.local (origem)
  [Parameter(Mandatory=$true)][string]$TargetDomain,   # ex.: corp.local (novo)
  [string]$SourceAdminUser,                            # ex.: ABZSERVICOS\\Administrador
  [string]$TargetAdminUser                             # ex.: CORP\\Administrador
)

$ErrorActionPreference = 'Stop'

function Require-Module($name){ if(-not (Get-Module -ListAvailable -Name $name)){ throw "Módulo '$name' não encontrado. Instale RSAT-AD-PowerShell." } }
Require-Module ActiveDirectory

Write-Host "[Trust] Criando trust bidirecional entre '$SourceDomain' e '$TargetDomain'" -ForegroundColor Cyan

if(-not $SourceAdminUser){ $SourceAdminUser = Read-Host "Informe usuário admin no domínio origem ($SourceDomain)" }
if(-not $TargetAdminUser){ $TargetAdminUser = Read-Host "Informe usuário admin no domínio destino ($TargetDomain)" }
$SrcCred = Get-Credential -UserName $SourceAdminUser -Message "Senha do admin do domínio origem"
$DstCred = Get-Credential -UserName $TargetAdminUser -Message "Senha do admin do domínio destino"

# Criamos dois trusts unidirecionais que somam um bidirecional externo
# Lado destino confiando na origem
Write-Host "[Trust] Criando trust: $TargetDomain confia em $SourceDomain" -ForegroundColor Yellow
New-ADTrust -Name $SourceDomain -SourceForest $TargetDomain -TargetForest $SourceDomain -Direction Inbound -TrustType External -Credential $DstCred -Confirm:$false

# Lado origem confiando no destino
Write-Host "[Trust] Criando trust: $SourceDomain confia em $TargetDomain" -ForegroundColor Yellow
New-ADTrust -Name $TargetDomain -SourceForest $SourceDomain -TargetForest $TargetDomain -Direction Inbound -TrustType External -Credential $SrcCred -Confirm:$false

Write-Host "[Trust] Validando..." -ForegroundColor Yellow
Get-ADTrust -Filter "Name -eq '$TargetDomain' -or Name -eq '$SourceDomain'" | Format-Table Name,Direction,TrustType,ForestTransitive

Write-Host "[Trust] Concluído. Prossiga com enable-sidhistory." -ForegroundColor Green

