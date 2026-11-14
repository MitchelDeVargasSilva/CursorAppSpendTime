@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
taskkill /f /im WhipCursor.exe >nul 2>&1
set "FILES="
for %%f in (src\*.cs) do if /i not "%%~nxf"=="Tests.cs" set FILES=!FILES! "%%f"
"%CSC%" /nologo /codepage:65001 /target:winexe /optimize /out:WhipCursor.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll !FILES!
if errorlevel 1 (pause & exit /b 1)
start "" WhipCursor.exe
