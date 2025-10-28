param(
    [Parameter(Mandatory=$true)][string]$DomainName,
    [Parameter(Mandatory=$true)][string]$SiteName,
    [switch]$InstallDNS = $true,
    [string]$DatabasePath = "D:\\NTDS",
    [string]$LogPath      = "E:\\Logs",
    [string]$SYSVOLPath   = "D:\\SYSVOL"
)

Install-WindowsFeature AD-Domain-Services -IncludeManagementTools

$cred = Get-Credential "$DomainName\\Administrator"
$common = @{
    Credential                   = $cred
    DomainName                   = $DomainName
    SiteName                     = $SiteName
    DatabasePath                 = $DatabasePath
    LogPath                      = $LogPath
    SYSVOLPath                   = $SYSVOLPath
    SafeModeAdministratorPassword= (Read-Host -AsSecureString "Digite a senha do Modo de Restauração (DSRM)")
    Force                        = $true
}

if ($InstallDNS) {
    Install-ADDSDomainController @common -InstallDNS
} else {
    Install-ADDSDomainController @common
}

# Observação: a promoção solicitará (ou executará) reinicialização após concluir.
