@echo off
REM Inicia o PKL Bridge em modo console (testes / desenvolvimento)
REM Equivalente a: dotnet run --project PklBridge.ConsoleApp
cd /d "%~dp0"
echo.
echo === PKL Bridge - Console (modo teste) ===
echo.
dotnet run -c Release
if errorlevel 1 (
    echo.
    echo [ERRO] Falha ao executar. Verifique se o .NET 8 SDK esta instalado.
    pause
    exit /b 1
)
pause
