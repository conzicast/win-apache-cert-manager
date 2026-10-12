@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Build x64 compatibile con Windows 7 SP1 e .NET Framework 4.8.
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "SOURCE=%~dp0src\CertificatiWeb\Program.cs"
set "MANIFEST=%~dp0src\CertificatiWeb\app.manifest"
set "OUTPUT_DIR=%~dp0build"
set "OUTPUT=%OUTPUT_DIR%\GestioneCertificati.exe"

if not exist "%CSC%" (
    echo ERRORE: compilatore .NET Framework x64 non trovato: "%CSC%"
    exit /b 1
)

if not exist "%SOURCE%" (
    echo ERRORE: sorgente non trovato: "%SOURCE%"
    exit /b 1
)

if not exist "%MANIFEST%" (
    echo ERRORE: manifest non trovato: "%MANIFEST%"
    exit /b 1
)

if not exist "%OUTPUT_DIR%\" mkdir "%OUTPUT_DIR%"
if errorlevel 1 exit /b 1

"%CSC%" /nologo /target:winexe /optimize+ /platform:x64 ^
    /win32manifest:"%MANIFEST%" ^
    /reference:System.dll ^
    /reference:System.Core.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Management.dll ^
    /reference:System.Xml.Linq.dll ^
    /out:"%OUTPUT%" ^
    "%SOURCE%"

if errorlevel 1 (
    echo BUILD FALLITA.
    exit /b 1
)

echo Build completata: "%OUTPUT%"
exit /b 0
