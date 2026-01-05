; Zarla Browser NSIS Installer Script
; Requires NSIS 3.x - https://nsis.sourceforge.io/
;
; HOW TO BUILD:
; 1. First run: .\build.ps1 -Release
; 2. Then run: makensis installer\zarla-installer.nsi
; Or just run: .\build.ps1 -Installer

!include "MUI2.nsh"
!include "FileFunc.nsh"

; Basic Info
!define PRODUCT_NAME "Zarla Browser"
!define PRODUCT_VERSION "1.0.0"
!define PRODUCT_PUBLISHER "Zarla"
!define PRODUCT_WEB_SITE "https://github.com/xlelord9292/Zarla-Browser"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\Zarla.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKCU"

; MUI Settings
!define MUI_ABORTWARNING
; Icon will use default NSIS icon if zarla.ico doesn't exist
!if /FileExists "..\assets\icons\zarla.ico"
    !define MUI_ICON "..\assets\icons\zarla.ico"
    !define MUI_UNICON "..\assets\icons\zarla.ico"
!endif

; Welcome page
!insertmacro MUI_PAGE_WELCOME

; License page
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"

; Directory page
!insertmacro MUI_PAGE_DIRECTORY

; Instfiles page
!insertmacro MUI_PAGE_INSTFILES

; Finish page
!define MUI_FINISHPAGE_RUN "$INSTDIR\Zarla.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch Zarla Browser"
!insertmacro MUI_PAGE_FINISH

; Uninstaller pages
!insertmacro MUI_UNPAGE_INSTFILES

; Language files
!insertmacro MUI_LANGUAGE "English"

; Installer Info
Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "ZarlaSetup-${PRODUCT_VERSION}.exe"
InstallDir "$LOCALAPPDATA\Zarla"
InstallDirRegKey HKCU "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show

; No admin required - install to user folder
RequestExecutionLevel user

Section "MainSection" SEC01
    SetOutPath "$INSTDIR"
    SetOverwrite on

    ; Copy all files from publish\win-x64 folder
    File /r "..\publish\win-x64\*.*"

    ; Create shortcuts
    CreateDirectory "$SMPROGRAMS\Zarla"
    CreateShortCut "$SMPROGRAMS\Zarla\Zarla Browser.lnk" "$INSTDIR\Zarla.exe"
    CreateShortCut "$DESKTOP\Zarla Browser.lnk" "$INSTDIR\Zarla.exe"

    ; Register as default browser option (user-level)
    WriteRegStr HKCU "SOFTWARE\RegisteredApplications" "Zarla" "Software\Zarla\Capabilities"
    WriteRegStr HKCU "SOFTWARE\Zarla\Capabilities" "ApplicationName" "Zarla Browser"
    WriteRegStr HKCU "SOFTWARE\Zarla\Capabilities" "ApplicationDescription" "A fast, lightweight, privacy-focused web browser"
    WriteRegStr HKCU "SOFTWARE\Zarla\Capabilities\URLAssociations" "http" "ZarlaHTML"
    WriteRegStr HKCU "SOFTWARE\Zarla\Capabilities\URLAssociations" "https" "ZarlaHTML"
    WriteRegStr HKCU "SOFTWARE\Zarla\Capabilities\FileAssociations" ".htm" "ZarlaHTML"
    WriteRegStr HKCU "SOFTWARE\Zarla\Capabilities\FileAssociations" ".html" "ZarlaHTML"

    ; File associations (user-level via HKCU\Software\Classes)
    WriteRegStr HKCU "Software\Classes\ZarlaHTML" "" "Zarla HTML Document"
    WriteRegStr HKCU "Software\Classes\ZarlaHTML\DefaultIcon" "" "$INSTDIR\Zarla.exe,0"
    WriteRegStr HKCU "Software\Classes\ZarlaHTML\shell\open\command" "" '"$INSTDIR\Zarla.exe" "%1"'

    ; URL protocol handlers (user-level)
    WriteRegStr HKCU "Software\Classes\zarla" "" "URL:Zarla Protocol"
    WriteRegStr HKCU "Software\Classes\zarla" "URL Protocol" ""
    WriteRegStr HKCU "Software\Classes\zarla\DefaultIcon" "" "$INSTDIR\Zarla.exe,0"
    WriteRegStr HKCU "Software\Classes\zarla\shell\open\command" "" '"$INSTDIR\Zarla.exe" "%1"'
SectionEnd

Section -Post
    WriteUninstaller "$INSTDIR\uninst.exe"
    WriteRegStr HKCU "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\Zarla.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninst.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\Zarla.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"

    ; Get installed size
    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    IntFmt $0 "0x%08X" $0
    WriteRegDWORD ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "EstimatedSize" "$0"
SectionEnd

Section Uninstall
    ; Remove shortcuts
    Delete "$SMPROGRAMS\Zarla\Zarla Browser.lnk"
    Delete "$DESKTOP\Zarla Browser.lnk"
    RMDir "$SMPROGRAMS\Zarla"

    ; Remove installation directory
    RMDir /r "$INSTDIR"

    ; Remove registry entries (user-level)
    DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
    DeleteRegKey HKCU "${PRODUCT_DIR_REGKEY}"
    DeleteRegKey HKCU "SOFTWARE\Zarla"
    DeleteRegValue HKCU "SOFTWARE\RegisteredApplications" "Zarla"
    DeleteRegKey HKCU "Software\Classes\ZarlaHTML"
    DeleteRegKey HKCU "Software\Classes\zarla"

    ; Remove user data (optional - ask user)
    MessageBox MB_YESNO "Do you want to remove your Zarla browsing data (history, bookmarks, settings)?" IDNO skip_userdata
        RMDir /r "$LOCALAPPDATA\Zarla"
    skip_userdata:

    SetAutoClose true
SectionEnd
