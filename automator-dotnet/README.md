# MigracaoAD.Automator (.NET) — Opção B (novo domínio/ADMT)

Automatiza o fluxo de migração inter-florestas (novo domínio) com ADMT/SIDHistory e migração de arquivos (Robocopy), orquestrando scripts PowerShell já existentes e novos.

## Requisitos
- Windows com .NET 8 SDK para compilar (ou use o binário pronto quando disponível)
- PowerShell 5+
- Privilégios administrativos nos servidores/AD envolvidos
- Scripts base em `projeto-migracao/` (existentes) e `projeto-migracao/scripts-b` (novos)

## Build
```powershell
cd automator-dotnet
 dotnet build -c Release
# Copie appsettings.json ao lado do .exe conforme seu ambiente
```

## Configuração
- Copie `appsettings.sample.json` para `appsettings.json` e edite:
  - `SourceDomainName` = `abzservicos.local`
  - `TargetDomainName` = novo domínio (ex.: `corp.local`)
  - `AdmtServer` = servidor no novo domínio que terá o ADMT 3.2
  - `ConnectivityMode` = `Tunnel` (recomendado) ou `Direct`
  - `MapsCsv` = mapeamento dos shares (padrão já aponta para `projeto-migracao/samples/maps.csv`)

## Uso (exemplos)
```powershell
# Pré-checagens (estrutura de scripts)
MigracaoAD.Automator.exe precheck

# Trust e SIDHistory
MigracaoAD.Automator.exe setup-trust
MigracaoAD.Automator.exe enable-sidhistory

# ADMT (requer scripts-b correspondentes)
MigracaoAD.Automator.exe migrate-groups
MigracaoAD.Automator.exe migrate-users
MigracaoAD.Automator.exe migrate-computers
MigracaoAD.Automator.exe translate-security

# Arquivos (usa scripts existentes)
MigracaoAD.Automator.exe files-seed
MigracaoAD.Automator.exe files-delta

# DFSN/DFSR (opcional)
MigracaoAD.Automator.exe dfsn-setup
MigracaoAD.Automator.exe dfsr-setup

# Validação
MigracaoAD.Automator.exe validate
```

## Próximos passos
- Criaremos `projeto-migracao/scripts-b/` com:
  - `Create-Trust.ps1`, `Enable-SIDHistory.ps1`
  - `ADMT-Migrate-Groups.ps1`, `ADMT-Migrate-Users.ps1`, `ADMT-Migrate-Computers.ps1`, `ADMT-Translate-Security.ps1`
  - `DFSN-Create.ps1`, `DFSR-Setup.ps1`
- O executável já invoca esses scripts se presentes.

