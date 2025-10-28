# 🚀 Guia de Configuração - Servidor na Nuvem (Proxmox/EVEO)

## ✅ Pré-requisitos
- Windows Server 2019 instalado no Proxmox
- Acesso RDP ao servidor
- Conectividade de rede entre local e nuvem
- Credenciais de Domain Admin do domínio local

---

## 📝 ETAPA 1: Configuração de Rede e Conectividade

### 1.1 Configurar IP Estático no Servidor Nuvem
```powershell
# Execute no PowerShell como Administrador no servidor da NUVEM

# Listar adaptadores de rede
Get-NetAdapter

# Configurar IP estático (AJUSTE OS VALORES!)
$InterfaceAlias = "Ethernet"  # Nome do adaptador
$IPAddress = "10.0.0.20"      # IP do servidor nuvem
$PrefixLength = 24            # Máscara (24 = 255.255.255.0)
$Gateway = "10.0.0.1"         # Gateway
$DNS1 = "192.168.1.10"        # IP do DC local (IMPORTANTE!)
$DNS2 = "8.8.8.8"             # DNS secundário

New-NetIPAddress -InterfaceAlias $InterfaceAlias -IPAddress $IPAddress -PrefixLength $PrefixLength -DefaultGateway $Gateway
Set-DnsClientServerAddress -InterfaceAlias $InterfaceAlias -ServerAddresses $DNS1,$DNS2

# Verificar
Get-NetIPAddress -InterfaceAlias $InterfaceAlias
Get-DnsClientServerAddress -InterfaceAlias $InterfaceAlias
```

### 1.2 Testar Conectividade com o Servidor Local
```powershell
# Execute no servidor da NUVEM

# Substituir pelo IP/hostname do servidor LOCAL
$ServidorLocal = "192.168.1.10"  # IP do DC local

# Testar ping
Test-Connection $ServidorLocal -Count 4

# Testar portas críticas
Test-NetConnection $ServidorLocal -Port 53   # DNS
Test-NetConnection $ServidorLocal -Port 88   # Kerberos
Test-NetConnection $ServidorLocal -Port 135  # RPC
Test-NetConnection $ServidorLocal -Port 389  # LDAP
Test-NetConnection $ServidorLocal -Port 445  # SMB
Test-NetConnection $ServidorLocal -Port 464  # Kerberos Password
Test-NetConnection $ServidorLocal -Port 636  # LDAPS
Test-NetConnection $ServidorLocal -Port 3268 # Global Catalog
Test-NetConnection $ServidorLocal -Port 5985 # WinRM HTTP
```

**⚠️ IMPORTANTE:** Se alguma porta falhar, você precisa:
- Abrir no firewall do Proxmox/EVEO
- Abrir no firewall do Windows (ambos servidores)
- Verificar se há VPN/túnel entre local e nuvem

---

## 📝 ETAPA 2: Abrir Portas no Firewall do Windows

### 2.1 No Servidor LOCAL (Windows 2012)
```powershell
# Execute no PowerShell como Administrador no servidor LOCAL

# Habilitar WinRM (para gerenciamento remoto)
Enable-PSRemoting -Force
Set-Item WSMan:\localhost\Client\TrustedHosts -Value "*" -Force

# Abrir portas do Active Directory
New-NetFirewallRule -DisplayName "AD - DNS (TCP)" -Direction Inbound -Protocol TCP -LocalPort 53 -Action Allow
New-NetFirewallRule -DisplayName "AD - DNS (UDP)" -Direction Inbound -Protocol UDP -LocalPort 53 -Action Allow
New-NetFirewallRule -DisplayName "AD - Kerberos (TCP)" -Direction Inbound -Protocol TCP -LocalPort 88 -Action Allow
New-NetFirewallRule -DisplayName "AD - Kerberos (UDP)" -Direction Inbound -Protocol UDP -LocalPort 88 -Action Allow
New-NetFirewallRule -DisplayName "AD - RPC (TCP)" -Direction Inbound -Protocol TCP -LocalPort 135 -Action Allow
New-NetFirewallRule -DisplayName "AD - LDAP (TCP)" -Direction Inbound -Protocol TCP -LocalPort 389 -Action Allow
New-NetFirewallRule -DisplayName "AD - LDAP (UDP)" -Direction Inbound -Protocol UDP -LocalPort 389 -Action Allow
New-NetFirewallRule -DisplayName "AD - SMB (TCP)" -Direction Inbound -Protocol TCP -LocalPort 445 -Action Allow
New-NetFirewallRule -DisplayName "AD - Kerberos Pwd (TCP)" -Direction Inbound -Protocol TCP -LocalPort 464 -Action Allow
New-NetFirewallRule -DisplayName "AD - Kerberos Pwd (UDP)" -Direction Inbound -Protocol UDP -LocalPort 464 -Action Allow
New-NetFirewallRule -DisplayName "AD - LDAPS (TCP)" -Direction Inbound -Protocol TCP -LocalPort 636 -Action Allow
New-NetFirewallRule -DisplayName "AD - Global Catalog (TCP)" -Direction Inbound -Protocol TCP -LocalPort 3268 -Action Allow
New-NetFirewallRule -DisplayName "AD - Global Catalog SSL (TCP)" -Direction Inbound -Protocol TCP -LocalPort 3269 -Action Allow
New-NetFirewallRule -DisplayName "AD - RPC Dynamic" -Direction Inbound -Protocol TCP -LocalPort 49152-65535 -Action Allow

# Verificar regras criadas
Get-NetFirewallRule | Where-Object {$_.DisplayName -like "AD -*"}
```

