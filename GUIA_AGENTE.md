# 🚀 GUIA RÁPIDO - Sistema Cliente-Servidor para Migração AD

## 📋 O QUE FOI CRIADO?

Criei um sistema **cliente-servidor** completo que substitui o WinRM! Agora você tem:

### ✅ **AGENTE (Windows Service)**
- Roda nos servidores (origem e destino)
- API REST na porta 8765
- Executa comandos PowerShell com privilégios de administrador
- Configuração de rede, roles, firewall, etc.

### ✅ **CLIENTE (no App WPF)**
- Classe `AgentClient` para comunicação HTTP
- Métodos prontos para todas as operações
- Muito mais rápido e confiável que WinRM!

---

## 🏗️ ARQUITETURA

```
┌─────────────────────────────────────┐
│   SEU COMPUTADOR                    │
│   - App WPF (Manager)               │
│   - Controla tudo remotamente       │
└──────────────┬──────────────────────┘
               │ HTTP (porta 8765)
       ┌───────┴───────┐
       │               │
┌──────▼──────┐ ┌─────▼───────┐
│ SERVIDOR    │ │ SERVIDOR    │
│ ORIGEM      │ │ DESTINO     │
│ Win 2012    │ │ Win 2019    │
│             │ │             │
│ AGENTE      │ │ AGENTE      │
│ instalado   │ │ instalado   │
└─────────────┘ └─────────────┘
```

---

## 📦 PASSO 1: COMPILAR O AGENTE

No seu computador de desenvolvimento:

```powershell
cd "D:\Projeto\Desenvolvendo\Migração_AD"

# Compilar o agente
dotnet publish agent/MigracaoAD.Agent/MigracaoAD.Agent.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o agent/publish
```

Isso vai criar todos os arquivos necessários em `agent/publish/`

---

## 📤 PASSO 2: COPIAR PARA OS SERVIDORES

### Opção A: Via Compartilhamento de Rede

```powershell
# Copiar para servidor ORIGEM
Copy-Item -Path "agent\publish\*" `
          -Destination "\\SERVIDOR-ORIGEM\C$\Temp\MigracaoAgent\" `
          -Recurse -Force

# Copiar para servidor DESTINO
Copy-Item -Path "agent\publish\*" `
          -Destination "\\SERVIDOR-DESTINO\C$\Temp\MigracaoAgent\" `
          -Recurse -Force
```

### Opção B: Via Pendrive/RDP

1. Copie a pasta `agent\publish` para um pendrive
2. Conecte via RDP em cada servidor
3. Cole a pasta em `C:\Temp\MigracaoAgent\`

---

## ⚙️ PASSO 3: INSTALAR NOS SERVIDORES

### Em CADA servidor (origem e destino):

1. **Conecte via RDP** no servidor
2. **Abra PowerShell como Administrador**
3. Execute:

```powershell
cd C:\Temp\MigracaoAgent

# Gerar um token seguro (use o MESMO em ambos os servidores!)
$token = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | ForEach-Object {[char]$_})
Write-Host "Token gerado: $token" -ForegroundColor Green

# ANOTE ESTE TOKEN! Você vai precisar dele no app!

# Instalar o agente
.\install-agent.ps1 -Token $token
```

**IMPORTANTE:** 
- ✅ Use o **MESMO token** em ambos os servidores
- ✅ **Anote o token** - você vai precisar no app manager
- ✅ **Anote os IPs** dos servidores que aparecem no final da instalação

---

## ✅ PASSO 4: VERIFICAR INSTALAÇÃO

Em cada servidor, verifique:

```powershell
# Status do serviço
Get-Service MigracaoADAgent

# Deve mostrar: Status = Running

# Testar localmente
Invoke-WebRequest -Uri "http://localhost:8765/health" -UseBasicParsing

# Deve retornar JSON com informações do servidor
```

---

## 🎯 PASSO 5: USAR NO APP MANAGER

No seu app WPF, agora você pode usar o `AgentClient`:

### Exemplo de uso:

```csharp
using MigracaoAD.UI.Services;

// Criar cliente para servidor ORIGEM
var origemClient = new AgentClient(
    serverIp: "192.168.0.10",  // IP do servidor origem
    port: 8765,
    token: "seu-token-aqui"
);

// Criar cliente para servidor DESTINO
var destinoClient = new AgentClient(
    serverIp: "192.168.0.20",  // IP do servidor destino
    port: 8765,
    token: "seu-token-aqui"  // MESMO token!
);

// Testar conectividade
var health = await origemClient.CheckHealthAsync();
if (health != null)
{
    MessageBox.Show($"Conectado a {health.Hostname}!");
}

// Obter informações do sistema
var sysInfo = await origemClient.GetSystemInfoAsync();
MessageBox.Show($"OS: {sysInfo.WindowsVersion}\nRoles: {string.Join(", ", sysInfo.InstalledRoles)}");

// Executar comando PowerShell
var result = await origemClient.ExecuteCommandAsync("Get-ADUser -Filter * | Measure-Object");
if (result.Success)
{
    MessageBox.Show($"Resultado:\n{result.Output}");
}

// Instalar role no servidor destino
var installResult = await destinoClient.InstallRoleAsync("AD-Domain-Services");
if (installResult.Success)
{
    MessageBox.Show("Role AD DS instalada com sucesso!");
}

