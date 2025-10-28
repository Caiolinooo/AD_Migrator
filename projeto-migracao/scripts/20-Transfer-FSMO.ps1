param(
    [Parameter(Mandatory=$true)][string]$NewDC
)

Import-Module ActiveDirectory -ErrorAction Stop

# Move all five FSMO roles to the specified DC
Move-ADDirectoryServerOperationMasterRole -Identity $NewDC -OperationMasterRole SchemaMaster, DomainNamingMaster, PDCEmulator, RIDMaster, InfrastructureMaster -Force

Write-Host "FSMO transferido para $NewDC" -ForegroundColor Green
