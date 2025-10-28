param(
    [string]$ParamsPath = ".\\config\\parameters.psd1",
    [switch]$AutoProceed
)

$ErrorActionPreference = 'Stop'

function Confirm-Step {
    param([string]$Message)
    if ($usingAutoProceed) { return $true }
    $resp = Read-Host "$Message (Y/N)"
    return ($resp -match '^(?i)y')
}

# Load parameters
if (-not (Test-Path -LiteralPath $ParamsPath)) { throw "Parâmetros não encontrados: $ParamsPath" }
$params = Import-PowerShellDataFile -Path $ParamsPath
$usingAutoProceed = $true
if ($PSBoundParameters.ContainsKey('AutoProceed')) { $usingAutoProceed = $AutoProceed.IsPresent }
elseif ($null -ne $params.AutoProceed) { $usingAutoProceed = [bool]$params.AutoProceed }

# Normalize relative paths from orchestrator root
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

# Paths
$scripts = Join-Path $root 'scripts'
$outputs = Resolve-Path (Join-Path $root ($params.OutputsDir))
$mapsCsv = Resolve-Path (Join-Path $root ($params.MapsCsv)) -ErrorAction SilentlyContinue
if (-not $mapsCsv) { $mapsCsv = Join-Path $root ($params.MapsCsv) }

# Runtime prompts for missing critical parameters
function Read-RequiredValue {
    param(
        [string]$Current,
        [string]$Prompt,
        [string]$Example,
        [string]$Hint
    )
    if (-not $Current -or [string]::IsNullOrWhiteSpace($Current)){
        Write-Host $Hint -ForegroundColor DarkGray
        $val = Read-Host ("$Prompt (ex.: $Example)")
        return $val
    }
    return $Current
}

$params.DomainName = Read-RequiredValue -Current $params.DomainName -Prompt 'Informe o DomainName (DNS do domínio)' -Example 'contoso.local' -Hint 'Dica: rode Get-ADDomain | Select -ExpandProperty DNSRoot no DC atual.'
$params.SiteName   = Read-RequiredValue -Current $params.SiteName   -Prompt 'Informe o SiteName (Site do AD para o novo DC)' -Example 'CloudSite' -Hint 'Dica: veja em Active Directory Sites and Services o nome do site destino.'

$params.OldDCHostname = Read-RequiredValue -Current $params.OldDCHostname -Prompt 'Hostname do DC antigo (on-prem)' -Example 'OLDDC01' -Hint 'Dica: no DC antigo, $env:COMPUTERNAME.'
$params.NewDCHostname = Read-RequiredValue -Current $params.NewDCHostname -Prompt 'Hostname do novo DC (nuvem)' -Example 'NEWDC01' -Hint 'Dica: nome NetBIOS/hostname da VM que será promovida.'

$params.SourceFileServer      = Read-RequiredValue -Current $params.SourceFileServer      -Prompt 'Servidor de arquivos origem (on-prem)' -Example 'OLDFS'  -Hint 'Dica: servidor atual que hospeda os shares.'
$params.DestinationFileServer = Read-RequiredValue -Current $params.DestinationFileServer -Prompt 'Servidor de arquivos destino (nuvem)' -Example 'NEWFS'  -Hint 'Dica: novo servidor na nuvem para hospedar os shares.'
$params.DestinationRootPath   = Read-RequiredValue -Current $params.DestinationRootPath   -Prompt 'Caminho raiz no destino para as pastas dos shares' -Example 'D:\\Shares' -Hint 'Dica: partição/volume NTFS no novo servidor de arquivos (ex.: D:\\Shares).'

# 0) Prechecks
Write-Host '== Fase 0: Prechecks ==' -ForegroundColor Magenta
& (Join-Path $scripts '00-Prechecks.ps1') -ParamsPath $ParamsPath -OutDir $outputs
$pre = Get-Content (Join-Path $outputs 'Prechecks.json') -Raw | ConvertFrom-Json

# Abort if SYSVOL uses FRS (precisa migrar para DFSR)
if ($pre.SysvolReplication -ne 'DFSR') {
    throw "Seu domínio não está com SYSVOL em DFSR (valor atual: $($pre.SysvolReplication)). Migre FRS->DFSR antes de adicionar DCs modernos."
}

