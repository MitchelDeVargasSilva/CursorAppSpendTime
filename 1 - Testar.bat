@echo off
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
taskkill /f /im WhipCursor.exe >nul 2>&1
echo Compilando...
"%CSC%" /nologo /codepage:65001 /target:winexe /optimize /out:WhipCursor.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll Whip.cs
if errorlevel 1 goto erro
start "" WhipCursor.exe
exit /b 0
:erro
echo.
echo Erro ao compilar. Copie a mensagem acima e envie para corrigir.
pause
exit /b 1
