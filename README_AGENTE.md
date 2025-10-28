# 🤖 Sistema de Agentes - Migração AD

## 📖 Visão Geral

Este projeto agora utiliza uma **arquitetura cliente-servidor** para gerenciar os servidores remotamente, substituindo o WinRM por um sistema mais confiável e rápido.

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

---

## 🚀 Início Rápido

### 1. Compilar o Agente

```powershell
cd agent
dotnet publish MigracaoAD.Agent/MigracaoAD.Agent.csproj -c Release -r win-x64 --self-contained true -o publish
```

O executável estará em: `agent\publish\MigracaoAD.Agent.exe`

### 2. Instalar nos Servidores

Copie `MigracaoAD.Agent.exe` e `install-agent.ps1` para cada servidor e execute:

```powershell
.\install-agent.ps1 -Token "seu-token-secreto"
```

**IMPORTANTE**: Use o mesmo token em ambos os servidores!

### 3. Usar no App

1. Abra `MigracaoAD.UI.exe`
2. Vá para "Ambiente & Credenciais"
3. Configure:
   - IPs dos servidores
   - Token (o mesmo usado na instalação)
   - Porta (8765 por padrão)
4. Teste a conexão

---

## 📁 Estrutura do Projeto

```
Migração_AD/
├── agent/                          # Sistema de agentes
│   ├── MigracaoAD.Agent/          # Código do agente
│   │   ├── Program.cs             # Servidor HTTP + Windows Service
│   │   ├── Controllers/           # API REST
│   │   └── MigracaoAD.Agent.csproj
│   ├── publish/                   # Executável compilado
│   │   └── MigracaoAD.Agent.exe   # 99 MB
│   ├── install-agent.ps1          # Script de instalação
│   ├── uninstall-agent.ps1        # Script de desinstalação
│   ├── README.md                  # Documentação técnica
│   └── INSTALACAO_RAPIDA.md       # Guia rápido
│
├── ui-wpf/                        # Aplicação WPF
│   ├── Services/
│   │   └── AgentClient.cs         # Cliente HTTP para o agente
│   ├── Views/
│   │   ├── EnvironmentPage.xaml   # Configuração + teste de agentes
│   │   └── AgentTestPage.xaml     # Página dedicada para testes
│   └── MainWindow.xaml.cs         # Navegação (7 etapas)
│
├── GUIA_AGENTE.md                 # Guia completo em português
└── README_AGENTE.md               # Este arquivo
```

---

## 🎯 Funcionalidades do Agente

O agente fornece uma API REST completa para:

### 📊 Informações do Sistema
- Hostname, OS, versão do Windows
- CPU, memória, discos
- Roles instaladas
- Informações do domínio (se for DC)
- Compartilhamentos SMB

### ⚙️ Configuração
- Configurar IP estático, DNS, gateway
- Instalar roles (AD DS, DFS, File Server)
- Promover a Domain Controller
- Criar compartilhamentos SMB
- Configurar firewall

### 💻 Execução
- Executar comandos PowerShell
- Testar conectividade
- Reiniciar servidor

---

## 🔐 Segurança

### Autenticação
- Token baseado em header HTTP (`X-Agent-Token`)
- Token configurado via variável de ambiente
- Endpoint `/health` público (sem autenticação)

### Firewall
- Porta 8765 liberada automaticamente na instalação
- Regra específica para o agente

### Execução
- Roda como Windows Service
- Executa comandos com privilégios de SYSTEM
- Ideal para operações administrativas

---

## 📖 Documentação

### Para Usuários
- **[INSTALACAO_RAPIDA.md](agent/INSTALACAO_RAPIDA.md)** - Guia passo a passo
- **[GUIA_AGENTE.md](GUIA_AGENTE.md)** - Guia completo com exemplos

### Para Desenvolvedores
- **[agent/README.md](agent/README.md)** - Documentação técnica da API
- **[ui-wpf/Services/AgentClient.cs](ui-wpf/Services/AgentClient.cs)** - Cliente HTTP

---

## 🆚 Comparação: Agente vs WinRM

