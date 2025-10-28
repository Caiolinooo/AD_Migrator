param(
    [switch]$Force = $true
)

# Despromove o DC local (executar no DC antigo após validações completas)
$localAdminPwd = Read-Host -AsSecureString "Nova senha do Administrador local (pós-despromoção)"
Uninstall-ADDSDomainController -DemoteOperationMasterRole -Force:$Force -LocalAdministratorPassword $localAdminPwd

Write-Host "DC despromovido. Verifique a remoção de metadados (nTDSDSA) se necessário." -ForegroundColor Yellow
