# 🔧 Troubleshooting - AD Migration Suite

## 🚨 Problemas Comuns e Soluções

### 1. **"Test-NetConnection não é reconhecido"**

**Problema:** O comando `Test-NetConnection` não existe em Windows Server 2012 R2.

**Solução:** ✅ **JÁ CORRIGIDO NA v1.0.1!**

O agente agora usa `Test-Connection` e `System.Net.Sockets.TcpClient` que são compatíveis com todas as versões do Windows Server.

**Ação:** Atualize para a versão mais recente do agente.

---

### 2. **"Agentes não se conectam"**

**Sintomas:**
- Teste de conexão falha
- Timeout ao tentar conectar
- "Não foi possível conectar ao agente"

**Soluções:**

#### A) Verificar se o serviço está rodando

```powershell
Get-Service MigracaoADAgent
```

Se estiver parado, inicie:

```powershell
Start-Service MigracaoADAgent
```

#### B) Verificar firewall

```powershell
# Ver regra do firewall
Get-NetFirewallRule -DisplayName "MigracaoAD Agent"

# Se não existir, criar manualmente:
New-NetFirewallRule -DisplayName "MigracaoAD Agent" `
    -Direction Inbound `
    -Protocol TCP `
    -LocalPort 8765 `
    -Action Allow
```

#### C) Testar conectividade local

```powershell
# No próprio servidor onde o agente está instalado:
Invoke-WebRequest -Uri "http://localhost:8765/health" -UseBasicParsing
```

Deve retornar:
```json
{
  "status": "healthy",
  "version": "1.0.0",
  "hostname": "SERVER-NAME",
  "os": "Microsoft Windows NT 10.0.17763.0",
  "timestamp": "2025-10-28T..."
}
```

#### D) Testar conectividade remota

```powershell
# De outro servidor/computador:
Invoke-WebRequest -Uri "http://192.168.1.10:8765/health" `
    -Headers @{"X-Agent-Token"="seu-token"} `
    -UseBasicParsing
```

#### E) Verificar token

O token deve ser **exatamente o mesmo** em:
1. Instalação do agente (parâmetro `-Token`)
2. Configuração no app Manager

Para verificar o token configurado:

```powershell
# Ver variável de ambiente
[Environment]::GetEnvironmentVariable("AGENT_TOKEN", "Machine")
```

Para alterar o token:

```powershell
# Parar serviço
Stop-Service MigracaoADAgent

# Alterar token
[Environment]::SetEnvironmentVariable("AGENT_TOKEN", "novo-token", "Machine")

# Iniciar serviço
Start-Service MigracaoADAgent
```

---

### 3. **"Nada acontece quando tento executar"**

**Problema:** O botão "Executar" não faz nada ou mostra erro de script não encontrado.

**Causa:** O app está tentando executar scripts de migração que não estão incluídos no instalador.

**Solução:** Use o sistema de **agentes** em vez dos scripts:

1. Instale o agente nos servidores
2. Vá para **"Teste de Agentes"**
3. Use os comandos via agente

**Nota:** A funcionalidade de execução automática de scripts será removida em versões futuras. O sistema de agentes é a forma recomendada.

---

### 4. **"Token inválido"**

**Sintomas:**
- Erro 401 Unauthorized
- "Token inválido ou ausente"

**Solução:**

```powershell
# 1. Desinstalar agente
.\uninstall-agent.ps1

# 2. Reinstalar com token correto
.\install-agent.ps1 -Token "token-correto-123"

# 3. Verificar
Invoke-WebRequest -Uri "http://localhost:8765/health" `
    -Headers @{"X-Agent-Token"="token-correto-123"} `
    -UseBasicParsing
```

---

### 5. **"Porta 8765 já está em uso"**

**Sintomas:**
- Erro ao iniciar o serviço
- "Address already in use"

**Solução:**

#### A) Verificar o que está usando a porta

```powershell
Get-NetTCPConnection -LocalPort 8765 | Select-Object OwningProcess
Get-Process -Id <PID>
```

#### B) Usar outra porta

```powershell
# Desinstalar
.\uninstall-agent.ps1

# Reinstalar em outra porta
.\install-agent.ps1 -Token "meu-token" -Port 8766
```

**Importante:** Configure a mesma porta no app Manager!

---

### 6. **"Erro ao executar comandos PowerShell"**

**Sintomas:**
- Comandos falham com erro genérico
- "Access denied"
- "Execution policy"

