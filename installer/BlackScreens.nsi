; BlackScreens installer.
;
; Per user install: no admin prompt, no UAC, and the app registers its own "start with Windows"
; entry under HKCU, so nothing here needs machine wide rights.
;
; Build with scripts\build-installer.ps1, which passes VERSION, SOURCE_EXE and OUTFILE.

Unicode true

!ifndef VERSION
  !define VERSION "1.0.0"
!endif
!ifndef SOURCE_EXE
  !define SOURCE_EXE "..\artifacts\BlackScreens-${VERSION}-win-x64.exe"
!endif
!ifndef OUTFILE
  !define OUTFILE "..\artifacts\BlackScreens-${VERSION}-setup.exe"
!endif

!define APPNAME "BlackScreens"
!define PUBLISHER "Greg Gage"
!define DESCRIPTION "Blacks out unused monitors while a fullscreen game is running"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"
!define RUNKEY "Software\Microsoft\Windows\CurrentVersion\Run"

Name "${APPNAME}"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\${APPNAME}"
InstallDirRegKey HKCU "Software\${APPNAME}" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
ShowInstDetails hide
ShowUnInstDetails hide

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APPNAME}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "FileVersion" "${VERSION}.0"
VIAddVersionKey "FileDescription" "${APPNAME} setup"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "LegalCopyright" "Copyright (c) 2026 ${PUBLISHER}"

!include "MUI2.nsh"
!include "FileFunc.nsh"  ; ${GetSize}

!define MUI_ICON "..\src\BlackScreens\Assets\app.ico"
!define MUI_UNICON "..\src\BlackScreens\Assets\app.ico"
!define MUI_ABORTWARNING

!define MUI_FINISHPAGE_RUN "$INSTDIR\BlackScreens.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Start BlackScreens now"
!define MUI_FINISHPAGE_TEXT "BlackScreens lives in the notification area. Right click its icon for Pause, Settings and Quit."

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; Nothing can be replaced while the app holds its own exe open.
!macro StopRunningApp
  DetailPrint "Closing ${APPNAME} if it is running..."
  nsExec::Exec '"$SYSDIR\taskkill.exe" /F /IM BlackScreens.exe'
  Pop $0
  Sleep 500
!macroend

Section "Install"
  !insertmacro StopRunningApp

  SetOutPath "$INSTDIR"
  SetOverwrite on
  File /oname=BlackScreens.exe "${SOURCE_EXE}"
  File "..\LICENSE"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateShortCut "$SMPROGRAMS\${APPNAME}.lnk" "$INSTDIR\BlackScreens.exe" "" "$INSTDIR\BlackScreens.exe" 0

  WriteRegStr HKCU "Software\${APPNAME}" "InstallDir" "$INSTDIR"

  WriteRegStr HKCU "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTKEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\BlackScreens.exe"
  WriteRegStr HKCU "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegStr HKCU "${UNINSTKEY}" "Comments" "${DESCRIPTION}"
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1

  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKCU "${UNINSTKEY}" "EstimatedSize" "$0"
SectionEnd

Section "Uninstall"
  !insertmacro StopRunningApp

  Delete "$INSTDIR\BlackScreens.exe"
  Delete "$INSTDIR\LICENSE"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"

  Delete "$SMPROGRAMS\${APPNAME}.lnk"

  ; The app writes this itself when "Start with Windows" is on.
  DeleteRegValue HKCU "${RUNKEY}" "${APPNAME}"
  DeleteRegKey HKCU "${UNINSTKEY}"
  DeleteRegKey HKCU "Software\${APPNAME}"

  ; Settings and the error log are left alone on purpose so a reinstall keeps them. Offer to clear.
  IfSilent +6
  MessageBox MB_YESNO|MB_ICONQUESTION "Remove your BlackScreens settings and error log as well?" IDNO +5
  Delete "$LOCALAPPDATA\${APPNAME}\settings.json"
  Delete "$LOCALAPPDATA\${APPNAME}\error.log"
  Delete "$LOCALAPPDATA\${APPNAME}\error.log.1"
  RMDir "$LOCALAPPDATA\${APPNAME}"
SectionEnd

Function .onInit
  ReadRegStr $0 HKCU "${UNINSTKEY}" "DisplayVersion"
  StrCmp $0 "" done
  StrCmp $0 "${VERSION}" done
  DetailPrint "Upgrading ${APPNAME} $0 to ${VERSION}"
  done:
FunctionEnd