| Característica | Agente | WinRM |
|----------------|--------|-------|
| **Configuração** | Simples (1 comando) | Complexa (múltiplos passos) |
| **Portas** | 1 porta (8765) | 3+ portas (5985, 5986, 135, 49152-65535) |
| **Autenticação** | Token simples | Kerberos/NTLM/CredSSP |
| **Cross-domain** | Funciona | Problemático |
| **Firewall** | Automático | Manual |
| **Confiabilidade** | Alta | Média |
| **Performance** | Rápida | Lenta |
| **Debugging** | Fácil (HTTP) | Difícil |

---

## 🛠️ Desenvolvimento

### Compilar o App WPF
```powershell
dotnet build ui-wpf/MigracaoAD.UI.csproj -c Release
```

### Executar o App
```powershell
.\ui-wpf\bin\Release\net8.0-windows\MigracaoAD.UI.exe
```

### Testar o Agente Localmente
```powershell
# Compilar
cd agent
dotnet run --project MigracaoAD.Agent

# Em outro terminal, testar
Invoke-WebRequest http://localhost:8765/health
```

---

## 📋 Etapas do Wizard

O app WPF agora tem **7 etapas**:

1. **Bem-vindo** - Introdução
2. **Ambiente & Credenciais** - Configuração dos servidores + teste de agentes
3. **Teste de Agentes** - Página dedicada para testes avançados
4. **Conectividade** - Teste de conectividade entre servidores
5. **Modo de Migração** - Escolher modo (mesmo domínio ou novo domínio)
6. **Arquivos (Espelho)** - Configurar espelhamento de arquivos
7. **Resumo & Executar** - Revisar e executar a migração

---

## 🧪 Testando

### Teste Básico
1. Instale o agente em um servidor
2. Abra o app
3. Configure o IP e token
4. Clique em "Testar Conexão"
5. Deve aparecer "✅ Conectado"

### Teste Completo
1. Instale em ambos os servidores
2. Vá para "Teste de Agentes"
3. Execute todos os testes:
   - Testar Conexão
   - Obter Informações
   - Executar Comando
   - Testar Ambos
   - Testar Conectividade Entre Servidores
   - Listar Compartilhamentos

---

## ❓ Solução de Problemas

### Agente não inicia
```powershell
# Ver logs
Get-EventLog -LogName Application -Source MigracaoADAgent -Newest 10

# Verificar serviço
Get-Service MigracaoADAgent

# Reiniciar
Restart-Service MigracaoADAgent
```

### Não consegue conectar
```powershell
# Testar localmente
Invoke-WebRequest http://localhost:8765/health

# Testar remotamente
Test-NetConnection -ComputerName 192.168.1.10 -Port 8765

# Verificar firewall
Get-NetFirewallRule -DisplayName "MigracaoAD Agent"
```

### Token inválido
- Verifique se o token no app é EXATAMENTE o mesmo usado na instalação
- Tokens são case-sensitive
- Reinstale com o token correto se necessário

---

## 🔄 Atualizações

### Atualizar o Agente
1. Compile a nova versão
2. Pare o serviço: `Stop-Service MigracaoADAgent`
3. Substitua o executável em `C:\Program Files\MigracaoAD\Agent\`
4. Inicie o serviço: `Start-Service MigracaoADAgent`

### Atualizar o App
1. Compile a nova versão
2. Substitua o executável
3. Pronto!

---

## 📞 Suporte

Para problemas ou dúvidas:
1. Consulte [GUIA_AGENTE.md](GUIA_AGENTE.md)
2. Consulte [agent/README.md](agent/README.md)
3. Verifique os logs do serviço
4. Teste a conectividade

---

## ✅ Checklist de Implantação

- [ ] Agente compilado (`agent\publish\MigracaoAD.Agent.exe`)
- [ ] Agente instalado no servidor origem
- [ ] Agente instalado no servidor destino
- [ ] Mesmo token usado em ambos
- [ ] Serviços rodando em ambos
- [ ] Firewall configurado
- [ ] App WPF compilado
- [ ] Teste de conexão bem-sucedido
- [ ] Teste de informações bem-sucedido
- [ ] Teste de conectividade entre servidores bem-sucedido

---

## 🎉 Pronto para Usar!

Agora você tem um sistema profissional de gerenciamento remoto para sua migração AD! 🚀

**Vantagens**:
- ✅ Mais confiável que WinRM
- ✅ Mais rápido
- ✅ Mais fácil de configurar
- ✅ Mais fácil de debugar
- ✅ Funciona cross-domain
- ✅ Firewall automático
- ✅ API REST completa