### 2.2 No Servidor NUVEM (Windows 2019)
```powershell
# Execute no PowerShell como Administrador no servidor da NUVEM

# Habilitar WinRM
Enable-PSRemoting -Force
Set-Item WSMan:\localhost\Client\TrustedHosts -Value "*" -Force

# Abrir as mesmas portas (copie os comandos acima)
# ... (mesmos comandos New-NetFirewallRule do servidor local)
```

---

## 📝 ETAPA 3: Instalar Roles e Features no Servidor Nuvem

### 3.1 Instalar AD DS e Ferramentas
```powershell
# Execute no PowerShell como Administrador no servidor da NUVEM

# Instalar AD Domain Services
Install-WindowsFeature -Name AD-Domain-Services -IncludeManagementTools

# Instalar DFS (para replicação de arquivos)
Install-WindowsFeature -Name FS-DFS-Namespace,FS-DFS-Replication -IncludeManagementTools

# Instalar File Server
Install-WindowsFeature -Name FS-FileServer -IncludeManagementTools

# Verificar instalação
Get-WindowsFeature | Where-Object {$_.Installed -eq $true -and $_.Name -like "*AD-*"}
Get-WindowsFeature | Where-Object {$_.Installed -eq $true -and $_.Name -like "*DFS*"}
```

### 3.2 Preparar Disco para Shares (se necessário)
```powershell
# Se você tiver um disco adicional para os shares (como E:\ no local)

# Listar discos
Get-Disk

# Inicializar disco 1 (AJUSTE O NÚMERO!)
Initialize-Disk -Number 1 -PartitionStyle GPT

# Criar partição e formatar
New-Partition -DiskNumber 1 -UseMaximumSize -DriveLetter E
Format-Volume -DriveLetter E -FileSystem NTFS -NewFileSystemLabel "Shares" -Confirm:$false

# Verificar
Get-Volume -DriveLetter E
```

---

## 📝 ETAPA 4: Informações Necessárias para o EXE

Antes de executar o `MigracaoAD.UI.exe`, anote estas informações:

### 4.1 Servidor LOCAL (Origem)
```
Domínio: _____________________________ (ex: empresa.local)
Nome do DC: __________________________ (ex: DC01)
IP do DC: ____________________________ (ex: 192.168.1.10)
Versão Windows: ______________________ (Windows Server 2012 R2)
Servidor de Arquivos: ________________ (ex: DC01 ou servidor separado)
IP Servidor Arquivos: ________________ (ex: 192.168.1.10)
Caminho Shares: ______________________ (ex: E:\Shares)
```

### 4.2 Servidor NUVEM (Destino)
```
Domínio: _____________________________ (MESMO do local!)
Nome do DC: __________________________ (ex: DC02-CLOUD)
IP do DC: ____________________________ (ex: 10.0.0.20)
Versão Windows: ______________________ (Windows Server 2019)
Servidor de Arquivos: ________________ (ex: DC02-CLOUD)
IP Servidor Arquivos: ________________ (ex: 10.0.0.20)
Caminho Shares: ______________________ (ex: E:\Shares)
```

### 4.3 Credenciais
```
Usuário Domain Admin: ________________ (ex: EMPRESA\Administrador)
Senha: _______________________________ (guarde com segurança)
```

---

## 📝 ETAPA 5: Executar o MigracaoAD.UI.exe

