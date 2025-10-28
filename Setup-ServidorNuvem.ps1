<#
.SYNOPSIS
    Script de configuração automática do servidor na nuvem (Proxmox/EVEO)
    
.DESCRIPTION
    Prepara o Windows Server 2019 na nuvem para receber a migração AD Option A
    - Configura rede e DNS
    - Abre portas no firewall
    - Instala roles necessários (AD DS, DFS, File Server)
    - Prepara disco para shares
    - Testa conectividade com servidor local
    
.NOTES
    Autor: Caio Valerio Goulart Correia
    Versão: 1.0
    Execute como Administrador no servidor da NUVEM
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$IPServidorLocal,
    
    [Parameter(Mandatory=$true)]
    [string]$IPServidorNuvem,
    
    [Parameter(Mandatory=$false)]
    [string]$Gateway = "",
    
    [Parameter(Mandatory=$false)]
    [int]$PrefixLength = 24,
    
    [Parameter(Mandatory=$false)]
    [string]$InterfaceAlias = "Ethernet",
    
    [Parameter(Mandatory=$false)]
    [int]$DiscoShares = -1,
    
    [Parameter(Mandatory=$false)]
    [string]$LetraShares = "E"
)

$ErrorActionPreference = "Continue"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Setup Servidor Nuvem - Option A" -ForegroundColor Cyan
Write-Host "  Migração AD - Mesmo Domínio" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar se está rodando como Administrador
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "❌ ERRO: Execute este script como Administrador!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Executando como Administrador" -ForegroundColor Green
Write-Host ""

# ============================================
# ETAPA 1: Configurar Rede
# ============================================
Write-Host "📡 ETAPA 1: Configurando Rede..." -ForegroundColor Yellow

try {
    # Verificar se o adaptador existe
    $adapter = Get-NetAdapter -Name $InterfaceAlias -ErrorAction Stop
    Write-Host "  ✅ Adaptador '$InterfaceAlias' encontrado" -ForegroundColor Green
    
    # Remover IP existente (se houver)
    $existingIP = Get-NetIPAddress -InterfaceAlias $InterfaceAlias -AddressFamily IPv4 -ErrorAction SilentlyContinue
    if ($existingIP) {
        Write-Host "  ⚠️  Removendo IP existente: $($existingIP.IPAddress)" -ForegroundColor Yellow
        Remove-NetIPAddress -InterfaceAlias $InterfaceAlias -AddressFamily IPv4 -Confirm:$false -ErrorAction SilentlyContinue
        Remove-NetRoute -InterfaceAlias $InterfaceAlias -Confirm:$false -ErrorAction SilentlyContinue
    }
    
    # Configurar novo IP
    Write-Host "  🔧 Configurando IP: $IPServidorNuvem/$PrefixLength" -ForegroundColor Cyan
    if ($Gateway) {
        New-NetIPAddress -InterfaceAlias $InterfaceAlias -IPAddress $IPServidorNuvem -PrefixLength $PrefixLength -DefaultGateway $Gateway -ErrorAction Stop | Out-Null
    } else {
        New-NetIPAddress -InterfaceAlias $InterfaceAlias -IPAddress $IPServidorNuvem -PrefixLength $PrefixLength -ErrorAction Stop | Out-Null
    }
    
    # Configurar DNS (servidor local como primário)
    Write-Host "  🔧 Configurando DNS: $IPServidorLocal (primário)" -ForegroundColor Cyan
    Set-DnsClientServerAddress -InterfaceAlias $InterfaceAlias -ServerAddresses $IPServidorLocal,"8.8.8.8" -ErrorAction Stop
    
    Write-Host "  ✅ Rede configurada com sucesso!" -ForegroundColor Green
    
    # Exibir configuração
    $ip = Get-NetIPAddress -InterfaceAlias $InterfaceAlias -AddressFamily IPv4
    $dns = Get-DnsClientServerAddress -InterfaceAlias $InterfaceAlias -AddressFamily IPv4
    Write-Host "  📊 IP: $($ip.IPAddress)/$($ip.PrefixLength)" -ForegroundColor Gray
    Write-Host "  📊 DNS: $($dns.ServerAddresses -join ', ')" -ForegroundColor Gray
    
} catch {
    Write-Host "  ❌ Erro ao configurar rede: $_" -ForegroundColor Red
    Write-Host "  💡 Configure manualmente via GUI ou ajuste os parâmetros" -ForegroundColor Yellow
}

Write-Host ""

# ============================================
# ETAPA 2: Testar Conectividade
# ============================================
Write-Host "🔍 ETAPA 2: Testando Conectividade com Servidor Local..." -ForegroundColor Yellow

