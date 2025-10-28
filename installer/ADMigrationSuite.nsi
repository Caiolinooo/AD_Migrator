; AD Migration Suite - NSIS Installer Script
; Professional installer with component selection

!define PRODUCT_NAME "AD Migration Suite"
!define PRODUCT_VERSION "1.0.0"
!define PRODUCT_PUBLISHER "AD Migration Suite Team"
!define PRODUCT_WEB_SITE "https://admigration.example.com"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\MigracaoAD.UI.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"

; Modern UI
!include "MUI2.nsh"
!include "Sections.nsh"
!include "LogicLib.nsh"

; MUI Settings
!define MUI_ABORTWARNING
!define MUI_ICON "${NSISDIR}\Contrib\Graphics\Icons\modern-install.ico"
!define MUI_UNICON "${NSISDIR}\Contrib\Graphics\Icons\modern-uninstall.ico"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "${NSISDIR}\Contrib\Graphics\Header\nsis.bmp"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${NSISDIR}\Contrib\Graphics\Wizard\win.bmp"

; Welcome page
!insertmacro MUI_PAGE_WELCOME

; License page
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"

; Components page
!insertmacro MUI_PAGE_COMPONENTS

; Directory page
!insertmacro MUI_PAGE_DIRECTORY

; Custom page for Agent configuration
Page custom AgentConfigPage AgentConfigLeave

; Instfiles page
!insertmacro MUI_PAGE_INSTFILES

; Finish page
!define MUI_FINISHPAGE_RUN "$INSTDIR\Manager\MigracaoAD.UI.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch AD Migration Suite Manager"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\Documentation\README.md"
!define MUI_FINISHPAGE_SHOWREADME_TEXT "View Documentation"
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
!insertmacro MUI_PAGE_FINISH

; Uninstaller pages
!insertmacro MUI_UNPAGE_INSTFILES

; Language
!insertmacro MUI_LANGUAGE "English"

; Variables
Var AgentToken
Var AgentPort
Var LaunchManager

; Installer attributes
Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "output\ADMigrationSuite-${PRODUCT_VERSION}-Setup.exe"
InstallDir "$PROGRAMFILES64\AD Migration Suite"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show
RequestExecutionLevel admin

; Version Information
VIProductVersion "${PRODUCT_VERSION}.0"
VIAddVersionKey "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey "CompanyName" "${PRODUCT_PUBLISHER}"
VIAddVersionKey "LegalCopyright" "Copyright © 2025 ${PRODUCT_PUBLISHER}"
VIAddVersionKey "FileDescription" "${PRODUCT_NAME} Installer"
VIAddVersionKey "FileVersion" "${PRODUCT_VERSION}"
VIAddVersionKey "ProductVersion" "${PRODUCT_VERSION}"

; Sections
SectionGroup /e "Components" SecGroup

Section "Management Console" SecManager
  SectionIn RO
  SetOutPath "$INSTDIR\Manager"
  
  ; Copy Manager files
  File /r "..\ui-wpf\bin\Release\net8.0-windows\*.*"
  
  ; Create shortcuts
  CreateDirectory "$SMPROGRAMS\${PRODUCT_NAME}"
  CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\AD Migration Suite.lnk" "$INSTDIR\Manager\MigracaoAD.UI.exe"
  CreateShortCut "$DESKTOP\AD Migration Suite.lnk" "$INSTDIR\Manager\MigracaoAD.UI.exe"
  
  ; Registry
  WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\Manager\MigracaoAD.UI.exe"
SectionEnd

Section "Agent Service" SecAgent
  SetOutPath "$INSTDIR\Agent"
  
  ; Copy Agent files
  File "..\agent\publish\MigracaoAD.Agent.exe"
  File "..\agent\install-agent.ps1"
  File "..\agent\uninstall-agent.ps1"
  
  ; Set environment variable
  WriteRegStr HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "AGENT_TOKEN" "$AgentToken"
  
  ; Configure firewall
  DetailPrint "Configuring firewall..."
  nsExec::ExecToLog 'powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "New-NetFirewallRule -DisplayName \"AD Migration Agent\" -Direction Inbound -Protocol TCP -LocalPort $AgentPort -Action Allow -ErrorAction SilentlyContinue"'
  
  ; Install and start service
  DetailPrint "Installing Agent service..."
  nsExec::ExecToLog '"$INSTDIR\Agent\MigracaoAD.Agent.exe" install'
  Sleep 2000
  
  DetailPrint "Starting Agent service..."
  nsExec::ExecToLog '"$INSTDIR\Agent\MigracaoAD.Agent.exe" start'
  
  DetailPrint "Agent service installed and started on port $AgentPort"
