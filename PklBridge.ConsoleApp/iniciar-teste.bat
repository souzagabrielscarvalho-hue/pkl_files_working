@echo off
echo ===========================================
echo    PKL Bridge - Aplicacao Console Teste
echo ===========================================
echo.
echo Compilando...
dotnet build --configuration Release
echo.
if %errorlevel% neq 0 (
    echo ❌ Erro na compilacao!
    pause
    exit /b %errorlevel%
)

echo ✅ Compilacao bem-sucedida!
echo.
echo Iniciando PKL Bridge Console...
echo.
dotnet run --configuration Release

echo.
echo ✋ PKL Bridge Console finalizado.
pause