// Configurar rede no servidor destino
var networkResult = await destinoClient.ConfigureNetworkAsync(
    interfaceName: "Ethernet",
    ipAddress: "192.168.0.20",
    subnetMask: 24,
    gateway: "192.168.0.1",
    dnsServers: new List<string> { "192.168.0.1", "8.8.8.8" }
);

// Promover a Domain Controller
var promoteResult = await destinoClient.PromoteToDCAsync(
    domainName: "corp.local",
    safeModePassword: "P@ssw0rd123!",
    isNewForest: true
);
```

---

## 🔧 OPERAÇÕES DISPONÍVEIS

O `AgentClient` tem métodos para:

### 📊 Informações
- `CheckHealthAsync()` - Verifica se agente está respondendo
- `GetSystemInfoAsync()` - Informações do sistema (OS, CPU, RAM, roles)
- `GetDomainInfoAsync()` - Informações do domínio (se for DC)
- `GetSharesAsync()` - Lista compartilhamentos SMB
- `GetDiskInfoAsync()` - Informações de discos

### ⚙️ Configuração
- `ExecuteCommandAsync(command)` - Executa qualquer comando PowerShell
- `ConfigureNetworkAsync()` - Configura IP, gateway, DNS
- `InstallRoleAsync(roleName)` - Instala roles do Windows Server
- `PromoteToDCAsync()` - Promove servidor a Domain Controller
- `CreateShareAsync()` - Cria compartilhamento SMB
- `ConfigureFirewallAsync()` - Configura regras de firewall
- `RebootAsync()` - Reinicia o servidor
- `TestConnectionAsync()` - Testa conectividade com outro servidor

---

## 🛠️ COMANDOS ÚTEIS

### No servidor (via RDP):

```powershell
# Ver status do serviço
Get-Service MigracaoADAgent

# Parar serviço
Stop-Service MigracaoADAgent

# Iniciar serviço
Start-Service MigracaoADAgent

# Reiniciar serviço
Restart-Service MigracaoADAgent

# Ver logs
Get-EventLog -LogName Application -Source MigracaoADAgent -Newest 20

# Testar conectividade
Invoke-WebRequest http://localhost:8765/health

# Desinstalar
cd "C:\Program Files\MigracaoAD\Agent"
.\uninstall-agent.ps1
```

---

## 🐛 TROUBLESHOOTING

### ❌ Agente não inicia

```powershell
# Ver logs de erro
Get-EventLog -LogName Application -Newest 10

# Verificar se porta está em uso
netstat -ano | findstr 8765

# Verificar firewall
Get-NetFirewallRule -DisplayName "MigracaoAD Agent"
```

### ❌ Não consegue conectar do app

1. **Testar localmente no servidor:**
   ```powershell
   Invoke-WebRequest http://localhost:8765/health
   ```

2. **Testar do seu computador:**
   ```powershell
   Test-NetConnection -ComputerName 192.168.0.10 -Port 8765
   ```

3. **Verificar firewall do Windows:**
   - Painel de Controle → Firewall → Regras de Entrada
   - Procurar por "MigracaoAD Agent"
   - Deve estar habilitada

### ❌ Erro 401 (Unauthorized)

- Token está errado
- Token é **case-sensitive**
- Verificar token no servidor:
  ```powershell
  [Environment]::GetEnvironmentVariable("AGENT_TOKEN", "Machine")
  ```

---

## 🎯 VANTAGENS SOBRE WINRM

| Recurso | WinRM | Agente |
|---------|-------|--------|
| **Configuração** | Complexa | Simples |
| **Firewall** | Múltiplas portas | 1 porta (8765) |
| **Autenticação** | Kerberos/NTLM | Token simples |
| **Velocidade** | Lenta | Rápida |
| **Confiabilidade** | Média | Alta |
| **Debugging** | Difícil | Fácil (logs claros) |
| **Cross-domain** | Problemático | Funciona |

---

## 📝 CHECKLIST DE INSTALAÇÃO

- [ ] Compilei o agente (`dotnet publish`)
- [ ] Copiei para servidor ORIGEM
- [ ] Copiei para servidor DESTINO
- [ ] Instalei no servidor ORIGEM
- [ ] Instalei no servidor DESTINO
- [ ] Usei o MESMO token em ambos
- [ ] Anotei o token
- [ ] Anotei os IPs dos servidores
- [ ] Testei conectividade local (localhost:8765/health)
- [ ] Testei conectividade remota do meu PC
- [ ] Configurei o token no app manager
- [ ] Testei `CheckHealthAsync()` no app

---

## 🎉 PRONTO!

Agora você tem um sistema cliente-servidor profissional para gerenciar a migração AD!

**Próximos passos:**
1. Integrar o `AgentClient` nas páginas do app WPF
2. Substituir chamadas WinRM por chamadas ao agente
3. Testar todas as operações
4. Executar a migração completa!

---

## 📞 SUPORTE

Se tiver problemas:
1. Verifique os logs: `Get-EventLog -LogName Application`
2. Teste conectividade: `Test-NetConnection -Port 8765`
3. Verifique firewall: `Get-NetFirewallRule`
4. Confirme que o serviço está rodando: `Get-Service MigracaoADAgent`

