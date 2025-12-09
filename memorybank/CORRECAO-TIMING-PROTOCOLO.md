# Correção de Timing no Protocolo ASTM - HLAB Query/Order

**Data**: 07/11/2025
**Status**: ✅ IMPLEMENTADO E COMPILADO

## 🔍 Problema Identificado

### Sintomas
- HLAB envia Query para solicitar exames
- PKL Bridge recebe Query e responde ACK corretamente
- PKL Bridge tenta enviar Order imediatamente após Query
- HLAB envia EOT (End of Transmission) porque não está esperando receber dados
- Order não chega ao equipamento PKL 125

### Logs do Problema
```
[INF] 🔍 DETECTADO: QUERY (Q) - HLAB solicitando exames!
[INF] 🔍 ID do Paciente extraído: 50
[INF] 📤 ENVIANDO RESPOSTA ASTM: ACK (06) - Confirmar recebimento Query
[INF] ✅ Resposta ASTM ACK ENVIADA com sucesso!
[INF] 🚀 Iniciando envio de mensagem ASTM via protocolo completo...
[INF] 📤 Enviando frame 1 (tentativa 1/3): 48 bytes
[INF] 📨 Dados recebidos: 1 bytes - EOT  <-- HLAB DESCONECTA!
[WRN] ⚠️ Timeout ou resposta inválida para frame 1
```

## 📋 Causa Raiz

De acordo com o protocolo PPC 125 (ASTM E1394-97):

### Fluxo Correto para Query/Order:

1. **Sessão Query (HLAB = Transmissor)**
   ```
   HLAB → ENQ
   PKL Bridge → ACK
   HLAB → Frame 1 (Header)
   PKL Bridge → ACK
   HLAB → Frame 2 (Query Q)
   PKL Bridge → ACK
   HLAB → Frame 3 (Terminator L)
   PKL Bridge → ACK
   HLAB → EOT (finaliza e desconecta)
   ```

2. **HLAB SE DESCONECTA**

3. **Sessão Order (PKL Bridge = Transmissor)**
   ```
   HLAB → ENQ (reconecta para nova sessão)
   PKL Bridge → ACK
   PKL Bridge → Frame 1 (Header)
   HLAB → ACK
   PKL Bridge → Frame 2 (Patient)
   HLAB → ACK
   PKL Bridge → Frame 3 (Order)
   HLAB → ACK
   PKL Bridge → EOT
   ```

### O que estava ERRADO:
- PKL Bridge tentava enviar Order **IMEDIATAMENTE** após receber Query
- HLAB ainda estava finalizando a transmissão da Query com EOT
- HLAB não estava pronto para **RECEBER** dados (estava no papel de **TRANSMISSOR**)

## ✅ Solução Implementada

### 1. Sistema de Fila de Mensagens Pendentes

Criado `ConcurrentDictionary<string, string> _pendingMessages` para armazenar mensagens que precisam ser enviadas quando o HLAB reconectar.

### 2. Novo Método `QueueExamsForPatient`

Em vez de enviar imediatamente, a mensagem é:
1. Construída com base nos dados da API VIDA
2. **Adicionada à fila** `_pendingMessages[patientId] = astmMessage`
3. Log informa que está aguardando reconexão

```csharp
private async Task QueueExamsForPatient(string patientId, string clientEndpoint, CancellationToken cancellationToken)
{
    // Buscar dados do paciente e exames
    var astmMessage = BuildAstmOrderMessage(patientId, patient, order);
    
    // ADICIONAR À FILA em vez de enviar
    _pendingMessages[patientId] = astmMessage;
    
    _logger.LogInformation("📦 ✅ Mensagem para paciente {PatientId} ADICIONADA À FILA!", patientId);
    _logger.LogInformation("⏳ Aguardando HLAB finalizar com EOT e reconectar com ENQ para enviar...");
}
```

### 3. Modificação no Tratamento de Query

Quando Query é detectada:

**ANTES:**
```csharp
// Enviar ACK primeiro
await SendAstmResponse(clientEndpoint, 0x06, "ACK", "Confirmar recebimento Query", cancellationToken);

// Buscar e enviar exames IMEDIATAMENTE (ERRADO!)
await SendExamsForPatient(patientId, clientEndpoint, cancellationToken);
```

**DEPOIS:**
```csharp
// Enviar ACK primeiro
await SendAstmResponse(clientEndpoint, 0x06, "ACK", "Confirmar recebimento Query", cancellationToken);

// CORREÇÃO: Adicionar à fila e aguardar reconexão
await QueueExamsForPatient(patientId, clientEndpoint, cancellationToken);
```

### 4. Tratamento de ENQ para Mensagens Pendentes

Quando ENQ é detectado (HLAB reconectando):

```csharp
if (controlChar == 0x05) // ENQ
{
    _logger.LogInformation("🔍 DETECTADO: ENQ (Enquiry) - HLAB reconectou!");
    
    // Verificar se há mensagens pendentes
    if (_pendingMessages.Count > 0)
    {
        var firstPending = _pendingMessages.First();
        var patientId = firstPending.Key;
        var astmMessage = firstPending.Value;
        
        // Responder ACK ao ENQ
        await _tcpServer.SendToClientAsync(e.ClientEndpoint, new byte[] { 0x06 }, CancellationToken.None);
        
        // Aguardar preparação
        await Task.Delay(500);
        
        // ENVIAR mensagem (agora o HLAB está pronto!)
        var success = await _astmSessionManager.SendAstmMessageAsync(
            e.ClientEndpoint,
            astmMessage,
            isResponseToQuery: false,  // Nova sessão após ENQ
            CancellationToken.None);
        
        if (success)
        {
            // Remover da fila
            _pendingMessages.TryRemove(patientId, out _);
        }
    }
    else
    {
        // Sem mensagens pendentes, apenas responder ACK
        await _tcpServer.SendToClientAsync(e.ClientEndpoint, new byte[] { 0x06 }, CancellationToken.None);
    }
    
    return;
}
```

### 5. Tratamento de EOT

Quando EOT é detectado após Query:

```csharp
if (controlChar == 0x04) // EOT
{
    _logger.LogInformation("🔍 DETECTADO: EOT (End of Transmission) - HLAB finalizou e vai desconectar");
    _logger.LogInformation("⏳ Aguardando HLAB se reconectar com ENQ para enviar mensagens pendentes ({Count})...", _pendingMessages.Count);
    return;
}
```

## 🔄 Fluxo Corrigido

### 1. HLAB Envia Query
```
HLAB → ENQ
PKL Bridge → ACK
HLAB → Header Frame
PKL Bridge → ACK
HLAB → Query Frame (Q|1|^50|...)
PKL Bridge → ACK
HLAB → Terminator Frame
PKL Bridge → ACK
HLAB → EOT
```

**PKL Bridge ações:**
- ✅ Recebe Query
- ✅ Extrai Patient ID (50)
- ✅ Busca exames na API VIDA
- ✅ Constrói mensagem ASTM Order
- ✅ **ADICIONA À FILA** `_pendingMessages["50"] = astmMessage`
- ✅ **AGUARDA** HLAB reconectar

### 2. HLAB Reconecta para Receber Order
```
HLAB → ENQ (reconexão)
PKL Bridge → ACK
PKL Bridge → Header Frame
HLAB → ACK
PKL Bridge → Patient Frame
HLAB → ACK
PKL Bridge → Order Frame
HLAB → ACK
PKL Bridge → EOT
```

**PKL Bridge ações:**
- ✅ Detecta ENQ
- ✅ Verifica fila: `_pendingMessages.Count > 0`
- ✅ Pega mensagem pendente para paciente "50"
- ✅ Responde ACK ao ENQ
- ✅ **ENVIA** mensagem via `AstmSessionManager.SendAstmMessageAsync`
- ✅ Remove da fila após sucesso

