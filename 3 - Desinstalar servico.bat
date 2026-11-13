@echo off
set "DEST=%LOCALAPPDATA%\WhipCursor"
taskkill /f /im WhipCursor.exe >nul 2>&1
if exist "%DEST%\WhipCursor.exe" "%DEST%\WhipCursor.exe" /restaurar
timeout /t 1 >nul
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v WhipCursor /f >nul 2>&1
reg delete "HKCU\Software\WhipCursor" /f >nul 2>&1
if exist "%DEST%" rmdir /s /q "%DEST%"
echo Desinstalado. O cursor normal foi restaurado.
pause
