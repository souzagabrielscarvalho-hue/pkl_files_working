# Solução Final - Race Condition HLAB

## 🎯 Problema Identificado

### ❌ Race Condition Crítica

O sistema tinha uma race condition onde o EOT do HLAB chegava **ANTES** da mensagem ser armazenada na fila de mensagens pendentes.

### 📊 Timing do Problema

```
T+0ms:   HLAB envia Query
T+10ms:  Sistema envia ACK
T+10ms:  Sistema inicia Task.Delay(500ms) ← PROBLEMA!
T+100ms: HLAB envia EOT ← Chegou rápido!
T+100ms: Sistema verifica fila = 0 mensagens ❌
T+510ms: Sistema busca exames
T+560ms: Sistema armazena mensagem ← Tarde demais!
T+5000ms: HLAB reconecta com ENQ
T+5010ms: Sistema verifica fila = 1 mensagem ✅ Mas não envia porque já passou!
```

## ✅ Solução Implementada

### 1. Remoção do Delay Desnecessário

**Arquivo:** `PklBridge.ConsoleApp/ConsoleWorker.cs`

**Antes:**
```csharp
await SendAstmResponse(clientEndpoint, 0x06, "ACK", "Confirmar recebimento Query", cancellationToken);

// Aguardar um pouco antes de enviar a resposta
await Task.Delay(500, cancellationToken); // ← REMOVIDO!

// Buscar e enviar exames
await SendExamsForPatient(patientId, clientEndpoint, cancellationToken);
```

**Depois:**
```csharp
await SendAstmResponse(clientEndpoint, 0x06, "ACK", "Confirmar recebimento Query", cancellationToken);

// Buscar e armazenar exames IMEDIATAMENTE (sem delay!)
// O EOT vai chegar rápido, então precisamos armazenar antes!
await SendExamsForPatient(patientId, clientEndpoint, cancellationToken);
```

### 2. Novo Timing Correto

```
T+0ms:   HLAB envia Query
T+10ms:  Sistema envia ACK
T+10ms:  Sistema busca exames IMEDIATAMENTE ✅
T+60ms:  Sistema armazena mensagem ✅
T+100ms: HLAB envia EOT
T+100ms: Sistema verifica fila = 1 mensagem ✅
T+5000ms: HLAB reconecta com ENQ
T+5010ms: Sistema detecta 1 mensagem pendente
T+5010ms: Sistema envia ACK para ENQ
T+5510ms: Sistema inicia envio dos frames
```

## 🔧 Componentes da Solução

### Sistema de Fila de Mensagens

```csharp
private readonly ConcurrentDictionary<string, string> _pendingMessages = new();
```

**Por que ConcurrentDictionary?**
- Thread-safe para acesso concorrente
- Key = Patient ID (suporta múltiplas queries simultâneas)
- Value = Mensagem ASTM completa (pronta para envio)

### Detecção de EOT com Logging

```csharp
if (controlChar == 0x04) // EOT
{
    _logger.LogInformation("🔍 DETECTADO: EOT (End of Transmission) - HLAB finalizou e vai desconectar");
    _logger.LogInformation("⏳ Aguardando HLAB se reconectar com ENQ para enviar mensagens pendentes ({Count})...", 
        _pendingMessages.Count);
    return;
}
```

### Detecção de Reconexão e Envio

```csharp
if (controlChar == 0x05) // ENQ
{
    _logger.LogInformation("🔍 DETECTADO: ENQ (Enquiry) - HLAB reconectou!");
    
    if (_pendingMessages.Count > 0)
    {
        _logger.LogInformation("📦 DETECTADO {Count} mensagem(ns) pendente(s)!", _pendingMessages.Count);
        
        var firstPending = _pendingMessages.First();
        var patientId = firstPending.Key;
        var astmMessage = firstPending.Value;
        
        _logger.LogInformation("📤 Enviando mensagem pendente para paciente {PatientId}", patientId);
        
        await _tcpServer.SendToClientAsync(e.ClientEndpoint, new byte[] { 0x06 }, CancellationToken.None);
        _logger.LogInformation("✅ ACK enviado para ENQ");
        
        await Task.Delay(500); // Delay SÓ AQUI é OK
        
        var success = await _astmSessionManager.SendAstmMessageAsync(
            e.ClientEndpoint,
            astmMessage,
            isResponseToQuery: false,  // Nova sessão!
            CancellationToken.None);
        
        if (success)
        {
            _logger.LogInformation("✅ Mensagem enviada com sucesso para paciente {PatientId}!", patientId);
            _pendingMessages.TryRemove(patientId, out _);
        }
        else
        {
            _logger.LogError("❌ Falha ao enviar mensagem para paciente {PatientId}", patientId);
        }
    }
    else
    {
        _logger.LogInformation("📭 Nenhuma mensagem pendente - apenas respondendo ACK");
        await _tcpServer.SendToClientAsync(e.ClientEndpoint, new byte[] { 0x06 }, CancellationToken.None);
        _logger.LogInformation("✅ ACK enviado para ENQ");
    }
    
    return;
}
```

