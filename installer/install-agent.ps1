# Script de instalação do Agente de Migração AD
# Execute como Administrador

param(
    [string]$Token = "default-token-change-me",
    [int]$Port = 8765,
    [string]$InstallPath = "C:\Program Files\MigracaoAD\Agent"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Instalador do Agente de Migração AD  " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar se está rodando como administrador
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "❌ ERRO: Este script precisa ser executado como Administrador!" -ForegroundColor Red
    Write-Host "   Clique com botão direito e escolha 'Executar como Administrador'" -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host "✅ Executando como Administrador" -ForegroundColor Green
Write-Host ""

# Criar diretório de instalação
Write-Host "📁 Criando diretório de instalação..." -ForegroundColor Yellow
if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    Write-Host "   ✅ Diretório criado: $InstallPath" -ForegroundColor Green
} else {
    Write-Host "   ℹ️  Diretório já existe: $InstallPath" -ForegroundColor Cyan
}

# Copiar executável
Write-Host ""
Write-Host "📦 Copiando arquivos do agente..." -ForegroundColor Yellow
$exePath = Join-Path $PSScriptRoot "MigracaoAD.Agent.exe"
if (Test-Path $exePath) {
    Copy-Item $exePath -Destination $InstallPath -Force
    Write-Host "   ✅ Executável copiado" -ForegroundColor Green
} else {
    Write-Host "   ❌ ERRO: Executável não encontrado em: $exePath" -ForegroundColor Red
    Write-Host "   Compile o projeto primeiro com: dotnet publish -c Release" -ForegroundColor Yellow
    pause
    exit 1
}

# Configurar variável de ambiente para o token
Write-Host ""
Write-Host "🔐 Configurando token de autenticação..." -ForegroundColor Yellow
[Environment]::SetEnvironmentVariable("AGENT_TOKEN", $Token, "Machine")
Write-Host "   ✅ Token configurado (variável de ambiente AGENT_TOKEN)" -ForegroundColor Green
Write-Host "   ⚠️  IMPORTANTE: Anote este token para usar no app manager!" -ForegroundColor Yellow
Write-Host "   Token: $Token" -ForegroundColor White

# Parar serviço se já existir
$serviceName = "MigracaoADAgent"
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host ""
    Write-Host "⏸️  Parando serviço existente..." -ForegroundColor Yellow
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "   ✅ Serviço parado" -ForegroundColor Green
}

# Criar/atualizar serviço Windows
Write-Host ""
Write-Host "⚙️  Configurando Windows Service..." -ForegroundColor Yellow
$exeFullPath = Join-Path $InstallPath "MigracaoAD.Agent.exe"

if ($existingService) {
    # Atualizar serviço existente
    sc.exe config $serviceName binPath= $exeFullPath start= auto | Out-Null
    Write-Host "   ✅ Serviço atualizado" -ForegroundColor Green
} else {
    # Criar novo serviço
    New-Service -Name $serviceName `
                -BinaryPathName $exeFullPath `
                -DisplayName "Agente de Migração AD" `
                -Description "Agente para gerenciamento remoto de migração Active Directory" `
                -StartupType Automatic | Out-Null
    Write-Host "   ✅ Serviço criado" -ForegroundColor Green
}

# Configurar firewall
Write-Host ""
Write-Host "🔥 Configurando regra de firewall..." -ForegroundColor Yellow
$firewallRule = Get-NetFirewallRule -DisplayName "MigracaoAD Agent" -ErrorAction SilentlyContinue
if ($firewallRule) {
    Remove-NetFirewallRule -DisplayName "MigracaoAD Agent" -ErrorAction SilentlyContinue
}
New-NetFirewallRule -DisplayName "MigracaoAD Agent" `
                    -Direction Inbound `
                    -Protocol TCP `
                    -LocalPort $Port `
                    -Action Allow `
                    -Profile Any | Out-Null
Write-Host "   ✅ Porta $Port liberada no firewall" -ForegroundColor Green

# Iniciar serviço
Write-Host ""
Write-Host "▶️  Iniciando serviço..." -ForegroundColor Yellow
Start-Service -Name $serviceName
Start-Sleep -Seconds 3

# Verificar status
$service = Get-Service -Name $serviceName
if ($service.Status -eq "Running") {
    Write-Host "   ✅ Serviço iniciado com sucesso!" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Serviço não está rodando. Status: $($service.Status)" -ForegroundColor Yellow
}

# Testar conectividade
Write-Host ""
Write-Host "🔍 Testando conectividade..." -ForegroundColor Yellow
Start-Sleep -Seconds 2
try {
    $response = Invoke-WebRequest -Uri "http://localhost:$Port/health" -UseBasicParsing -TimeoutSec 5
    if ($response.StatusCode -eq 200) {
        Write-Host "   ✅ Agente respondendo corretamente!" -ForegroundColor Green
        $health = $response.Content | ConvertFrom-Json
        Write-Host "   Hostname: $($health.hostname)" -ForegroundColor Cyan
        Write-Host "   Versão: $($health.version)" -ForegroundColor Cyan
        Write-Host "   OS: $($health.os)" -ForegroundColor Cyan
    }
} catch {
    Write-Host "   ⚠️  Não foi possível conectar ao agente" -ForegroundColor Yellow
    Write-Host "   Verifique os logs em: eventvwr.msc" -ForegroundColor Yellow
}

# Obter IP do servidor
Write-Host ""
Write-Host "🌐 Informações de rede:" -ForegroundColor Yellow
$ipAddress = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.InterfaceAlias -notlike "*Loopback*"} | Select-Object -First 1).IPAddress
Write-Host "   IP do servidor: $ipAddress" -ForegroundColor Cyan
Write-Host "   Porta: $Port" -ForegroundColor Cyan
Write-Host "   URL: http://${ipAddress}:${Port}" -ForegroundColor Cyan

# Resumo final
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  ✅ INSTALAÇÃO CONCLUÍDA COM SUCESSO!  " -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "📋 PRÓXIMOS PASSOS:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. No APP MANAGER, configure:" -ForegroundColor White
Write-Host "   - IP do servidor: $ipAddress" -ForegroundColor Cyan
Write-Host "   - Porta: $Port" -ForegroundColor Cyan
Write-Host "   - Token: $Token" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. Comandos úteis:" -ForegroundColor White
Write-Host "   - Ver status: Get-Service MigracaoADAgent" -ForegroundColor Gray
Write-Host "   - Parar: Stop-Service MigracaoADAgent" -ForegroundColor Gray
Write-Host "   - Iniciar: Start-Service MigracaoADAgent" -ForegroundColor Gray
Write-Host "   - Logs: Get-EventLog -LogName Application -Source MigracaoADAgent" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Para desinstalar:" -ForegroundColor White
Write-Host "   - Execute: .\uninstall-agent.ps1" -ForegroundColor Gray
Write-Host ""

pause