$portas = @(
    @{Porta=53; Nome="DNS"},
    @{Porta=88; Nome="Kerberos"},
    @{Porta=135; Nome="RPC"},
    @{Porta=389; Nome="LDAP"},
    @{Porta=445; Nome="SMB"},
    @{Porta=464; Nome="Kerberos Password"},
    @{Porta=636; Nome="LDAPS"},
    @{Porta=3268; Nome="Global Catalog"},
    @{Porta=5985; Nome="WinRM"}
)

$falhas = 0
foreach ($p in $portas) {
    Write-Host "  🔌 Testando porta $($p.Porta) ($($p.Nome))..." -NoNewline
    $result = Test-NetConnection -ComputerName $IPServidorLocal -Port $p.Porta -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
    if ($result.TcpTestSucceeded) {
        Write-Host " ✅" -ForegroundColor Green
    } else {
        Write-Host " ❌" -ForegroundColor Red
        $falhas++
    }
}

if ($falhas -eq 0) {
    Write-Host "  ✅ Todas as portas estão acessíveis!" -ForegroundColor Green
} else {
    Write-Host "  ⚠️  $falhas porta(s) não acessível(is)" -ForegroundColor Yellow
    Write-Host "  💡 Verifique firewall do servidor local e do Proxmox/EVEO" -ForegroundColor Yellow
}

Write-Host ""

# ============================================
# ETAPA 3: Habilitar WinRM
# ============================================
Write-Host "🔧 ETAPA 3: Habilitando WinRM..." -ForegroundColor Yellow

try {
    Enable-PSRemoting -Force -ErrorAction Stop | Out-Null
    Set-Item WSMan:\localhost\Client\TrustedHosts -Value "*" -Force -ErrorAction Stop
    Write-Host "  ✅ WinRM habilitado" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Erro ao habilitar WinRM: $_" -ForegroundColor Red
}

Write-Host ""

# ============================================
# ETAPA 4: Abrir Portas no Firewall
# ============================================
Write-Host "🔥 ETAPA 4: Abrindo Portas no Firewall..." -ForegroundColor Yellow

$regras = @(
    @{Nome="AD - DNS (TCP)"; Protocolo="TCP"; Porta=53},
    @{Nome="AD - DNS (UDP)"; Protocolo="UDP"; Porta=53},
    @{Nome="AD - Kerberos (TCP)"; Protocolo="TCP"; Porta=88},
    @{Nome="AD - Kerberos (UDP)"; Protocolo="UDP"; Porta=88},
    @{Nome="AD - RPC (TCP)"; Protocolo="TCP"; Porta=135},
    @{Nome="AD - LDAP (TCP)"; Protocolo="TCP"; Porta=389},
    @{Nome="AD - LDAP (UDP)"; Protocolo="UDP"; Porta=389},
    @{Nome="AD - SMB (TCP)"; Protocolo="TCP"; Porta=445},
    @{Nome="AD - Kerberos Pwd (TCP)"; Protocolo="TCP"; Porta=464},
    @{Nome="AD - Kerberos Pwd (UDP)"; Protocolo="UDP"; Porta=464},
    @{Nome="AD - LDAPS (TCP)"; Protocolo="TCP"; Porta=636},
    @{Nome="AD - Global Catalog (TCP)"; Protocolo="TCP"; Porta=3268},
    @{Nome="AD - Global Catalog SSL (TCP)"; Protocolo="TCP"; Porta=3269},
    @{Nome="AD - WinRM HTTP (TCP)"; Protocolo="TCP"; Porta=5985},
    @{Nome="AD - WinRM HTTPS (TCP)"; Protocolo="TCP"; Porta=5986}
)

foreach ($regra in $regras) {
    # Remover regra existente (se houver)
    $existing = Get-NetFirewallRule -DisplayName $regra.Nome -ErrorAction SilentlyContinue
    if ($existing) {
        Remove-NetFirewallRule -DisplayName $regra.Nome -ErrorAction SilentlyContinue | Out-Null
    }
    
    # Criar nova regra
    New-NetFirewallRule -DisplayName $regra.Nome -Direction Inbound -Protocol $regra.Protocolo -LocalPort $regra.Porta -Action Allow -ErrorAction SilentlyContinue | Out-Null
    Write-Host "  ✅ $($regra.Nome)" -ForegroundColor Green
}

# RPC Dynamic Ports
$existing = Get-NetFirewallRule -DisplayName "AD - RPC Dynamic" -ErrorAction SilentlyContinue
if ($existing) {
    Remove-NetFirewallRule -DisplayName "AD - RPC Dynamic" -ErrorAction SilentlyContinue | Out-Null
}
New-NetFirewallRule -DisplayName "AD - RPC Dynamic" -Direction Inbound -Protocol TCP -LocalPort 49152-65535 -Action Allow -ErrorAction SilentlyContinue | Out-Null
Write-Host "  ✅ AD - RPC Dynamic (49152-65535)" -ForegroundColor Green

Write-Host ""

