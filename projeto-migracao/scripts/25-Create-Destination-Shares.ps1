param(
    [string]$ParamsPath = "..\config\parameters.psd1",
    [string]$SharesCsv  = "..\outputs\FS-Shares.csv"
)

$ErrorActionPreference = 'Stop'

function Convert-SharePermissionsString {
    param([string]$PermString)
    # Format expected per entry: AccountName:AccessControlType:AccessRight;...
    $result = @()
    if ([string]::IsNullOrWhiteSpace($PermString)) { return $result }
    foreach($entry in ($PermString -split ';')){
        if ([string]::IsNullOrWhiteSpace($entry)) { continue }
        $parts = $entry -split ':'
        if ($parts.Count -ge 3){
            $result += [pscustomobject]@{
                AccountName       = $parts[0]
                AccessControlType = $parts[1]
                AccessRight       = $parts[2]
            }
        }
    }
    return $result
}

$params = Import-PowerShellDataFile -Path $ParamsPath
$destServer = $params.DestinationFileServer
$destRoot   = $params.DestinationRootPath
$useRem     = [bool]$params.UseRemoting

if (-not (Test-Path -LiteralPath $SharesCsv)) {
    throw "Arquivo não encontrado: $SharesCsv. Execute 02-Discovery-FileShares.ps1 antes."
}
$shares = Import-Csv -Path $SharesCsv

# Ensure destination root exists
if ($useRem) {
    Invoke-Command -ComputerName $destServer -ScriptBlock {
        param($root)
        if (-not (Test-Path -LiteralPath $root)) { New-Item -ItemType Directory -Path $root -Force | Out-Null }
    } -ArgumentList $destRoot
} else {
    if (-not (Test-Path -LiteralPath $destRoot)) { New-Item -ItemType Directory -Path $destRoot -Force | Out-Null }
}

foreach($s in $shares){
    $name = $s.Name
    if ([string]::IsNullOrWhiteSpace($name)) { continue }

    $folder = Join-Path $destRoot $name
    $desc   = $s.Description
    $perms  = Convert-SharePermissionsString -PermString $s.SharePermissions

    $script = {
        param($folder,$name,$desc,$perms)
        if (-not (Test-Path -LiteralPath $folder)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }

        # Create or ensure SMB share exists
        $existing = Get-SmbShare -Name $name -ErrorAction SilentlyContinue
        if ($null -eq $existing){
            New-SmbShare -Name $name -Path $folder -Description $desc -CachingMode None -FullAccess @() | Out-Null
        }
        
        # Reset existing permissions (optional: keep existing). Here we clear non builtin by re-creating perms
        $currentAcc = Get-SmbShareAccess -Name $name -ErrorAction SilentlyContinue
        foreach($a in $currentAcc){
            try { Revoke-SmbShareAccess -Name $name -AccountName $a.AccountName -Force -CimSession $null -ErrorAction SilentlyContinue } catch {}
        }

        foreach($p in $perms){
            # Map AccessRight to SMB Share rights (Full, Change, Read). If unknown, default to Change when Allow
            $right = switch -Regex ($p.AccessRight) {
                'Full'   { 'Full' ; break }
                'Change' { 'Change' ; break }
                'Read'   { 'Read' ; break }
                default  { 'Change' }
            }
            if ($p.AccessControlType -ieq 'Allow'){
                try { Grant-SmbShareAccess -Name $name -AccountName $p.AccountName -AccessRight $right -Force | Out-Null } catch { Write-Warning $_ }
            } else {
                try { Block-SmbShareAccess -Name $name -AccountName $p.AccountName -Force | Out-Null } catch { Write-Warning $_ }
            }
        }
    }

    if ($useRem) {
        Invoke-Command -ComputerName $destServer -ScriptBlock $script -ArgumentList $folder,$name,$desc,$perms
    } else {
        & $script.Invoke($folder,$name,$desc,$perms)
    }

    Write-Host ("Share criado/atualizado em {0}: {1} ({2})" -f $destServer, $name, $folder) -ForegroundColor Cyan
}

Write-Host "Criação de shares no destino concluída." -ForegroundColor Green