**Soluções:**

#### A) Verificar Execution Policy

```powershell
# Ver política atual
Get-ExecutionPolicy

# Alterar se necessário (como Admin)
Set-ExecutionPolicy RemoteSigned -Force
```

#### B) Executar como Administrador

O agente precisa rodar como **SYSTEM** (já configurado automaticamente).

Para verificar:

```powershell
Get-Service MigracaoADAgent | Select-Object Name, Status, StartType
```

Deve mostrar:
- Status: Running
- StartType: Automatic

---

### 7. **"Logs do agente"**

Para ver logs do serviço:

```powershell
# Event Viewer
eventvwr.msc

# Ou via PowerShell:
Get-EventLog -LogName Application -Source MigracaoADAgent -Newest 20
```

---

### 8. **"Desempenho lento"**

**Sintomas:**
- Comandos demoram muito
- Timeout frequente

**Soluções:**

#### A) Aumentar timeout no app

No código do `AgentClient.cs`, o timeout padrão é 30 segundos. Para comandos longos, aumente:

```csharp
_httpClient.Timeout = TimeSpan.FromMinutes(5);
```

#### B) Verificar recursos do servidor

```powershell
# CPU e memória
Get-Process -Name MigracaoAD.Agent | Select-Object CPU, WorkingSet

# Disco
Get-PSDrive C | Select-Object Used, Free
```

---

### 9. **"Reinstalar agente"**

```powershell
# 1. Desinstalar
.\uninstall-agent.ps1

# 2. Limpar (opcional)
Remove-Item -Path "C:\Program Files\MigracaoAD\Agent" -Recurse -Force -ErrorAction SilentlyContinue

# 3. Reinstalar
.\install-agent.ps1 -Token "seu-token"
```

---

### 10. **"Testar conectividade entre servidores"**

Use o app Manager:

1. Vá para **"Teste de Agentes"**
2. Clique em **"Testar Conectividade Entre Servidores"**
3. Verifique se ambos conseguem se comunicar

Ou via PowerShell:

```powershell
# Do servidor A para B
Test-Connection -ComputerName 192.168.1.20 -Count 2

# Testar porta específica
$tcpClient = New-Object System.Net.Sockets.TcpClient
$tcpClient.Connect("192.168.1.20", 8765)
$tcpClient.Close()
```

---

## 📞 Suporte

Se nenhuma dessas soluções resolver seu problema:

1. **Colete informações:**
   ```powershell
   # Status do serviço
   Get-Service MigracaoADAgent | Format-List *
   
   # Logs
   Get-EventLog -LogName Application -Source MigracaoADAgent -Newest 50 | Export-Csv logs.csv
   
   # Firewall
   Get-NetFirewallRule -DisplayName "MigracaoAD Agent" | Format-List *
   
   # Conectividade
   Test-Connection -ComputerName localhost -Count 2
   Invoke-WebRequest -Uri "http://localhost:8765/health" -UseBasicParsing
   ```

2. **Abra uma issue no GitHub:**
   https://github.com/Caiolinooo/AD_Migrator/issues

3. **Inclua:**
   - Versão do Windows Server
   - Versão do agente
   - Logs coletados
   - Descrição detalhada do problema

---

## 🔄 Atualizações

Para atualizar o agente:

```powershell
# 1. Parar serviço
Stop-Service MigracaoADAgent

# 2. Substituir executável
Copy-Item ".\MigracaoAD.Agent.exe" -Destination "C:\Program Files\MigracaoAD\Agent\" -Force

# 3. Iniciar serviço
Start-Service MigracaoADAgent

# 4. Verificar versão
Invoke-WebRequest -Uri "http://localhost:8765/health" -UseBasicParsing | ConvertFrom-Json | Select-Object version
```

---

## ✅ Checklist de Instalação

Use este checklist para garantir que tudo está configurado corretamente:

- [ ] Windows Server 2012 R2 ou superior
- [ ] .NET 8.0 Runtime instalado (ou usar versão self-contained)
- [ ] Firewall configurado (porta 8765)
- [ ] Serviço MigracaoADAgent rodando
- [ ] Token configurado corretamente
- [ ] Teste de health funcionando (`/health`)
- [ ] Conectividade entre servidores OK
- [ ] App Manager consegue conectar aos agentes

---

**Última atualização:** 28/10/2025 - v1.0.1