# ============================================
# ETAPA 5: Instalar Roles e Features
# ============================================
Write-Host "📦 ETAPA 5: Instalando Roles e Features..." -ForegroundColor Yellow

$features = @(
    @{Nome="AD-Domain-Services"; Descricao="Active Directory Domain Services"},
    @{Nome="FS-DFS-Namespace"; Descricao="DFS Namespace"},
    @{Nome="FS-DFS-Replication"; Descricao="DFS Replication"},
    @{Nome="FS-FileServer"; Descricao="File Server"},
    @{Nome="RSAT-AD-Tools"; Descricao="AD Management Tools"},
    @{Nome="RSAT-DFS-Mgmt-Con"; Descricao="DFS Management Tools"}
)

foreach ($feature in $features) {
    Write-Host "  📦 Instalando $($feature.Descricao)..." -NoNewline
    $result = Install-WindowsFeature -Name $feature.Nome -IncludeManagementTools -ErrorAction SilentlyContinue
    if ($result.Success) {
        Write-Host " ✅" -ForegroundColor Green
    } else {
        Write-Host " ⚠️  (já instalado ou erro)" -ForegroundColor Yellow
    }
}

Write-Host ""

# ============================================
# ETAPA 6: Preparar Disco para Shares
# ============================================
if ($DiscoShares -gt 0) {
    Write-Host "💾 ETAPA 6: Preparando Disco para Shares..." -ForegroundColor Yellow
    
    try {
        # Verificar se o disco existe
        $disco = Get-Disk -Number $DiscoShares -ErrorAction Stop
        
        if ($disco.PartitionStyle -eq "RAW") {
            Write-Host "  🔧 Inicializando disco $DiscoShares..." -ForegroundColor Cyan
            Initialize-Disk -Number $DiscoShares -PartitionStyle GPT -ErrorAction Stop | Out-Null
            
            Write-Host "  🔧 Criando partição $LetraShares`:..." -ForegroundColor Cyan
            New-Partition -DiskNumber $DiscoShares -UseMaximumSize -DriveLetter $LetraShares -ErrorAction Stop | Out-Null
            
            Write-Host "  🔧 Formatando volume..." -ForegroundColor Cyan
            Format-Volume -DriveLetter $LetraShares -FileSystem NTFS -NewFileSystemLabel "Shares" -Confirm:$false -ErrorAction Stop | Out-Null
            
            Write-Host "  ✅ Disco preparado: $LetraShares`:\" -ForegroundColor Green
        } else {
            Write-Host "  ⚠️  Disco já está inicializado" -ForegroundColor Yellow
        }
        
        # Exibir informações
        $volume = Get-Volume -DriveLetter $LetraShares -ErrorAction SilentlyContinue
        if ($volume) {
            $tamanhoGB = [math]::Round($volume.Size / 1GB, 2)
            $livreGB = [math]::Round($volume.SizeRemaining / 1GB, 2)
            Write-Host "  📊 Volume: $LetraShares`:\ | Tamanho: $tamanhoGB GB | Livre: $livreGB GB" -ForegroundColor Gray
        }
        
    } catch {
        Write-Host "  ❌ Erro ao preparar disco: $_" -ForegroundColor Red
        Write-Host "  💡 Configure manualmente via Disk Management" -ForegroundColor Yellow
    }
    
    Write-Host ""
} else {
    Write-Host "💾 ETAPA 6: Preparar Disco para Shares - PULADO (use -DiscoShares N)" -ForegroundColor Gray
    Write-Host ""
}

# ============================================
# RESUMO FINAL
# ============================================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ✅ CONFIGURAÇÃO CONCLUÍDA!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📋 Próximos Passos:" -ForegroundColor Yellow
Write-Host "  1. Execute o MigracaoAD.UI.exe no servidor local" -ForegroundColor White
Write-Host "  2. Preencha os dados:" -ForegroundColor White
Write-Host "     - IP Origem: $IPServidorLocal" -ForegroundColor Gray
Write-Host "     - IP Destino: $IPServidorNuvem" -ForegroundColor Gray
Write-Host "  3. Escolha Option A (Promote/Transfer/Demote)" -ForegroundColor White
Write-Host "  4. Execute em DRY-RUN primeiro!" -ForegroundColor White
Write-Host ""
Write-Host "⚠️  IMPORTANTE:" -ForegroundColor Yellow
Write-Host "  - Faça BACKUP do servidor local antes de executar" -ForegroundColor White
Write-Host "  - Faça SNAPSHOT do servidor nuvem no Proxmox" -ForegroundColor White
Write-Host "  - Execute em horário de manutenção" -ForegroundColor White
Write-Host ""
Write-Host "Desenvolvido com ❤️ e muito 💪 por Caio Valerio Goulart Correia" -ForegroundColor Cyan
Write-Host ""

