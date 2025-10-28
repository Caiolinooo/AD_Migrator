param(
    [string]$OutDir = ".\outputs",
    [string[]]$SharesInclude = @(),  # opcional: filtrar nomes de shares
    [switch]$SkipMetrics,
    [string]$ComputerName = "",
    [string]$Username = "",
    [string]$Password = "",
    [switch]$PromptCredential
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

function Collect-Local {
    # List user shares (exclude administrative shares)
    $shares = Get-SmbShare | Where-Object { $_.Name -notin @("ADMIN$", "C$", "IPC$") }
    if ($SharesInclude.Count -gt 0) { $shares = $shares | Where-Object { $SharesInclude -contains $_.Name } }

    $shareInfo = foreach($s in $shares){
        $acc = Get-SmbShareAccess -Name $s.Name | ForEach-Object { "{0}:{1}:{2}" -f $_.AccountName, $_.AccessControlType, $_.AccessRight }
        [pscustomobject]@{
            Name=$s.Name; Path=$s.Path; Description=$s.Description; FolderEnumerationMode=$s.FolderEnumerationMode;
            ConcurrentUserLimit=$s.ConcurrentUserLimit; SharePermissions=($acc -join ";")
        }
    }
    $shareInfo | Export-Csv (Join-Path $OutDir "FS-Shares.csv") -NoTypeInformation -Encoding UTF8

    $ntfs = foreach($s in $shares){
        try {
            $acl = Get-Acl -LiteralPath $s.Path
            $aces = $acl.Access | ForEach-Object { "{0}:{1}" -f $_.IdentityReference, $_.FileSystemRights }
            if ($SkipMetrics) { [pscustomobject]@{ ShareName=$s.Name; Path=$s.Path; AclRoot=($aces -join ";"); FileCount=$null; DirCount=$null; TotalBytes=$null } }
            else {
                $items = Get-ChildItem -LiteralPath $s.Path -Recurse -Force -ErrorAction SilentlyContinue
                $files = $items | Where-Object { -not $_.PSIsContainer }
                $size  = ($files | Measure-Object -Property Length -Sum).Sum
                [pscustomobject]@{ ShareName=$s.Name; Path=$s.Path; AclRoot=($aces -join ";"); FileCount=($files | Measure-Object).Count; DirCount=($items | Where-Object {$_.PSIsContainer} | Measure-Object).Count; TotalBytes=$size }
            }
        } catch { [pscustomobject]@{ ShareName=$s.Name; Path=$s.Path; AclRoot="ERROR"; FileCount=0; DirCount=0; TotalBytes=0 } }
    }
    $ntfs | Export-Csv (Join-Path $OutDir "FS-NTFS-Sample.csv") -NoTypeInformation -Encoding UTF8
}

if ([string]::IsNullOrWhiteSpace($ComputerName)) {
    Collect-Local
    Write-Host "Discovery File Shares (local) concluído em $OutDir" -ForegroundColor Green
    exit 0
}

$cred = $null
if ($PromptCredential) { $cred = Get-Credential }
elseif (-not [string]::IsNullOrWhiteSpace($Username)) {
    $sec = ConvertTo-SecureString -String $Password -AsPlainText -Force
    $cred = New-Object System.Management.Automation.PSCredential($Username, $sec)
}

Invoke-Command -ComputerName $ComputerName -Credential $cred -ScriptBlock {
    param($outDir,$sharesInclude,$skip)
    $ErrorActionPreference='Stop'
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    try { Import-Module SmbShare -ErrorAction SilentlyContinue } catch {}
    if (Get-Command Get-SmbShare -ErrorAction SilentlyContinue) {
        $shares = Get-SmbShare | Where-Object { $_.Name -notin @("ADMIN$","C$","IPC$") }
    }
    else {
        $shares = Get-WmiObject -Class Win32_Share | Where-Object { $_.Name -notin @("ADMIN$","C$","IPC$") } | ForEach-Object {
            [pscustomobject]@{ Name=$_.Name; Path=$_.Path; Description=$_.Description; FolderEnumerationMode=$null; ConcurrentUserLimit=$null }
        }
    }
    if ($sharesInclude.Count -gt 0) { $shares = $shares | Where-Object { $sharesInclude -contains $_.Name } }

    if (Get-Command Get-SmbShareAccess -ErrorAction SilentlyContinue) {
        $shareInfo = foreach($s in $shares){
            $acc = Get-SmbShareAccess -Name $s.Name | ForEach-Object { "{0}:{1}:{2}" -f $_.AccountName, $_.AccessControlType, $_.AccessRight }
            [pscustomobject]@{ Name=$s.Name; Path=$s.Path; Description=$s.Description; FolderEnumerationMode=$s.FolderEnumerationMode; ConcurrentUserLimit=$s.ConcurrentUserLimit; SharePermissions=($acc -join ";") }
        }
    } else {
        $shareInfo = foreach($s in $shares){ [pscustomobject]@{ Name=$s.Name; Path=$s.Path; Description=$s.Description; FolderEnumerationMode=$null; ConcurrentUserLimit=$null; SharePermissions=$null } }
    }
    $shareInfo | Export-Csv (Join-Path $outDir "FS-Shares.csv") -NoTypeInformation -Encoding UTF8

    $ntfs = foreach($s in $shares){
        try {
            $acl = Get-Acl -LiteralPath $s.Path
            $aces = $acl.Access | ForEach-Object { "{0}:{1}" -f $_.IdentityReference, $_.FileSystemRights }
            if ($skip) { [pscustomobject]@{ ShareName=$s.Name; Path=$s.Path; AclRoot=($aces -join ";"); FileCount=$null; DirCount=$null; TotalBytes=$null } }
            else {
                $items = Get-ChildItem -LiteralPath $s.Path -Recurse -Force -ErrorAction SilentlyContinue
                $files = $items | Where-Object { -not $_.PSIsContainer }
                $size  = ($files | Measure-Object -Property Length -Sum).Sum
                [pscustomobject]@{ ShareName=$s.Name; Path=$s.Path; AclRoot=($aces -join ";"); FileCount=($files | Measure-Object).Count; DirCount=($items | Where-Object {$_.PSIsContainer} | Measure-Object).Count; TotalBytes=$size }
            }
        } catch { [pscustomobject]@{ ShareName=$s.Name; Path=$s.Path; AclRoot="ERROR"; FileCount=0; DirCount=0; TotalBytes=0 } }
    }
    $ntfs | Export-Csv (Join-Path $outDir "FS-NTFS-Sample.csv") -NoTypeInformation -Encoding UTF8
} -ArgumentList $OutDir,$SharesInclude,$SkipMetrics

Write-Host "Discovery File Shares (remoto $ComputerName) concluído em $OutDir" -ForegroundColor Green
