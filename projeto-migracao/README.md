# Projeto de Migração AD + Shares (com preservação de ACLs)

Este projeto contém scripts PowerShell para:

- Inventariar o Active Directory (níveis funcionais, DCs, FSMO, sites/sub-redes, trusts, zonas DNS).
- Inventariar compartilhamentos de arquivos, permissões de compartilhamento e amostra de ACLs NTFS, além de métricas.
- Promover um novo DC na nuvem (mesmo domínio).
- Transferir as funções FSMO para o novo DC após replicação estável.
- Migrar dados de compartilhamentos preservando ACLs/timestamps com Robocopy.
- Despromover o DC antigo com segurança após validações.
 - Orquestrar todo o fluxo ponta a ponta com confirmações (ou totalmente automático via parâmetro).

Estrutura sugerida:

```
projeto-migracao/
  scripts/
    01-Discovery-AD.ps1
    02-Discovery-FileShares.ps1
    10-Promote-New-DC.ps1
    20-Transfer-FSMO.ps1
    30-Robocopy-Migrate.ps1
    40-Demote-Old-DC.ps1
    00-Prechecks.ps1
    24-Generate-Maps.ps1
    25-Create-Destination-Shares.ps1
  outputs/
  samples/
    maps.csv
  config/
    parameters.psd1
  orchestrator.ps1
```

## Pré-requisitos

- Acesso administrativo aos DCs e servidores de arquivos origem/destino.
- RSAT-AD-PowerShell nos hosts que rodarão scripts de AD (`ActiveDirectory` module) e, para DNS opcionalmente, `DnsServer`.
- Conectividade entre on-prem e nuvem (VPN/ExpressRoute) e resolução DNS cruzada.
- Se a estratégia de arquivos for Azure Files, confirmar suporte a identidade AD/Entra para SMB e planejar RBAC + NTFS.
- SYSVOL deve estar em DFSR (não FRS). O orquestrador bloqueará a execução caso detecte FRS.

## Parâmetros

Arquivo `config/parameters.psd1` (exemplo padrão criado):

```
@{
  DomainName            = 'contoso.local'
  SiteName              = 'CloudSite'
  OldDCHostname         = 'OLDDC01'
  NewDCHostname         = 'NEWDC01'
  DnsServer             = $null

  DatabasePath          = 'D:\\NTDS'
  LogPath               = 'E:\\Logs'
  SYSVOLPath            = 'D:\\SYSVOL'

  SourceFileServer      = 'OLDFS'
  DestinationFileServer = 'NEWFS'
  DestinationRootPath   = 'D:\\Shares'
  CreateDestinationShares = $true

  FileCopyThreads       = 32
  RobocopyRetry         = 2
  RobocopyWait          = 2

  AutoProceed           = $false
  UseRemoting           = $false

  OutputsDir            = '..\\outputs'
  MapsCsv               = '..\\samples\\maps.csv'
}
```

Edite estes valores conforme seu ambiente antes de rodar o orquestrador.

### Prompts em runtime — o que será solicitado e onde achar

- DomainName (DNS do domínio)
  - Exemplo: `contoso.local`
  - Onde obter: no DC atual, execute `Get-ADDomain | Select -ExpandProperty DNSRoot`.

- SiteName (nome do site do AD)
  - Exemplo: `CloudSite`
  - Onde obter: console `Active Directory Sites and Services` (Sites e Serviços do AD).

- OldDCHostname (DC antigo, on-prem)
  - Exemplo: `OLDDC01`
  - Onde obter: no DC antigo, `echo $env:COMPUTERNAME`.

- NewDCHostname (novo DC, nuvem)
  - Exemplo: `NEWDC01`
  - Onde obter: hostname/NetBIOS da VM que será promovida.

- SourceFileServer (servidor de arquivos origem)
  - Exemplo: `OLDFS`
  - Onde obter: nome NetBIOS do servidor que hospeda os shares atuais.

- DestinationFileServer (servidor de arquivos destino)
  - Exemplo: `NEWFS`
  - Onde obter: nome NetBIOS do novo servidor de arquivos na nuvem.

- DestinationRootPath (caminho raiz no destino para as pastas dos shares)
  - Exemplo: `D:\\Shares`
  - Onde obter: volume/pasta NTFS no servidor de destino onde os dados serão copiados.

Se você deixar valores em branco no `parameters.psd1`, o orquestrador vai pedir em tempo de execução (runtime) com exemplos e dicas de onde obter cada dado.

