# 🤖 Agente de Migração AD

Sistema cliente-servidor para gerenciamento remoto de servidores Windows durante migração Active Directory.

## 📋 Visão Geral

O **Agente de Migração AD** é um Windows Service leve que roda nos servidores (origem e destino) e permite que o aplicativo principal gerencie remotamente:

- ✅ Execução de comandos PowerShell
- ✅ Configuração de rede (IP, DNS, Gateway)
- ✅ Instalação de roles (AD DS, DFS, File Server)
- ✅ Promoção a Domain Controller
- ✅ Criação de compartilhamentos SMB
- ✅ Configuração de firewall
- ✅ Coleta de informações do sistema
- ✅ Testes de conectividade

## 🏗️ Arquitetura

```
┌─────────────────────────────────────┐
│   APP PRINCIPAL (Manager)           │
│   - Interface WPF                   │
│   - Orquestra a migração            │
│   - Comunica via HTTP REST          │
└──────────────┬──────────────────────┘
               │
       ┌───────┴───────┐
       │               │
┌──────▼──────┐ ┌─────▼───────┐
│ AGENTE      │ │ AGENTE      │
│ ORIGEM      │ │ DESTINO     │
│ (Win 2012)  │ │ (Win 2019)  │
│             │ │             │
│ Port: 8765  │ │ Port: 8765  │
│ API REST    │ │ API REST    │
└─────────────┘ └─────────────┘
```

## 🚀 Instalação Rápida

### 1️⃣ Compilar o Agente

No computador de desenvolvimento:

```powershell
cd agent
dotnet publish MigracaoAD.Agent/MigracaoAD.Agent.csproj -c Release -r win-x64 --self-contained true -o publish
```

### 2️⃣ Copiar para os Servidores

Copie a pasta `publish` para cada servidor (origem e destino):

```powershell
# Exemplo via compartilhamento de rede
Copy-Item -Path .\publish\* -Destination "\\SERVIDOR\C$\Temp\MigracaoAgent\" -Recurse
```

### 3️⃣ Instalar nos Servidores

Em **cada servidor** (origem e destino), execute como **Administrador**:

```powershell
cd C:\Temp\MigracaoAgent
.\install-agent.ps1 -Token "seu-token-secreto-aqui"
```

**Parâmetros opcionais:**
- `-Token`: Token de autenticação (padrão: `default-token-change-me`)
- `-Port`: Porta do serviço (padrão: `8765`)
- `-InstallPath`: Caminho de instalação (padrão: `C:\Program Files\MigracaoAD\Agent`)

### 4️⃣ Verificar Instalação

```powershell
# Verificar status do serviço
Get-Service MigracaoADAgent

# Testar conectividade
Invoke-WebRequest -Uri "http://localhost:8765/health" -UseBasicParsing
```

## 🔐 Segurança

### Token de Autenticação

O agente usa um token simples para autenticação. **IMPORTANTE:**

1. **Use um token forte** (mínimo 32 caracteres aleatórios)
2. **Use o MESMO token** em ambos os servidores
3. **Configure o token no app manager**

Gerar token seguro:

```powershell
# PowerShell
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | ForEach-Object {[char]$_})
```

### Firewall

O instalador automaticamente:
- ✅ Libera a porta 8765 (TCP Inbound)
- ✅ Permite conexões de qualquer IP

**Para produção**, restrinja o firewall:

```powershell
# Permitir apenas do IP do manager
Set-NetFirewallRule -DisplayName "MigracaoAD Agent" -RemoteAddress "192.168.0.100"
```

## 📡 API REST

### Endpoints Disponíveis

#### Health Check (sem autenticação)
```http
GET /health
```

#### Executar Comando
```http
POST /api/execute
Content-Type: application/json
X-Agent-Token: seu-token

{
  "command": "Get-Service",
  "asAdmin": true
}
```

#### Informações do Sistema
```http
GET /api/system
X-Agent-Token: seu-token
```

#### Informações do Domínio
```http
GET /api/domain
X-Agent-Token: seu-token
```

#### Configurar Rede
```http
POST /api/network/configure
X-Agent-Token: seu-token

{
  "interfaceName": "Ethernet",
  "ipAddress": "192.168.0.10",
  "subnetMask": 24,
  "gateway": "192.168.0.1",
  "dnsServers": ["192.168.0.1", "8.8.8.8"]
}
```

#### Instalar Role
```http
POST /api/roles/install
X-Agent-Token: seu-token

{
  "roleName": "AD-Domain-Services"
}
```

#### Promover a DC
```http
POST /api/domain/promote
X-Agent-Token: seu-token

{
  "domainName": "corp.local",
  "safeModePassword": "P@ssw0rd123!",
  "isNewForest": true
}
```