## 📝 Logs Esperados

### Sequência Correta:

```
[06:XX:XX] 🔍 DETECTADO: QUERY (Q) - HLAB solicitando exames!
[06:XX:XX] 🔍 ID do Paciente extraído: 50
[06:XX:XX] 📤 ENVIANDO RESPOSTA ASTM: ACK (06)
[06:XX:XX] ✅ Resposta ASTM ACK ENVIADA
[06:XX:XX] 🔍 Buscando exames para paciente 50...
[06:XX:XX] ✅ Paciente encontrado: Paciente Cincuenta
[06:XX:XX] 📋 Encontrados 10 exames: GLICOSE, CREAT, ...
[06:XX:XX] 📦 ARMAZENANDO mensagem ASTM para paciente 50
[06:XX:XX] ✅ Mensagem armazenada! Total de mensagens pendentes: 1
[06:XX:XX] 🔍 DETECTADO: EOT - HLAB finalizou e vai desconectar
[06:XX:XX] ⏳ Aguardando HLAB se reconectar com ENQ... (1)  ← 1 mensagem!
...
[06:XX:XX] 🔍 DETECTADO: ENQ (Enquiry) - HLAB reconectou!
[06:XX:XX] 📦 DETECTADO 1 mensagem(ns) pendente(s)!
[06:XX:XX] 📤 Enviando mensagem pendente para paciente 50
[06:XX:XX] ✅ ACK enviado para ENQ
[06:XX:XX] 📤 Enviando frame 1...
[06:XX:XX] ✅ ACK recebido para frame 1
[06:XX:XX] 📤 Enviando frame 2...
[06:XX:XX] ✅ ACK recebido para frame 2
[06:XX:XX] 📤 Enviando EOT...
[06:XX:XX] ✅ Sessão ASTM concluída - 2 frames enviados
[06:XX:XX] ✅ Mensagem enviada com sucesso para paciente 50!
```

## 🧪 Como Testar

1. **Parar processo anterior** (se rodando)
2. **Compilar**: `dotnet build InterfacePKL.sln` ✅ (Feito!)
3. **Executar**: `dotnet run --project PklBridge.ConsoleApp`
4. **No HLAB**: Solicitar exames para ID 50
5. **Observar logs**: Procurar pela sequência acima

## ✅ Critérios de Sucesso

- [x] Build sem erros
- [ ] EOT mostra 1 mensagem pendente (não 0)
- [ ] ENQ detecta 1 mensagem pendente
- [ ] Frames são enviados após ENQ
- [ ] HLAB exibe exames na tela

## 🔗 Arquivos Modificados

1. `PklBridge.ConsoleApp/ConsoleWorker.cs`
   - Removido `Task.Delay(500)` após ACK
   - Adicionado log detalhado de EOT
   - Implementado detecção e envio em ENQ

2. `PklBridge.Infrastructure/Serial/AstmSessionManager.cs`
   - Retorna `false` quando `isResponseToQuery = true`
   - Implementado delays entre frames (3-6s)

## 📚 Documentos Relacionados

- `memorybank/DESCOBERTA-CRITICA-RECONEXAO.md` - Descoberta inicial do problema
- `memorybank/CORRECAO-TIMING-ASTM.md` - Tentativa anterior (incorreta)
- `memorybank/log.txt` - Log de referência que funciona
