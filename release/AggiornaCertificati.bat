@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Avvia il controllo e il rinnovo di tutti i certificati HTTPS configurati.
rem Per l'uso pianificato, eseguire questa procedura con privilegi elevati.

set "APP=%~dp0GestioneCertificati.exe"

if not exist "%APP%" (
    echo ERRORE: GestioneCertificati.exe non trovato accanto al file batch.
    exit /b 1
)

echo Avvio controllo periodico dei certificati...
start "" /wait "%APP%" --renew-all
set "RESULT=%ERRORLEVEL%"

if "%RESULT%"=="0" (
    echo Controllo completato correttamente.
) else (
    echo Controllo concluso con errori. Consultare C:\server\ssl-le64\log.
)

exit /b %RESULT%
