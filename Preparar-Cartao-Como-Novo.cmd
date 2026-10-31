@echo off
setlocal
chcp 65001 >nul
title Obsidian OS - preparar cartao como novo
set "SCRIPT=D:\R36S-sistema-novo\08-obsidian-installer\tools\format-single-partition.ps1"
set "DISK=2"
set "SERIAL=150101v01"

echo.
echo   OBSIDIAN OS - PREPARAR CARTAO COMO NOVO
echo   ---------------------------------------
echo   Disco alvo : %DISK%  (serie %SERIAL%)
echo   Resultado  : tabela nova + UMA particao FAT32 + formatacao rapida
echo.
echo   Tudo neste cartao sera apagado.
echo.
pause

net session >nul 2>&1
if %errorlevel%==0 goto :run

echo   Pedindo autorizacao de administrador...
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
exit /b

:run
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" -DiskNumber %DISK% -ExpectedSerial %SERIAL% -Label SDCARD -FileSystem fat32 -LogPath "D:\dsh-temp\format-single.log" -ReportPath "D:\dsh-temp\format-single.json"
echo.
echo   Relatorio: D:\dsh-temp\format-single.json
pause