## 📊 Mudanças nos Arquivos

### ConsoleWorker.cs

**Adições:**
- Método `QueueExamsForPatient` (linhas ~510-550)

**Modificações:**
- `ProcessTcpDataAsync` - Query detection (linha ~497)
- `OnTcpDataReceived` - ENQ handling (linhas ~367-407)

**Linhas alteradas**: ~50 linhas modificadas/adicionadas

## ✅ Validação

### Compilação
```bash
dotnet build PklBridge.ConsoleApp/PklBridge.ConsoleApp.csproj
```

**Resultado**: ✅ Compilação bem-sucedida com 18 avisos não críticos

### Logs Esperados (após correção)

```
[INF] 🔍 DETECTADO: QUERY (Q) - HLAB solicitando exames!
[INF] 🔍 ID do Paciente extraído: 50
[INF] ✅ Paciente encontrado: Paciente Cincuenta
[INF] 📋 Encontrados 10 exames: GLICOSE, CREAT, COLESTEROL...
[INF] 📄 Mensagem ASTM construída: 249 caracteres
[INF] 📦 ✅ Mensagem para paciente 50 ADICIONADA À FILA!
[INF] ⏳ Total de mensagens pendentes: 1
[INF] 🔄 Aguardando HLAB finalizar com EOT e reconectar com ENQ para enviar...
[INF] 🔍 DETECTADO: EOT (End of Transmission) - HLAB finalizou e vai desconectar
[INF] ⏳ Aguardando HLAB se reconectar com ENQ para enviar mensagens pendentes (1)...
[INF] 🔍 DETECTADO: ENQ (Enquiry) - HLAB reconectou!
[INF] 📦 DETECTADO 1 mensagem(ns) pendente(s)!
[INF] 📤 Enviando mensagem pendente para paciente 50
[INF] ✅ ACK enviado para ENQ
[INF] 📤 Enviando frame 1: 48 bytes
[INF] ✅ ACK recebido para frame 1
[INF] 📤 Enviando frame 2: 48 bytes
[INF] ✅ ACK recebido para frame 2
[INF] 📤 Enviando frame 3: 165 bytes
[INF] ✅ ACK recebido para frame 3
[INF] 📤 Enviando EOT
[INF] ✅ Mensagem enviada com sucesso para paciente 50!
```

## 🧪 Próximos Passos para Teste

1. **Compilar e executar**:
   ```bash
   dotnet run --project PklBridge.ConsoleApp
   ```

2. **Configurar HLAB**:
   - Conectar em `127.0.0.1:8081` via TCP/IP
   - Testar Query com ID de amostra

3. **Validar comportamento**:
   - ✅ Query recebida e ACK enviado
   - ✅ Mensagem adicionada à fila
   - ✅ EOT detectado
   - ✅ ENQ de reconexão detectado
   - ✅ Mensagem enviada com sucesso
   - ✅ PKL 125 recebe e processa Order

## 📝 Referências

- **Protocolo**: PPC 125 Protocol (ASTM E1394-97)
- **Seção**: "Examples" - Request from PPC 125 to Host / Test order from Host to PPC 125
- **Arquivo**: `memorybank/PPC 125 protocol.pdf`

## 🎯 Resultado

A correção resolve o problema de timing do protocolo ASTM, garantindo que:

1. ✅ Query é recebida e processada corretamente
2. ✅ Exames são buscados e mensagem é construída
3. ✅ Mensagem é **armazenada** em vez de enviada imediatamente
4. ✅ HLAB finaliza Query com EOT
5. ✅ HLAB **reconecta** com ENQ quando pronto para receber
6. ✅ PKL Bridge detecta ENQ e envia mensagem da fila
7. ✅ Order chega corretamente ao PKL 125

**Status**: ✅ PRONTO PARA TESTES COM HARDWARE REAL
