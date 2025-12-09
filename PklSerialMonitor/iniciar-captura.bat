@echo off
echo ============================================
echo     PKL SERIAL MONITOR - CAPTURA HLAB
echo ============================================
echo.
echo Configuracao atual:
echo - HLAB envia dados para: COM3
echo - Monitor escuta em: COM3
echo - Baud Rate: 19200
echo.
echo INSTRUCOES:
echo 1. Execute uma operacao no HLAB
echo 2. Os dados serao capturados automaticamente
echo 3. Pressione 'q' para parar o monitor
echo 4. Verifique os logs na pasta 'logs/'
echo.
echo Iniciando monitor em 3 segundos...
timeout /t 3 /nobreak > nul
echo.

dotnet run

echo.
echo Monitor finalizado. Verifique os arquivos de log.
pause
