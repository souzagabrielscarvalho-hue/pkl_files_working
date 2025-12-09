# Correções Finais do Protocolo ASTM

## Data: 07/01/2025

## 🔍 Problemas Identificados

### 1. Separador Incorreto (Backtick vs Backslash)
**Erro**: Usando ` (backtick/crase) para separar testes
**Correto**: Usar \ (backslash) conforme ASTM

### 2. Registro L (Terminator) Não Enviado
**Erro**: O registro L estava sendo **pulado** e nunca enviado
**Correto**: L deve ser enviado no último frame antes do EOT

### 3. ENQ Não Enviado
**Erro**: Usando `isResponseToQuery: true` que pula o ENQ
**Correto**: Usar `isResponseToQuery: false` para enviar ENQ primeiro

### 4. Estrutura do Order Validada
Verificado que a estrutura está correta conforme ASTM

## ✅ Correções Aplicadas

### Correção 1: Backslash em vez de Backtick
**Arquivo**: `PklBridge.ConsoleApp/ConsoleWorker.cs`
**Linha**: ~700

```csharp
// ANTES (ERRADO):
var testCodesList = string.Join("`", order.TestCodes.Select(t => $"^^^{t}"));

// DEPOIS (CORRETO):
var testCodesList = string.Join("\\", order.TestCodes.Select(t => $"^^^{t}"));
```

**Resultado**: Testes agora separados por `\` (backslash) conforme ASTM
- ❌ `^^^GLICOSE`^^^CREAT`^^^COLESTEROL`
- ✅ `^^^GLICOSE\^^^CREAT\^^^COLESTEROL\`

### Correção 2: Incluir Registro L
**Arquivo**: `PklBridge.Infrastructure/Serial/AstmSessionManager.cs`
**Método**: `SplitIntoFramesSeparated`

```csharp
// ANTES (ERRADO):
foreach (var line in lines)
{
    // Pular linha L (Terminator) - será enviada separadamente APÓS EOT
    if (line.StartsWith("L|"))
    {
        _logger.LogDebug("⏭️ Pulando frame L - será enviado após EOT");
        continue;  // <-- PULAVA O L!
    }
    frames.Add(line);
}

// DEPOIS (CORRETO):
foreach (var line in lines)
{
    // Cada linha vira um frame separado (incluindo L - Terminator)
    frames.Add(line);
}
```

**Resultado**: L agora é enviado no frame 4 antes do EOT

### Correção 3: Enviar ENQ Primeiro
**Arquivo**: `PklBridge.ConsoleApp/ConsoleWorker.cs`
**Método**: `SendExamsForPatient`

```csharp
// ANTES (ERRADO):
var success = await _astmSessionManager.SendAstmMessageAsync(
    clientEndpoint,
    astmMessage,
    isResponseToQuery: true,  // Pula ENQ!
    cancellationToken);

// DEPOIS (CORRETO):
var success = await _astmSessionManager.SendAstmMessageAsync(
    clientEndpoint,
    astmMessage,
    isResponseToQuery: false,  // ENVIA ENQ primeiro!
    cancellationToken);
```

**Resultado**: PKL Bridge agora inicia a sessão corretamente com ENQ

## 📋 Fluxo Correto Após Correções

### Query Recebida do HLAB
```
HLAB → PKL Bridge
ENQ
← ACK
<STX>2Q|1|^50||ALL||||||||O<ETX>7F<CR><LF>
← ACK
<STX>3L|1|N<ETX>06<CR><LF>
← ACK
EOT
```

### Resposta do PKL Bridge (CORRETO)
```
PKL Bridge → HLAB
ENQ                                              ← AGORA ENVIA!
← ACK
<STX>1H|\^&|||PKL Bridge|||||||1|20251107<ETX>XX<CR><LF>
← ACK
<STX>2P|1||50||Paciente Cincuenta|||M||||||35^Y<ETX>XX<CR><LF>
← ACK
<STX>3O|1|^50^1^50^N||^^^GLICOSE\^^^CREAT\...|R|20251107|||||||||1||||||||||O<ETX>XX<CR><LF>
                              ↑ BACKSLASH AGORA!
← ACK
<STX>4L|1|N<ETX>XX<CR><LF>                      ← AGORA ENVIADO!
← ACK
EOT
```

## 🎯 Resultados Esperados

### Antes das Correções ❌
- Testes com ` (backtick)
- Sem registro L
- Sem ENQ inicial
- 3 frames: H, P, O + EOT

### Depois das Correções ✅
- Testes com \ (backslash)
- Com registro L
- Com ENQ inicial
- 4 frames: H, P, O, L + EOT

## 📝 Arquivos Modificados

1. `PklBridge.ConsoleApp/ConsoleWorker.cs`
   - Linha ~700: Mudança de backtick para backslash
   - Linha ~552: Mudança de `isResponseToQuery: true` para `false`

2. `PklBridge.Infrastructure/Serial/AstmSessionManager.cs`
   - Método `SplitIntoFramesSeparated`: Removido skip do registro L

## ✅ Status

**Todas as 3 correções implementadas com sucesso!**

Pronto para compilar e testar com HLAB real.
