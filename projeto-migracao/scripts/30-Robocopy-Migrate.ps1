param(
    [Parameter(Mandatory=$true)][string]$CsvMapPath,  # CSV com colunas: Orig,Dest,Log
    [int]$Retry=2,
    [int]$Wait=2,
    [int]$Threads=32,
    [switch]$NoPreCopy
)

$map = Import-Csv -Path $CsvMapPath

foreach($i in $map){
    $orig = $i.Orig.Trim()
    $dest = $i.Dest.Trim()
    $log  = $i.Log.Trim()

    if (-not (Test-Path -LiteralPath (Split-Path -Path $log -Parent))) { New-Item -ItemType Directory -Path (Split-Path -Path $log -Parent) -Force | Out-Null }

    if (-not $NoPreCopy) {
        Write-Host "Pré-cópia: $orig -> $dest" -ForegroundColor Cyan
        robocopy "$orig" "$dest" /E /COPYALL /DCOPY:DAT /R:$Retry /W:$Wait /MT:$Threads /NFL /NDL /NP /TEE /LOG+:"$log"
    }

    Write-Host "Delta final (corte): $orig -> $dest" -ForegroundColor Yellow
    robocopy "$orig" "$dest" /MIR /COPYALL /DCOPY:DAT /SECFIX /TIMFIX /R:$Retry /W:$Wait /MT:$Threads /TEE /LOG+:"$log"
}

Write-Host "Migração de arquivos concluída" -ForegroundColor Green
