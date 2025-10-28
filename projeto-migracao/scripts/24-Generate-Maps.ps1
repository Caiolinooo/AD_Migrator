param(
    [string]$ParamsPath = "..\config\parameters.psd1",
    [string]$SharesCsv  = "..\outputs\FS-Shares.csv",
    [string]$OutCsv     = "..\samples\maps.csv"
)

$ErrorActionPreference = 'Stop'

$params = Import-PowerShellDataFile -Path $ParamsPath

if (-not (Test-Path -LiteralPath $SharesCsv)) {
    throw "Arquivo de shares não encontrado: $SharesCsv (execute 02-Discovery-FileShares.ps1)"
}

$shares = Import-Csv -Path $SharesCsv

# Gera o CSV no formato Orig,Dest,Log
$new = @()
foreach($s in $shares){
    $name = $s.Name
    if ([string]::IsNullOrWhiteSpace($name)) { continue }
    $orig = "\\\\{0}\\{1}" -f $params.SourceFileServer, $name
    $dest = "\\\\{0}\\{1}" -f $params.DestinationFileServer, $name
    $log  = "C:\\Logs\\{0}.log" -f $name
    $new += [pscustomobject]@{ Orig=$orig; Dest=$dest; Log=$log }
}

# Cria diretório de logs no destino (localmente ou remoto, deixe para Robocopy criar em runtime se não existir)
$new | Export-Csv -Path $OutCsv -NoTypeInformation -Encoding UTF8

Write-Host "Mapa gerado: $OutCsv" -ForegroundColor Green
