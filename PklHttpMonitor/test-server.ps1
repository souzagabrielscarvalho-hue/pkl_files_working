# Script para testar o servidor PKL HTTP Monitor
Write-Host "🧪 Testando Servidor PKL HTTP Monitor..." -ForegroundColor Green
Write-Host ""

# Testar se o servidor responde
try {
    $response = Invoke-RestMethod -Uri "http://localhost:8080/teste" -Method POST -Body "Mensagem de teste PKL" -ContentType "text/plain" -TimeoutSec 10
    Write-Host "✅ Servidor respondeu: $response" -ForegroundColor Green
} catch {
    if ($_.Exception.Message -like "*No connection could be made*") {
        Write-Host "❌ Servidor não está rodando na porta 8080" -ForegroundColor Red
        Write-Host "💡 Execute: dotnet run" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Erro: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "📋 Para testar manualmente:" -ForegroundColor Cyan
Write-Host "   1. Execute: dotnet run" -ForegroundColor White
Write-Host "   2. Em outra janela execute: .\test-server.ps1" -ForegroundColor White
Write-Host "   3. Verifique os logs em: logs/pkl-request-*.txt" -ForegroundColor White
