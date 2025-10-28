param(
    [string]$ParamsPath = "..\config\parameters.psd1",
    [string]$OutDir = "..\outputs"
)

$ErrorActionPreference = 'Stop'

function Test-HostReachable {
    param([string]$ComputerName)
    try {
        Test-Connection -ComputerName $ComputerName -Count 1 -Quiet -ErrorAction Stop
    } catch { $false }
}

function Test-DnsResolution {
    param([string]$Name,[string]$Server=$null)
    try {
        if ($Server) { (Resolve-DnsName -Name $Name -Server $Server -ErrorAction Stop) -ne $null }
        else { (Resolve-DnsName -Name $Name -ErrorAction Stop) -ne $null }
    } catch { $false }
}

Import-Module ActiveDirectory -ErrorAction Stop

if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

$params = Import-PowerShellDataFile -Path $ParamsPath

$report = [ordered]@{}
$report.Time = (Get-Date).ToString('s')
$report.DomainName = $params.DomainName
$report.ExpectedNewDC = $params.NewDCHostname
$report.ExpectedOldDC = $params.OldDCHostname
$report.SourceFileServer = $params.SourceFileServer
$report.DestinationFileServer = $params.DestinationFileServer

# AD basic
$domain  = Get-ADDomain
$forest  = Get-ADForest

$report.DomainFunctionalLevel = $domain.DomainMode
$report.ForestFunctionalLevel = $forest.ForestMode
$report.PDCEmulator = $domain.PDCEmulator

# SYSVOL replication tech
$sysvolReplication = "Unknown"
try {
    $dfsrObj = Get-ADObject -LDAPFilter '(CN=DFSR-GlobalSettings)' -SearchBase "CN=System,$($domain.DistinguishedName)" -ErrorAction Stop
    if ($null -ne $dfsrObj) { $sysvolReplication = "DFSR" } else { $sysvolReplication = "FRS" }
} catch { }
$report.SysvolReplication = $sysvolReplication

# Health checks
$report.OldDCReachable = Test-HostReachable -ComputerName $params.OldDCHostname
$report.NewDCReachable = Test-HostReachable -ComputerName $params.NewDCHostname
$report.SourceFSReachable = Test-HostReachable -ComputerName $params.SourceFileServer
$report.DestFSReachable = Test-HostReachable -ComputerName $params.DestinationFileServer

$primaryDns = if ($params.DnsServer) { $params.DnsServer } else { $domain.PDCEmulator }
$report.DnsResolveDomain = Test-DnsResolution -Name $params.DomainName -Server $primaryDns
$report.DnsResolveOldDC = Test-DnsResolution -Name $params.OldDCHostname -Server $primaryDns
$report.DnsResolveNewDC = Test-DnsResolution -Name $params.NewDCHostname -Server $primaryDns

# Output
$reportObj = New-Object psobject -Property $report
$reportObj | ConvertTo-Json -Depth 5 | Out-File (Join-Path $OutDir "Prechecks.json") -Encoding UTF8

Write-Host "Prechecks concluídos. Veja $OutDir/Prechecks.json" -ForegroundColor Green
