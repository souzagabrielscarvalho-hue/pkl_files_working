# Descoberta Crítica - HLAB Desconecta Após EOT

## 🔍 Problema Real Identificado

Analisando o `log.txt` com atenção, descobri o **PROBLEMA REAL**:

### ❌ O que estávamos fazendo ERRADO

Tentávamos enviar frames ASTM **imediatamente** após receber o EOT do HLAB na mesma conexão TCP.

### ✅ O que o log.txt mostra que REALMENTE acontece

**O HLAB DESCONECTA após enviar EOT!**

## 📊 Sequência Real do log.txt

```
21:58:46.569 [RX] - <EOT>           ← HLAB envia EOT
21:58:51.010 [TX] - <ACK>           ← LIS envia ACK (5s depois)
21:58:53.372 [TX] - <STX>1H|...     ← LIS envia Frame 1 (H)
21:58:53.514 [RX] - <ACK>           ← HLAB responde ACK

...

21:59:09.472 [RX] - <ACK><ENQ>      ← HLAB SE RECONECTA COM ENQ!
```

## 🎯 Descoberta Crítica

1. **HLAB envia Query** (Q)
2. **HLAB envia Terminator** (L)
3. **HLAB envia EOT** e **DESCONECTA**
4. **LIS aguarda** (~5 segundos)
5. **LIS envia ACK** (para a conexão que já foi fechada?)
6. **LIS envia frames** (H, P, O) - **SEM CONEXÃO ATIVA!**
7. **HLAB SE RECONECTA** com ENQ
8. **Comunicação continua normalmente**

## 💡 Solução Necessária

O sistema deve:

1. **Receber Query do HLAB**
2. **Confirmar com ACK**
3. **Buscar exames na API**
4. **ARMAZENAR mensagem ASTM** (não enviar ainda!)
5. **AGUARDAR HLAB SE RECONECTAR** com ENQ
6. **Quando HLAB enviar ENQ:**
   - Responder ACK
   - **AGORA SIM** enviar os frames (H, P, O, L)

## 🔧 Implementação Necessária

### 1. Sistema de Mensagens Pendentes

```csharp
// Armazenar mensagens pendentes por paciente ID
private readonly ConcurrentDictionary<string, string> _pendingMessages = new();
```

### 2. Ao Receber Query

```csharp
// Buscar exames
var astmMessage = BuildAstmOrderMessage(...);

// ARMAZENAR (não enviar!)
_pendingMessages[patientId] = astmMessage;

_logger.LogInformation("📦 Mensagem ASTM armazenada para paciente {PatientId}", patientId);
_logger.LogInformation("⏳ Aguardando HLAB se reconectar com ENQ...");
```

### 3. Ao Receber ENQ (Reconexão)

```csharp
if (controlChar == 0x05) // ENQ
{
    _logger.LogInformation("🔍 DETECTADO: ENQ - HLAB reconectou!");
    
    // Verificar se há mensagem pendente
    if (_pendingMessages.TryRemove(patientId, out var pendingMessage))
    {
        _logger.LogInformation("📤 Enviando mensagem pendente para paciente {PatientId}", patientId);
        
        // AGORA SIM enviar via AstmSessionManager
        await _astmSessionManager.SendAstmMessageAsync(
            clientEndpoint,
            pendingMessage,
            isResponseToQuery: false,  // Agora é uma nova sessão!
            cancellationToken);
    }
    else
    {
        // Apenas responder ACK se não houver mensagem pendente
        await SendAstmResponse(clientEndpoint, 0x06, "ACK", "Confirmar ENQ", cancellationToken);
    }
}
```

## 📝 Mudanças Necessárias

### Arquivo: `ConsoleWorker.cs`

1. ✅ Adicionar `ConcurrentDictionary<string, string> _pendingMessages`
2. ⏳ Modificar `SendExamsForPatient()` para **ARMAZENAR** ao invés de enviar
3. ⏳ Modificar `OnTcpDataReceived()` para detectar ENQ e enviar mensagens pendentes

### Arquivo: `AstmSessionManager.cs`

1. ✅ Remover lógica de delays quando `isResponseToQuery = true`
2. ✅ Retornar `false` quando `isResponseToQuery = true` (não pode enviar!)

## 🎯 Resultado Esperado

Com essas mudanças:

1. ✅ HLAB envia Query
2. ✅ Sistema busca exames e ARMAZENA mensagem
3. ✅ HLAB desconecta (EOT)
4. ✅ Sistema AGUARDA reconexão
5. ✅ HLAB se reconecta (ENQ)
6. ✅ Sistema envia mensagem armazenada
7. ✅ HLAB recebe e processa exames!

## ⚠️ Status Atual

- ✅ Problema identificado
- ✅ Solução arquitetada
- ⏳ Implementação parcial (falta completar lógica de reconexão)
- ⏳ Teste pendente

## 🔗 Referências

- `memorybank/log.txt` - Log de referência que funciona
- `memorybank/CORRECAO-TIMING-ASTM.md` - Tentativa anterior (incorreta)
