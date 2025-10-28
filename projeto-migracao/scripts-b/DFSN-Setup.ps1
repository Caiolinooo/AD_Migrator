param(
    [string]$ParamsPath = "..\config\parameters.psd1",
    [Parameter(Mandatory=$true)][string]$NamespacePath,   # ex: \\contoso.local\Arquivos
    [string[]]$RootTargets = @()                          # ex: 'OLDFS','NEWFS'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ParamsPath)) {
    Write-Warning "Parameters n\u00e3o encontrado: $ParamsPath. DFSN SKIP."
    exit 0
}
$params = Import-PowerShellDataFile -Path $ParamsPath
$src = $params.SourceFileServer
$dst = $params.DestinationFileServer

try { Import-Module DFSN -ErrorAction Stop } catch { Import-Module DFSN -ErrorAction SilentlyContinue }

function Ensure-DfsnRoot {
    param([string]$path,[string]$target)
    $nsName = (Split-Path -Leaf $path)
    $tPath = "\\\\$target\\$nsName"
    try {
        if (-not (Get-DfsnRoot -Path $path -ErrorAction SilentlyContinue)) {
            New-DfsnRoot -Path $path -TargetPath $tPath -Type DomainV2 -Description "Arquivos" -ErrorAction Stop | Out-Null
            Write-Host "DFSN Root criado: $path (target $tPath)" -ForegroundColor Green
        }
    } catch { Write-Warning "Falha ao criar DFSN Root: $_" }
}

function Ensure-DfsnRootTarget { param($path,$target)
    $nsName = (Split-Path -Leaf $path)
    $tPath = "\\\\$target\\$nsName"
    try { New-DfsnRootTarget -Path $path -TargetPath $tPath -ErrorAction Stop | Out-Null } catch { Write-Host "Target j\u00e1 existe: $tPath" -ForegroundColor DarkGray }
}

# Cria o root e targets
if ($RootTargets.Count -gt 0) {
    Ensure-DfsnRoot -path $NamespacePath -target $RootTargets[0]
    foreach($t in $RootTargets){ Ensure-DfsnRootTarget -path $NamespacePath -target $t }
} else {
    # fallback: usar destino como primeiro target
    if ($dst) { Ensure-DfsnRoot -path $NamespacePath -target $dst }
}

# Criar pastas do namespace por share
$sharesCsv = "..\outputs\FS-Shares.csv"
if (-not (Test-Path -LiteralPath $sharesCsv)) {
    Write-Host "FS-Shares.csv inexistente. DFSN folders SKIP." -ForegroundColor Yellow
    exit 0
}
$shares = Import-Csv -Path $sharesCsv
foreach($s in $shares){
    $name = $s.Name
    if ([string]::IsNullOrWhiteSpace($name)) { continue }
    $folderPath = Join-Path $NamespacePath $name
    try {
        if (-not (Get-DfsnFolder -Path $folderPath -ErrorAction SilentlyContinue)){
            New-DfsnFolder -Path $folderPath -TargetPath ("\\\\{0}\\{1}" -f $src,$name) -ErrorAction Stop | Out-Null
            Write-Host "DFSN folder criado: $folderPath" -ForegroundColor Cyan
        }
        # garantir targets
        foreach($t in @($src,$dst)){
            if ([string]::IsNullOrWhiteSpace($t)) { continue }
            $tPath = "\\\\$t\\$name"
            try { New-DfsnFolderTarget -Path $folderPath -TargetPath $tPath -ErrorAction Stop | Out-Null } catch { }
        }
    } catch { Write-Warning $_ }
}

Write-Host "DFSN configurado." -ForegroundColor Green
exit 0

