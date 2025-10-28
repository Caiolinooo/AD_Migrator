# Script de desinstalação do Agente de Migração AD
# Execute como Administrador

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Desinstalador do Agente de Migração AD" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar se está rodando como administrador
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "❌ ERRO: Este script precisa ser executado como Administrador!" -ForegroundColor Red
    pause
    exit 1
}

$serviceName = "MigracaoADAgent"
$installPath = "C:\Program Files\MigracaoAD\Agent"

# Parar serviço
Write-Host "⏸️  Parando serviço..." -ForegroundColor Yellow
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    Write-Host "   ✅ Serviço parado" -ForegroundColor Green
} else {
    Write-Host "   ℹ️  Serviço não encontrado" -ForegroundColor Cyan
}

# Remover serviço
Write-Host ""
Write-Host "🗑️  Removendo serviço..." -ForegroundColor Yellow
if ($service) {
    sc.exe delete $serviceName | Out-Null
    Write-Host "   ✅ Serviço removido" -ForegroundColor Green
} else {
    Write-Host "   ℹ️  Nada para remover" -ForegroundColor Cyan
}

# Remover regra de firewall
Write-Host ""
Write-Host "🔥 Removendo regra de firewall..." -ForegroundColor Yellow
$firewallRule = Get-NetFirewallRule -DisplayName "MigracaoAD Agent" -ErrorAction SilentlyContinue
if ($firewallRule) {
    Remove-NetFirewallRule -DisplayName "MigracaoAD Agent" -ErrorAction SilentlyContinue
    Write-Host "   ✅ Regra de firewall removida" -ForegroundColor Green
} else {
    Write-Host "   ℹ️  Regra não encontrada" -ForegroundColor Cyan
}

# Remover arquivos
Write-Host ""
Write-Host "📁 Removendo arquivos..." -ForegroundColor Yellow
if (Test-Path $installPath) {
    Start-Sleep -Seconds 2
    Remove-Item -Path $installPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "   ✅ Arquivos removidos" -ForegroundColor Green
} else {
    Write-Host "   ℹ️  Diretório não encontrado" -ForegroundColor Cyan
}

# Remover variável de ambiente
Write-Host ""
Write-Host "🔐 Removendo variável de ambiente..." -ForegroundColor Yellow
[Environment]::SetEnvironmentVariable("AGENT_TOKEN", $null, "Machine")
Write-Host "   ✅ Variável removida" -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  ✅ DESINSTALAÇÃO CONCLUÍDA!           " -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

pause

