param(
    [string]$ParamsPath = "..\config\parameters.psd1",
    [string]$SharesCsv = "..\outputs\FS-Shares.csv",
    [string]$GroupName = "FileMirror"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ParamsPath)) { Write-Warning "Parameters n\u00e3o encontrado: $ParamsPath"; exit 0 }
$params = Import-PowerShellDataFile -Path $ParamsPath
$src = $params.SourceFileServer
$dst = $params.DestinationFileServer
$dstRoot = $params.DestinationRootPath

if ([string]::IsNullOrWhiteSpace($src) -or [string]::IsNullOrWhiteSpace($dst)) { Write-Host "Servidores n\u00e3o definidos (src/dst). DFSR SKIP." -ForegroundColor Yellow; exit 0 }

try { Import-Module DFSR -ErrorAction Stop } catch { Import-Module DFSReplication -ErrorAction SilentlyContinue }

function Try-Run([scriptblock]$sb){ try { & $sb } catch { Write-Warning $_ } }

Try-Run { if (-not (Get-DfsReplicationGroup -GroupName $GroupName -ErrorAction SilentlyContinue)) { New-DfsReplicationGroup -GroupName $GroupName | Out-Null; Write-Host "DFSR Group criado: $GroupName" -ForegroundColor Green } }
Try-Run { Add-DfsrMember -GroupName $GroupName -ComputerName $src -ErrorAction SilentlyContinue | Out-Null }
Try-Run { Add-DfsrMember -GroupName $GroupName -ComputerName $dst -ErrorAction SilentlyContinue | Out-Null }
Try-Run { Add-DfsrConnection -GroupName $GroupName -SourceComputerName $src -DestinationComputerName $dst -ErrorAction SilentlyContinue | Out-Null }
Try-Run { Add-DfsrConnection -GroupName $GroupName -SourceComputerName $dst -DestinationComputerName $src -ErrorAction SilentlyContinue | Out-Null }

if (-not (Test-Path -LiteralPath $SharesCsv)) { Write-Host "FS-Shares.csv inexistente. DFSR memberships SKIP." -ForegroundColor Yellow; exit 0 }
$shares = Import-Csv -Path $SharesCsv
foreach($s in $shares){
    $name = $s.Name
    $srcPath = $s.Path
    if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($srcPath)) { continue }
    $folderName = $name
    Try-Run { if (-not (Get-DfsReplicatedFolder -GroupName $GroupName -FolderName $folderName -ErrorAction SilentlyContinue)) { New-DfsReplicatedFolder -GroupName $GroupName -FolderName $folderName | Out-Null; Write-Host "DFSR Folder criado: $folderName" -ForegroundColor Cyan } }
    $dstPath = Join-Path $dstRoot $name
    Try-Run { Set-DfsrMembership -GroupName $GroupName -FolderName $folderName -ComputerName $src -ContentPath $srcPath -StagingPath (Join-Path $srcPath ".dfsr-staging") -PrimaryMember $true -ErrorAction Stop | Out-Null }
    Try-Run { Set-DfsrMembership -GroupName $GroupName -FolderName $folderName -ComputerName $dst -ContentPath $dstPath -StagingPath (Join-Path $dstPath ".dfsr-staging") -PrimaryMember $false -ErrorAction Stop | Out-Null }
}

Write-Host "DFSR configurado." -ForegroundColor Green
exit 0

