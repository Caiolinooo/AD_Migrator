# 🚀 Instalação Rápida do Agente

## 📦 O que você precisa

1. **Arquivo do agente**: `agent\publish\MigracaoAD.Agent.exe` (99 MB)
2. **Script de instalação**: `agent\install-agent.ps1`
3. **Acesso administrativo** nos servidores

---

## 🔧 Passo a Passo

### 1️⃣ Copiar arquivos para os servidores

Copie estes 2 arquivos para cada servidor (origem e destino):
- `MigracaoAD.Agent.exe`
- `install-agent.ps1`

**Exemplo**: Copie para `C:\Temp\` em cada servidor

---

### 2️⃣ Instalar no Servidor ORIGEM (Windows Server 2012)

1. Abra **PowerShell como Administrador**
2. Navegue até a pasta onde copiou os arquivos:
   ```powershell
   cd C:\Temp
   ```
3. Execute o script de instalação:
   ```powershell
   .\install-agent.ps1 -Token "meu-token-secreto-123"
   ```

**IMPORTANTE**: Anote o token que você usou! Você vai precisar dele no app.

---

### 3️⃣ Instalar no Servidor DESTINO (Windows Server 2019)

Repita o mesmo processo no servidor destino:

1. Abra **PowerShell como Administrador**
2. Navegue até a pasta:
   ```powershell
   cd C:\Temp
   ```
3. Execute com o **MESMO TOKEN**:
   ```powershell
   .\install-agent.ps1 -Token "meu-token-secreto-123"
   ```

---

### 4️⃣ Verificar se está funcionando

Em cada servidor, execute:

```powershell
# Verificar se o serviço está rodando
Get-Service MigracaoADAgent

# Testar o endpoint de saúde
Invoke-WebRequest http://localhost:8765/health
```

Se retornar informações do sistema, está funcionando! ✅

---

### 5️⃣ Configurar no App WPF

1. Abra o app `MigracaoAD.UI.exe`
2. Vá para a página **"Ambiente & Credenciais"**
3. Preencha:
   - **IP do servidor origem**: Ex: `192.168.1.10`
   - **IP do servidor destino**: Ex: `192.168.1.20`
   - **Token de autenticação**: `meu-token-secreto-123` (o mesmo que você usou)
   - **Porta do agente**: `8765` (padrão)
4. Clique em **"🔌 Testar Conexão Origem"** e **"🔌 Testar Conexão Destino"**

Se aparecer "✅ Conectado", está tudo certo! 🎉

---

## 🔥 Firewall

O script de instalação já configura o firewall automaticamente para liberar a porta 8765.

Se tiver problemas de conexão, verifique:

```powershell
# Ver regra do firewall
Get-NetFirewallRule -DisplayName "MigracaoAD Agent"

# Testar conectividade de outro servidor
Test-NetConnection -ComputerName 192.168.1.10 -Port 8765
```

---

## 🛠️ Comandos Úteis

### Ver logs do serviço
```powershell
Get-EventLog -LogName Application -Source MigracaoADAgent -Newest 10
```

### Reiniciar o serviço
```powershell
Restart-Service MigracaoADAgent
```

### Parar o serviço
```powershell
Stop-Service MigracaoADAgent
```

### Desinstalar
```powershell
.\uninstall-agent.ps1
```

---

## ❓ Problemas Comuns

### "Não foi possível conectar ao agente"

1. Verifique se o serviço está rodando:
   ```powershell
   Get-Service MigracaoADAgent
   ```
   Se não estiver, inicie:
   ```powershell
   Start-Service MigracaoADAgent
   ```

2. Verifique o firewall:
   ```powershell
   Test-NetConnection -ComputerName localhost -Port 8765
   ```

3. Verifique o token:
   - O token deve ser EXATAMENTE o mesmo em ambos os servidores
   - O token no app deve ser EXATAMENTE o mesmo usado na instalação

### "Token inválido"

O token no app não corresponde ao token usado na instalação. Reinstale com o token correto:

```powershell
.\uninstall-agent.ps1
.\install-agent.ps1 -Token "token-correto"
```

### "Porta 8765 já está em uso"

Outro serviço está usando a porta. Instale em outra porta:

```powershell
.\install-agent.ps1 -Token "meu-token" -Port 8766
```

E configure a mesma porta no app.

---

## 📊 Teste Completo

Depois de instalar em ambos os servidores, vá para a página **"Teste de Agentes"** no app e clique em:

1. **"🔄 Testar Ambos"** - Testa conexão com os dois servidores
2. **"📊 Obter Informações"** - Mostra detalhes do sistema
3. **"🌐 Testar Conectividade Entre Servidores"** - Verifica se os servidores conseguem se comunicar

---

## ✅ Checklist

- [ ] Copiei `MigracaoAD.Agent.exe` e `install-agent.ps1` para ambos os servidores
- [ ] Instalei no servidor origem com `.\install-agent.ps1 -Token "meu-token"`
- [ ] Instalei no servidor destino com o MESMO token
- [ ] Verifiquei que o serviço está rodando em ambos: `Get-Service MigracaoADAgent`
- [ ] Testei o endpoint local em ambos: `Invoke-WebRequest http://localhost:8765/health`
- [ ] Configurei os IPs e token no app
- [ ] Testei a conexão no app e apareceu "✅ Conectado"

---

## 🎯 Pronto!

Agora você pode usar o app para gerenciar os servidores remotamente! 🚀

Todas as operações (configuração de rede, instalação de roles, criação de compartilhamentos, etc.) serão feitas através dos agentes.

**Muito mais confiável e rápido que WinRM!** ⚡