### 5.1 Abrir o Executável
1. Navegue até: `D:\Projeto\Desenvolvendo\Migração_AD\publish\`
2. Duplo clique em: `MigracaoAD.UI.exe`
3. Aguarde o splash screen

### 5.2 Preencher o Wizard

#### Tela 1: Welcome
- Leia a introdução
- Clique em "Próximo"

#### Tela 2: Ambiente & Detecção
- **Domínio Origem:** (ex: empresa.local)
- **DC Origem:** (ex: DC01)
- **IP DC Origem:** (ex: 192.168.1.10)
- **Domínio Destino:** (MESMO da origem!)
- **DC Destino:** (ex: DC02-CLOUD)
- **IP DC Destino:** (ex: 10.0.0.20)
- Clique em **"Detectar versões"** para testar conectividade
- Clique em "Próximo"

#### Tela 3: Conectividade
- Escolha: **Direct** (se tiver conectividade direta) ou **Tunnel** (se usar VPN)
- Faixa RPC: deixe padrão (49152-65535)
- Clique em "Próximo"

#### Tela 4: Modo
- Escolha: **Option A - Promote/Transfer/Demote**
- Marque as opções:
  - ✅ Promote (promover servidor nuvem a DC)
  - ✅ Transfer FSMO (transferir roles)
  - ✅ Demote (rebaixar servidor local - CUIDADO!)
- Clique em "Próximo"

#### Tela 5: Arquivos
- **Servidor Origem:** (ex: DC01 ou IP 192.168.1.10)
- **Servidor Destino:** (ex: DC02-CLOUD ou IP 10.0.0.20)
- **Caminho Destino:** (ex: E:\Shares)
- Marque as opções:
  - ✅ Descobrir shares automaticamente
  - ✅ Gerar mapeamento
  - ✅ Criar shares no destino
  - ✅ Robocopy (espelho + ACLs)
  - ✅ DFS Namespace (opcional, mas recomendado)
  - ✅ DFS Replication (opcional, mas recomendado)
- Clique em "Próximo"

#### Tela 6: Resumo & Execução
- Revise todas as configurações
- **IMPORTANTE:** Marque **"Dry-run (simulação)"** na primeira vez!
- Clique em **"Executar Migração"**
- Acompanhe os logs em tempo real

### 5.3 Interpretar os Resultados

**Se dry-run passar sem erros:**
- ✅ Conectividade OK
- ✅ Credenciais OK
- ✅ Pré-requisitos OK
- ➡️ Execute novamente SEM dry-run para migração real

**Se houver erros:**
- ❌ Verifique conectividade (portas, firewall)
- ❌ Verifique credenciais (Domain Admin)
- ❌ Verifique DNS (servidor nuvem deve resolver o domínio)
- ❌ Cole os erros aqui que eu te ajudo

---

## 🎯 Ordem de Execução (Option A)

O EXE vai executar nesta ordem:

1. **Prechecks** (verificações)
   - Testar conectividade
   - Verificar versões Windows
   - Verificar roles instalados

2. **Promote** (promover servidor nuvem)
   - Adicionar servidor nuvem como DC adicional
   - Replicar AD completo
   - Aguardar sincronização

3. **Transfer FSMO** (transferir roles)
   - Schema Master → Nuvem
   - Domain Naming Master → Nuvem
   - RID Master → Nuvem
   - PDC Emulator → Nuvem
   - Infrastructure Master → Nuvem

4. **Arquivos** (migrar shares)
   - Descobrir shares no servidor local
   - Criar shares no servidor nuvem
   - Robocopy (espelho + ACLs)
   - Configurar DFS Namespace
   - Configurar DFS Replication

5. **Demote** (rebaixar servidor local - OPCIONAL)
   - Remover roles FSMO do servidor local
   - Rebaixar DC local
   - Servidor local vira member server

---

## ⚠️ AVISOS IMPORTANTES

### Antes de Executar:
1. ✅ **Backup completo** do servidor local
2. ✅ **Snapshot** do servidor nuvem no Proxmox
3. ✅ **Testar conectividade** entre servidores
4. ✅ **Executar dry-run** primeiro
5. ✅ **Janela de manutenção** (usuários desconectados)

### Durante a Execução:
- ⏱️ Promote pode levar 30-60 minutos (depende do tamanho do AD)
- ⏱️ Robocopy pode levar horas (610GB de dados)
- 🚫 NÃO interrompa o processo
- 📊 Acompanhe os logs

### Depois da Execução:
- ✅ Verificar replicação AD: `repadmin /replsummary`
- ✅ Verificar FSMO: `netdom query fsmo`
- ✅ Testar login de usuários
- ✅ Testar acesso aos shares
- ✅ Verificar DFS: `dfsutil /root:\\dominio\namespace /view`

---

## 🆘 Troubleshooting

### Erro: "Não foi possível conectar ao servidor"
- Verificar firewall (portas 135, 445, 389, 636, 3268, 5985)
- Verificar DNS (servidor nuvem deve resolver o domínio)
- Verificar credenciais (Domain Admin)

### Erro: "Promote falhou"
- Verificar se DNS está apontando para o DC local
- Verificar se o servidor nuvem consegue resolver o domínio
- Verificar se as portas AD estão abertas

### Erro: "Robocopy falhou"
- Verificar permissões (Domain Admin)
- Verificar espaço em disco no servidor nuvem
- Verificar conectividade SMB (porta 445)

### Erro: "DFS falhou"
- Verificar se o role DFS está instalado
- Verificar se o namespace já existe
- Verificar permissões

---

## 📞 Suporte

Se tiver qualquer dúvida ou erro, me envie:
1. Screenshot da tela de erro
2. Logs do PowerShell (na janela de execução)
3. Resultado dos testes de conectividade

**Desenvolvido com ❤️ e muito 💪 por Caio Valerio Goulart Correia**

