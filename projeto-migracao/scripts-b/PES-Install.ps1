param(
    [Parameter(Mandatory=$true)][string]$SourcePDC,
    [string]$PesMsiPath = "..\installers\PES\Pwdmig.msi",
    [string]$KeyFilePath = "..\outputs\admt\pes.key",
    [switch]$PromptCredential
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PesMsiPath)) {
    Write-Warning "PES MSI n\u00e3o encontrado em $PesMsiPath. Copie o instalador para esta pasta e reexecute."
    exit 0
}

$cred = $null
if ($PromptCredential) { $cred = Get-Credential }

# Copiar MSI e chave para o PDC (C:\Temp)
$session = New-PSSession -ComputerName $SourcePDC -Credential $cred -ErrorAction Stop
try {
    Invoke-Command -Session $session -ScriptBlock { New-Item -ItemType Directory -Path C:\Temp -Force | Out-Null }
    Copy-Item -Path $PesMsiPath -Destination "C:\Temp\Pwdmig.msi" -ToSession $session -Force
    if (Test-Path -LiteralPath $KeyFilePath) { Copy-Item -Path $KeyFilePath -Destination "C:\Temp\pes.key" -ToSession $session -Force }

    # Instalar silenciosamente
    Invoke-Command -Session $session -ScriptBlock {
        Start-Process msiexec.exe -ArgumentList "/i C:\Temp\Pwdmig.msi /qn" -Wait
    }

    Write-Host "PES instalado em $SourcePDC. Abra o Password Export Server no PDC e importe a chave (C:\\Temp\\pes.key)." -ForegroundColor Green
} finally {
    Remove-PSSession -Session $session -ErrorAction SilentlyContinue
}

exit 0