## Descoberta (executar antes)

1) AD:

```powershell
cd projeto-migracao\scripts
./01-Discovery-AD.ps1 -OutDir ..\outputs
# opcional: ./01-Discovery-AD.ps1 -OutDir ..\outputs -DnsServer <DC-ou-DNS>
```

Gera `AD-Summary.json`, `AD-DCs.csv`, `AD-Trusts.csv`, `AD-DNSZones.txt`.

2) Compartilhamentos:

```powershell
./02-Discovery-FileShares.ps1 -OutDir ..\outputs
# opcional: -SharesInclude Nome1,Nome2 -SkipMetrics
```

Gera `FS-Shares.csv` e `FS-NTFS-Sample.csv`.

## Execução — AD

3) Promover novo DC (na VM da nuvem):

```powershell
./10-Promote-New-DC.ps1 -DomainName seu.dominio.local -SiteName SeuSite [-InstallDNS]
```

A promoção solicitará a senha DSRM e pode reiniciar o servidor.

4) Após replicação estável, transferir FSMO para o novo DC:

```powershell
./20-Transfer-FSMO.ps1 -NewDC <HostnameDoNovoDC>
```

5) Validar logon, replicação AD/DNS, funções FSMO, e apontar clientes/servidores para DNS dos DCs na nuvem.

6) Quando tudo estiver validado, despromover o DC antigo:

```powershell
./40-Demote-Old-DC.ps1
```

## Execução — Dados (Shares)

7) Preparar mapeamento de cópias no CSV `samples/maps.csv`:

```
Orig,Dest,Log
\\OLDFS\Engenharia,\\NEWFS\Engenharia,C:\\Logs\\Engenharia.log
\\OLDFS\Publico,\\NEWFS\Publico,C:\\Logs\\Publico.log
```

8) Rodar a migração com Robocopy:

```powershell
./30-Robocopy-Migrate.ps1 -CsvMapPath ..\samples\maps.csv
# Para testes sem pré-cópia (somente delta/corte): adicionar -NoPreCopy
```

- Rodar ao menos uma passagem inicial sem `/MIR` (default faz pré-cópia com `/E`) e aplicar `/MIR` apenas no corte.
- Certifique-se de que o destino é NTFS para manter as ACLs. Para Azure Files, ajuste permissões no nível do share via RBAC antes da grande carga.

## Orquestrador (fluxo ponta a ponta)

Você pode executar todo o fluxo com o `orchestrator.ps1` a partir da pasta `projeto-migracao/`:

```powershell
cd projeto-migracao
./orchestrator.ps1 -ParamsPath .\config\parameters.psd1
# Observação: AutoProceed é $true por padrão no parameters.psd1.
# Para forçar confirmações interativas, rode com: -AutoProceed:$false
```

Se você deixou valores em branco no `parameters.psd1`, o orquestrador fará perguntas em runtime e mostrará exemplos/dicas para preenchimento.

Fases executadas:

1. Prechecks (inclui verificação DFSR/FRS).
2. Discovery (AD e Shares).
3. Promoção do novo DC.
4. Transferência de FSMO.
5. Criação/ajuste de shares no destino (opcional e controlado por parâmetro).
6. Geração automática do maps.csv (se não existir).
7. Migração de dados (Robocopy, pré-cópia e corte).
8. Despromoção do DC antigo.

Observações:
- `UseRemoting = $false` por padrão. Se ativar `$true`, garanta que os scripts e/ou lógica sejam executados no alvo (ou copie os scripts para o host remoto). Por simplicidade, execute localmente nos hosts quando possível.
- O orquestrador interrompe se detectar SYSVOL em FRS.

## Observações importantes

- Em ambientes inter-florestas/novo domínio é necessário ADMT/SIDHistory (fora do escopo destes scripts, mas referenciado na documentação).
- Valide janelas de replicação e saúde do AD/DNS antes de mover FSMO e despromover DC antigo.
- Sempre teste em ambiente de homologação antes do corte em produção.

## Referências

- Install AD DS: https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/deploy/install-active-directory-domain-services--level-100
- FSMO via PowerShell: https://learn.microsoft.com/en-us/powershell/module/activedirectory/move-addirectoryserveroperationmasterrole
- Robocopy: https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy
- Storage Migration Service: https://learn.microsoft.com/en-us/windows-server/storage/storage-migration-service/overview
