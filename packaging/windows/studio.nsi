Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"

Name "造梦工坊"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\ZmxyStudio"
RequestExecutionLevel user
SetCompressor /SOLID lzma
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "造梦工坊"
VIAddVersionKey "FileDescription" "造梦工坊 Windows x64 安装程序"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "造梦工坊"
!define MUI_ICON "..\..\src\ZmxyDemo\app.ico"
!define UninstallKey "Software\Microsoft\Windows\CurrentVersion\Uninstall\ZmxyStudio"

!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\app\ZmxyDemo.exe"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

!macro CheckClosed
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\ZmxyDemo-Shell") p.r0'
  ${If} $0 != 0
    System::Call 'kernel32::CloseHandle(p r0)'
    MessageBox MB_OK "请先在游戏内保存并退出造梦工坊，然后重试。" /SD IDOK
    Abort
  ${EndIf}
!macroend
Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_OK "需要 64 位 Windows 10 或更高版本。" /SD IDOK
    Abort
  ${EndIf}
  SetRegView 64
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $0 < 528040
    MessageBox MB_OK "请先安装 Microsoft .NET Framework 4.8，然后重新运行安装程序。" /SD IDOK
    Abort
  ${EndIf}
  !insertmacro CheckClosed
FunctionEnd
Function un.onInit
  SetRegView 64
  !insertmacro CheckClosed
FunctionEnd

Section
  SetShellVarContext current
  !include "${MANIFEST}/install.nsh"
  FileOpen $0 "$INSTDIR\app\installed.flag" w
  FileClose $0
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateShortcut "$SMPROGRAMS\造梦工坊.lnk" "$INSTDIR\app\ZmxyDemo.exe"
  WriteRegStr HKCU "${UninstallKey}" "DisplayName" "造梦工坊"
  WriteRegStr HKCU "${UninstallKey}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UninstallKey}" "DisplayIcon" "$INSTDIR\app\ZmxyDemo.exe"
  WriteRegStr HKCU "${UninstallKey}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UninstallKey}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "${UninstallKey}" "NoModify" 1
  WriteRegDWORD HKCU "${UninstallKey}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  !include "${MANIFEST}/uninstall.nsh"
  Delete "$SMPROGRAMS\造梦工坊.lnk"
  DeleteRegKey HKCU "${UninstallKey}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
SectionEnd