#### Criar Compartilhamento
```http
POST /api/shares/create
X-Agent-Token: seu-token

{
  "shareName": "Dados",
  "path": "E:\\Shares\\Dados",
  "fullAccessUsers": "Everyone"
}
```

#### Configurar Firewall
```http
POST /api/firewall/configure
X-Agent-Token: seu-token

{
  "ruleName": "Allow RPC",
  "direction": "Inbound",
  "protocol": "TCP",
  "port": 135
}
```

#### Reiniciar Servidor
```http
POST /api/system/reboot
X-Agent-Token: seu-token

{
  "delaySeconds": 30
}
```

## 🛠️ Uso no App Manager

No aplicativo WPF, use a classe `AgentClient`:

```csharp
using MigracaoAD.UI.Services;

// Criar cliente
var client = new AgentClient("192.168.0.10", port: 8765, token: "seu-token");

// Testar conectividade
var health = await client.CheckHealthAsync();
if (health != null)
{
    Console.WriteLine($"Conectado a {health.Hostname}");
}

// Obter informações do sistema
var sysInfo = await client.GetSystemInfoAsync();
Console.WriteLine($"OS: {sysInfo.WindowsVersion}");
Console.WriteLine($"Roles: {string.Join(", ", sysInfo.InstalledRoles)}");

// Executar comando
var result = await client.ExecuteCommandAsync("Get-ADUser -Filter *");
if (result.Success)
{
    Console.WriteLine(result.Output);
}

// Instalar role
var installResult = await client.InstallRoleAsync("AD-Domain-Services");
if (installResult.Success)
{
    Console.WriteLine("Role instalada com sucesso!");
}
```

## 🔧 Manutenção

### Ver Logs

```powershell
# Logs do Windows Service
Get-EventLog -LogName Application -Source MigracaoADAgent -Newest 50

# Ou use Event Viewer
eventvwr.msc
```

### Comandos Úteis

```powershell
# Status do serviço
Get-Service MigracaoADAgent

# Parar serviço
Stop-Service MigracaoADAgent

# Iniciar serviço
Start-Service MigracaoADAgent

# Reiniciar serviço
Restart-Service MigracaoADAgent

# Ver processos
Get-Process | Where-Object {$_.ProcessName -like "*MigracaoAD*"}
```

### Desinstalar

```powershell
cd C:\Program Files\MigracaoAD\Agent
.\uninstall-agent.ps1
```

## 🐛 Troubleshooting

### Agente não inicia

1. Verificar logs: `Get-EventLog -LogName Application -Newest 10`
2. Verificar se a porta está em uso: `netstat -ano | findstr 8765`
3. Verificar permissões: O serviço roda como `LocalSystem`

### Não consegue conectar

1. Testar localmente: `Invoke-WebRequest http://localhost:8765/health`
2. Verificar firewall: `Get-NetFirewallRule -DisplayName "MigracaoAD Agent"`
3. Verificar se o serviço está rodando: `Get-Service MigracaoADAgent`

### Erro 401 (Unauthorized)

- Verificar se o token está correto
- Token é case-sensitive
- Verificar variável de ambiente: `[Environment]::GetEnvironmentVariable("AGENT_TOKEN", "Machine")`

## 📦 Estrutura de Arquivos

```
agent/
├── MigracaoAD.Agent/
│   ├── Program.cs              # Servidor HTTP + Windows Service
│   ├── Controllers/
│   │   └── AgentController.cs  # Endpoints da API
│   └── MigracaoAD.Agent.csproj
├── install-agent.ps1           # Script de instalação
├── uninstall-agent.ps1         # Script de desinstalação
└── README.md                   # Este arquivo
```

## 🎯 Próximos Passos

Após instalar o agente nos servidores:

1. ✅ Anote os IPs dos servidores
2. ✅ Anote o token usado
3. ✅ No app manager, vá para "Ambiente & Credenciais"
4. ✅ Preencha os IPs e o token
5. ✅ Clique em "Testar Conexão com Agente"
6. ✅ Se conectar, você pode usar todos os recursos remotos!

## 📝 Notas

- O agente roda como **LocalSystem** (máximos privilégios)
- Porta padrão: **8765** (TCP)
- Protocolo: **HTTP** (não HTTPS por simplicidade)
- Para produção, considere adicionar HTTPS com certificado
- O agente **não persiste dados** - é stateless
- Cada comando é executado de forma independente

## 🤝 Suporte

Em caso de problemas:

1. Verifique os logs do Windows Event Viewer
2. Teste a conectividade com `Test-NetConnection`
3. Verifique se o firewall está liberado
4. Confirme que o token está correto

