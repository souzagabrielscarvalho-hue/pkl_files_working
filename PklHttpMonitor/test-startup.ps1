# Script para testar inicialização do servidor PKL
Write-Host "🧪 Testando inicialização do servidor PKL..." -ForegroundColor Green
Write-Host ""

# Executar o servidor por alguns segundos para testar startup
$job = Start-Job -ScriptBlock {
    Set-Location "C:\Projetos\Andrey\Projetos\InterfacePKL\PklHttpMonitor"
    dotnet run
}

# Aguardar alguns segundos
Start-Sleep -Seconds 3

# Verificar se o job ainda está rodando (indica startup bem-sucedido)
if ($job.State -eq "Running") {
    Write-Host "✅ Servidor iniciou com sucesso!" -ForegroundColor Green
    
    # Tentar fazer uma requisição de teste
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:8080/teste" -Method GET -TimeoutSec 5
        Write-Host "✅ Servidor respondeu: $response" -ForegroundColor Green
    } catch {
        try {
            $response = Invoke-RestMethod -Uri "http://localhost:5000/teste" -Method GET -TimeoutSec 5
            Write-Host "✅ Servidor respondeu na porta 5000: $response" -ForegroundColor Green
        } catch {
            Write-Host "ℹ️ Servidor rodando, mas não conseguiu testar requisição" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "❌ Servidor falhou ao iniciar" -ForegroundColor Red
    Receive-Job $job
}

# Parar o job
Stop-Job $job
Remove-Job $job

Write-Host ""
Write-Host "📋 Para executar manualmente:" -ForegroundColor Cyan
Write-Host "   dotnet run" -ForegroundColor White
