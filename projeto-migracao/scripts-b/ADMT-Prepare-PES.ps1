param(
    [Parameter(Mandatory=$true)][string]$SourceDomain,
    [Parameter(Mandatory=$true)][string]$TargetDomain,
    [string]$KeyFilePath = "..\outputs\admt\pes.key",
    [string]$PesMsiPath = "",
    [string]$SourcePDC = ""
)

$ErrorActionPreference = 'Stop'

function Test-AdmtInstalled {
    $cmd = Get-Command admt -ErrorAction SilentlyContinue
    if ($null -eq $cmd) { return $false }
    return $true
}

# Ensure output folder exists
$newDir = Split-Path -Parent $KeyFilePath
if (-not (Test-Path -LiteralPath $newDir)) { New-Item -ItemType Directory -Path $newDir -Force | Out-Null }

if (-not (Test-AdmtInstalled)) {
    Write-Warning "ADMT não encontrado nesta máquina (target). Pulando etapa de geração de chave PES. Instale o ADMT 3.2 e reexecute."
    exit 0
}

Write-Host "Gerando chave de criptografia do PES para $SourceDomain" -ForegroundColor Cyan
admt key /option:create /sourcedomain:"$SourceDomain" /keyfile:"$KeyFilePath" /pwd:* | Write-Output

if (Test-Path -LiteralPath $KeyFilePath) {
    Write-Host "Chave gerada: $KeyFilePath" -ForegroundColor Green
} else {
    Write-Warning "Falha ao gerar a chave PES (arquivo não encontrado após execução). Verifique permissões."
}

if ([string]::IsNullOrWhiteSpace($PesMsiPath)) {
    Write-Host "PES: instale manualmente no PDC do domínio origem ($SourceDomain). Copie a chave para o PDC e aponte no wizard." -ForegroundColor Yellow
    Write-Host "Arquivo da chave: $KeyFilePath" -ForegroundColor Yellow
    exit 0
}

if (-not (Test-Path -LiteralPath $PesMsiPath)) {
    Write-Warning "Pacote PES não encontrado: $PesMsiPath. Pulando instalação."
    exit 0
}

if ([string]::IsNullOrWhiteSpace($SourcePDC)) {
    Write-Host "Para instalação remota silenciosa do PES, informe -SourcePDC <hostname> e garanta WinRM habilitado." -ForegroundColor Yellow
    exit 0
}

try {
    Write-Host "Tentando instalar PES remotamente em $SourcePDC (msiexec /qn)." -ForegroundColor Cyan
    Invoke-Command -ComputerName $SourcePDC -ScriptBlock {
        param($msi)
        Start-Process msiexec.exe -ArgumentList "/i `"$msi`" /qn" -Wait -PassThru | Out-Null
    } -ArgumentList $PesMsiPath -ErrorAction Stop
    Write-Host "PES instalado em $SourcePDC. Abra o wizard do PES e importe a chave: $using:KeyFilePath" -ForegroundColor Green
} catch {
    Write-Warning "Falha ao instalar PES remotamente: $_"
}

exit 0

