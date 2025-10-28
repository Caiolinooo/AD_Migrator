param(
    [string]$ParamsPath = "..\config\parameters.psd1",
    [string]$DomainName = "",
    [string]$SiteName = "",
    [string]$OldDCHostname = "",
    [string]$NewDCHostname = "",
    [string]$SourceFileServer = "",
    [string]$DestinationFileServer = "",
    [string]$DestinationRootPath = "E:\\Shares",
    [bool]$CreateDestinationShares = $true,
    [bool]$UseRemoting = $false
)

$ErrorActionPreference = 'Stop'

$ht = @{
    DomainName              = $DomainName
    SiteName                = $SiteName
    OldDCHostname           = $OldDCHostname
    NewDCHostname           = $NewDCHostname
    DnsServer               = $null

    DatabasePath            = 'D:\\NTDS'
    LogPath                 = 'E:\\Logs'
    SYSVOLPath              = 'D:\\SYSVOL'

    SourceFileServer        = $SourceFileServer
    DestinationFileServer   = $DestinationFileServer
    DestinationRootPath     = $DestinationRootPath
    CreateDestinationShares = $CreateDestinationShares

    FileCopyThreads         = 32
    RobocopyRetry           = 2
    RobocopyWait            = 2

    AutoProceed             = $true
    UseRemoting             = $UseRemoting

    OutputsDir              = '..\\outputs'
    MapsCsv                 = '..\\samples\\maps.csv'
}

function Convert-ToPsd1Value([object]$val){
    if ($null -eq $val) { return '$null' }
    if ($val -is [bool]) { return ($val ? '$true' : '$false') }
    if ($val -is [string]) { return "'" + $val.Replace("'", "''") + "'" }
    return $val.ToString()
}

$newDir = Split-Path -Parent $ParamsPath
if (-not (Test-Path -LiteralPath $newDir)) { New-Item -ItemType Directory -Path $newDir -Force | Out-Null }

$lines = $ht.GetEnumerator() | Sort-Object Name | ForEach-Object { "    {0} = {1}" -f $_.Name, (Convert-ToPsd1Value $_.Value) }
$content = "@{`r`n" + ($lines -join "`r`n") + "`r`n}`r`n"

Set-Content -Path $ParamsPath -Value $content -Encoding UTF8
Write-Host "parameters.psd1 atualizado em $ParamsPath" -ForegroundColor Green