SectionEnd

Section "Documentation" SecDocs
  SetOutPath "$INSTDIR\Documentation"
  
  ; Copy documentation
  File "..\README.md"
  File "..\CHANGELOG.md"
  File "..\README_AGENTE.md"
  File "..\COMO_FUNCIONA_AGENTE.md"
  File "..\agent\INSTALACAO_RAPIDA.md"
  
  ; Create documentation shortcut
  CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\Documentation.lnk" "$INSTDIR\Documentation"
SectionEnd

SectionGroupEnd

; Section descriptions
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecManager} "WPF application for managing AD migrations. Install this on your management workstation."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecAgent} "Windows Service agent for remote management. Install this on source and target servers."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDocs} "User guides, API documentation, and installation instructions."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; Custom page for Agent configuration
Function AgentConfigPage
  ${If} ${SectionIsSelected} ${SecAgent}
    nsDialogs::Create 1018
    Pop $0
    
    ${NSD_CreateLabel} 0 0 100% 12u "Agent Configuration"
    Pop $0
    
    ${NSD_CreateLabel} 0 20u 100% 12u "Enter the authentication token for the agent:"
    Pop $0
    
    ${NSD_CreateText} 0 35u 100% 12u "default-token-change-me"
    Pop $AgentToken
    
    ${NSD_CreateLabel} 0 55u 100% 12u "Enter the port for the agent (default: 8765):"
    Pop $0
    
    ${NSD_CreateText} 0 70u 100% 12u "8765"
    Pop $AgentPort
    
    nsDialogs::Show
  ${EndIf}
FunctionEnd

Function AgentConfigLeave
  ${If} ${SectionIsSelected} ${SecAgent}
    ${NSD_GetText} $AgentToken $AgentToken
    ${NSD_GetText} $AgentPort $AgentPort
  ${EndIf}
FunctionEnd

; Installer initialization
Function .onInit
  ; Check if running as admin
  UserInfo::GetAccountType
  Pop $0
  ${If} $0 != "admin"
    MessageBox MB_ICONSTOP "Administrator rights required!"
    SetErrorLevel 740
    Quit
  ${EndIf}
  
  ; Set default values
  StrCpy $AgentToken "default-token-change-me"
  StrCpy $AgentPort "8765"
  StrCpy $LaunchManager "1"
FunctionEnd

; Post-installation
Section -Post
  WriteUninstaller "$INSTDIR\uninst.exe"
  WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\Manager\MigracaoAD.UI.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninst.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\Manager\MigracaoAD.UI.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
  
  CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\Uninstall.lnk" "$INSTDIR\uninst.exe"
SectionEnd

; Uninstaller
Section Uninstall
  ; Stop and remove Agent service
  DetailPrint "Stopping Agent service..."
  nsExec::ExecToLog '"$INSTDIR\Agent\MigracaoAD.Agent.exe" stop'
  Sleep 2000
  
  DetailPrint "Uninstalling Agent service..."
  nsExec::ExecToLog '"$INSTDIR\Agent\MigracaoAD.Agent.exe" uninstall'
  Sleep 2000
  
  ; Remove firewall rule
  DetailPrint "Removing firewall rule..."
  nsExec::ExecToLog 'powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Remove-NetFirewallRule -DisplayName \"AD Migration Agent\" -ErrorAction SilentlyContinue"'
  
  ; Remove files
  Delete "$INSTDIR\uninst.exe"
  RMDir /r "$INSTDIR\Manager"
  RMDir /r "$INSTDIR\Agent"
  RMDir /r "$INSTDIR\Documentation"
  RMDir "$INSTDIR"
  
  ; Remove shortcuts
  Delete "$SMPROGRAMS\${PRODUCT_NAME}\*.*"
  RMDir "$SMPROGRAMS\${PRODUCT_NAME}"
  Delete "$DESKTOP\AD Migration Suite.lnk"
  
  ; Remove registry
  DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
  DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"
  DeleteRegValue HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "AGENT_TOKEN"
  
  SetAutoClose true
SectionEnd

