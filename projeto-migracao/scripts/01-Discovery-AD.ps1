param(
    [string]$OutDir = ".\outputs",
    [string]$DnsServer = $null
)

# Requires RSAT-AD-PowerShell on the host running this script
Import-Module ActiveDirectory -ErrorAction Stop

# Create output directory
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

# Collect basic AD info
$domain  = Get-ADDomain
$forest  = Get-ADForest
$dcs     = Get-ADDomainController -Filter * | Sort-Object HostName
$trusts  = try { Get-ADTrust -Filter * } catch { @() }
$sites   = Get-ADReplicationSite -Filter * | Sort-Object Name
$subnets = Get-ADReplicationSubnet -Filter * | Sort-Object Name

# Try to collect DNS zones (prefer querying a DNS server if provided or the PDC)
$dnsZones = @()
$dnsTarget = if ($DnsServer) { $DnsServer } else { $domain.PDCEmulator }
try {
    Import-Module DnsServer -ErrorAction Stop
    $dnsZones = Get-DnsServerZone -ComputerName $dnsTarget | Where-Object { -not $_.IsAutoCreated } | Select-Object -ExpandProperty ZoneName
} catch {
    # Fallback: query AD-integrated zones from Directory
    try {
        $zones1 = Get-ADObject -LDAPFilter '(objectClass=dnsZone)' -SearchBase "CN=MicrosoftDNS,DC=DomainDnsZones,$($domain.DistinguishedName)" -SearchScope OneLevel | Select-Object -ExpandProperty Name
        $zones2 = Get-ADObject -LDAPFilter '(objectClass=dnsZone)' -SearchBase "CN=MicrosoftDNS,DC=ForestDnsZones,$($forest.RootDomainNamingContext)" -SearchScope OneLevel | Select-Object -ExpandProperty Name
        $dnsZones = @($zones1 + $zones2 | Sort-Object -Unique)
    } catch {
        $dnsZones = @()
    }
}

# Detect SYSVOL replication technology (DFSR vs FRS)
$sysvolReplication = "Unknown"
try {
    $dfsrObj = Get-ADObject -LDAPFilter '(CN=DFSR-GlobalSettings)' -SearchBase "CN=System,$($domain.DistinguishedName)" -ErrorAction Stop
    if ($null -ne $dfsrObj) { $sysvolReplication = "DFSR" } else { $sysvolReplication = "FRS" }
} catch {
    # If we cannot query, try registry on local machine as a heuristic (requires running on DC)
    try {
        $frsKey = 'HKLM:SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon'
        $sysvolReady = Get-ItemProperty -Path $frsKey -Name 'SysVol' -ErrorAction SilentlyContinue
        # Fallback remains Unknown if we can't determine
    } catch { }
}

# Build summary object
$adSummary = [pscustomobject]@{
    GeneratedAt            = (Get-Date).ToString("s")
    DomainName             = $domain.DNSRoot
    ForestName             = $forest.Name
    DomainFunctionalLevel  = $domain.DomainMode
    ForestFunctionalLevel  = $forest.ForestMode
    PDCEmulator            = $domain.PDCEmulator
    RIDMaster              = $domain.RIDMaster
    InfrastructureMaster   = $domain.InfrastructureMaster
    SchemaMaster           = $forest.SchemaMaster
    DomainNamingMaster     = $forest.DomainNamingMaster
    Sites                  = ($sites.Name -join ",")
    Subnets                = ($subnets.Name -join ",")
    DnsZones               = $dnsZones
    SysvolReplication      = $sysvolReplication
}

$adSummary | ConvertTo-Json -Depth 5 | Out-File (Join-Path $OutDir "AD-Summary.json") -Encoding UTF8

$dcs | Select-Object HostName,IPv4Address,IsGlobalCatalog,Site,OperatingSystem,OperatingSystemVersion | 
    Export-Csv (Join-Path $OutDir "AD-DCs.csv") -NoTypeInformation -Encoding UTF8

$trusts | Select-Object Name,Source,Target,Direction,TrustType,TrustAttributes | 
    Export-Csv (Join-Path $OutDir "AD-Trusts.csv") -NoTypeInformation -Encoding UTF8

$dnsZones | Set-Content -Path (Join-Path $OutDir "AD-DNSZones.txt") -Encoding UTF8

Write-Host "Discovery AD concluído em $OutDir" -ForegroundColor Green
