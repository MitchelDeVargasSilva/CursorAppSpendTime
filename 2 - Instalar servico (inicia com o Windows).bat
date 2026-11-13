@echo off
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "DEST=%LOCALAPPDATA%\WhipCursor"
taskkill /f /im WhipCursor.exe >nul 2>&1
if not exist "%DEST%" mkdir "%DEST%"
echo Compilando e instalando...
"%CSC%" /nologo /codepage:65001 /target:winexe /optimize /out:"%DEST%\WhipCursor.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll Whip.cs
if errorlevel 1 goto erro
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v WhipCursor /t REG_SZ /d "\"%DEST%\WhipCursor.exe\" /boot" /f >nul
start "" "%DEST%\WhipCursor.exe"
echo.
echo Instalado em %DEST%
echo O Whip Cursor inicia sozinho com o Windows, mas fica DESATIVADO.
echo Use Ctrl+Alt+W para ligar.
pause
exit /b 0
:erro
echo.
echo Erro ao compilar. Copie a mensagem acima e envie para corrigir.
pause
exit /b 1