# 1) Discovery (AD + Shares)
Write-Host '== Fase 1: Discovery ==' -ForegroundColor Magenta
& (Join-Path $scripts '01-Discovery-AD.ps1') -OutDir $outputs -DnsServer $params.DnsServer
& (Join-Path $scripts '02-Discovery-FileShares.ps1') -OutDir $outputs

# 2) Promover novo DC
if (Confirm-Step "Deseja promover o novo DC ($($params.NewDCHostname)) no domínio $($params.DomainName)?") {
    Write-Host '== Fase 2: Promote New DC ==' -ForegroundColor Magenta
    if ($params.UseRemoting) {
        Invoke-Command -ComputerName $params.NewDCHostname -ScriptBlock {
            param($dn,$site,$db,$log,$sys,$installDns)
            & "$using:scripts/10-Promote-New-DC.ps1" -DomainName $dn -SiteName $site -DatabasePath $db -LogPath $log -SYSVOLPath $sys -InstallDNS:([bool]$installDns)
        } -ArgumentList $params.DomainName,$params.SiteName,$params.DatabasePath,$params.LogPath,$params.SYSVOLPath,$true
    } else {
        & (Join-Path $scripts '10-Promote-New-DC.ps1') -DomainName $params.DomainName -SiteName $params.SiteName -DatabasePath $params.DatabasePath -LogPath $params.LogPath -SYSVOLPath $params.SYSVOLPath -InstallDNS
    }
    Write-Host 'A promoção pode reiniciar o servidor. Aguarde a replicação estabilizar antes da próxima fase.' -ForegroundColor Yellow
} else { Write-Host 'Promoção de DC pulada pelo usuário.' }

# 3) Transferir FSMO
if (Confirm-Step "Deseja transferir as FSMO para $($params.NewDCHostname)?") {
    Write-Host '== Fase 3: Transfer FSMO ==' -ForegroundColor Magenta
    & (Join-Path $scripts '20-Transfer-FSMO.ps1') -NewDC $params.NewDCHostname
} else { Write-Host 'Transferência de FSMO pulada pelo usuário.' }

# 4) Preparar destino de arquivos (criar shares)
if ($params.CreateDestinationShares) {
    if (Confirm-Step "Criar/ajustar shares no servidor de destino $($params.DestinationFileServer)?") {
        Write-Host '== Fase 4: Create Destination Shares ==' -ForegroundColor Magenta
        & (Join-Path $scripts '25-Create-Destination-Shares.ps1') -ParamsPath $ParamsPath -SharesCsv (Join-Path $outputs 'FS-Shares.csv')
    }
}

# 5) Gerar CSV de mapeamentos (se ainda não existir)
if (-not (Test-Path -LiteralPath $mapsCsv)) {
    if (Confirm-Step "Gerar o arquivo de mapeamento de cópias (maps.csv) automaticamente?") {
        Write-Host '== Fase 5: Generate Maps ==' -ForegroundColor Magenta
        & (Join-Path $scripts '24-Generate-Maps.ps1') -ParamsPath $ParamsPath -SharesCsv (Join-Path $outputs 'FS-Shares.csv') -OutCsv (Join-Path $root ($params.MapsCsv))
        $mapsCsv = Resolve-Path (Join-Path $root ($params.MapsCsv))
    } else {
        Write-Host "Informe o caminho de CSV a usar (-CsvMapPath no Robocopy) antes da próxima fase." -ForegroundColor Yellow
    }
}

# 6) Migração de dados (Robocopy)
if (Test-Path -LiteralPath $mapsCsv) {
    if (Confirm-Step "Executar a cópia de dados (pré-cópia e corte com Robocopy)?") {
        Write-Host '== Fase 6: Robocopy Migration ==' -ForegroundColor Magenta
        & (Join-Path $scripts '30-Robocopy-Migrate.ps1') -CsvMapPath $mapsCsv -Retry $params.RobocopyRetry -Wait $params.RobocopyWait -Threads $params.FileCopyThreads
    }
} else {
    Write-Host "maps.csv não encontrado. Pulei a fase de migração de dados." -ForegroundColor Yellow
}

# 7) Despromover DC antigo
if (Confirm-Step "Deseja despromover o DC antigo ($($params.OldDCHostname))?") {
    Write-Host '== Fase 7: Demote Old DC ==' -ForegroundColor Magenta
    & (Join-Path $scripts '40-Demote-Old-DC.ps1')
}

Write-Host 'Orquestração concluída.' -ForegroundColor Green
