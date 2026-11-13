@echo off
cd /d "%~dp0"
taskkill /f /im WhipCursor.exe >nul 2>&1
set "EXE=%LOCALAPPDATA%\WhipCursor\WhipCursor.exe"
if not exist "%EXE%" set "EXE=%~dp0WhipCursor.exe"
if exist "%EXE%" "%EXE%" /restaurar
echo Pronto. O cursor normal foi restaurado.
timeout /t 2 >nul
