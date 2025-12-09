# Correção de Timing e Ordem dos Frames ASTM

## 🎯 Problema Identificado

O HLAB não estava recebendo/processando os exames enviados pelo sistema, mesmo com o protocolo ASTM aparentemente correto.

## 🔍 Análise Comparativa

Comparando nosso sistema com o `log.txt` (que funciona), identificamos **diferenças críticas**:

### ❌ Sistema Anterior (Incorreto)

```
1. HLAB envia Query (Q) → ACK
2. Sistema IMEDIATAMENTE envia resposta:
   Frame 1 (H+P+O+L) → ACK
   Frame 2 (continuação) → ACK
   EOT
```

**Problemas:**
- Sem delays entre frames
- Frames combinados (H+P+O juntos)
- Frame L enviado ANTES do EOT
- Sem ACK intermediário

### ✅ Log.txt (Correto)

```
1. HLAB envia Query (Q) → ACK
2. HLAB envia Terminator (L) → ACK
3. HLAB envia EOT
4. ⏰ AGUARDA 5 SEGUNDOS
5. Sistema envia ACK (confirma que vai responder)
6. ⏰ AGUARDA 2 SEGUNDOS
7. Sistema envia Frame 1 (H) → ACK
8. ⏰ AGUARDA 3 SEGUNDOS
9. Sistema envia Frame 2 (P) → ACK
10. ⏰ AGUARDA 3 SEGUNDOS
11. Sistema envia Frame 3 (O) → ACK
12. ⏰ AGUARDA 6 SEGUNDOS
13. Sistema envia EOT
14. ⏰ AGUARDA 3 SEGUNDOS
15. Sistema envia Frame 4 (L) → ACK
```

## 🔧 Correções Implementadas

### 1. **Delays Entre Frames**

```csharp
// Após receber EOT do HLAB
await Task.Delay(5000, cancellationToken); // 5 segundos
await SendAckAsync(clientEndpoint, cancellationToken);

await Task.Delay(2000, cancellationToken); // 2 segundos antes de começar

// Entre cada frame
await Task.Delay(3000, cancellationToken); // 3 segundos

// Antes de EOT
await Task.Delay(6000, cancellationToken); // 6 segundos

// Antes de frame L
await Task.Delay(3000, cancellationToken); // 3 segundos
```

### 2. **Frames Separados**

Novo método `SplitIntoFramesSeparated()`:

```csharp
private List<string> SplitIntoFramesSeparated(string message)
{
    var frames = new List<string>();
    var lines = message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    
    foreach (var line in lines)
    {
        // Pular linha L - será enviada APÓS EOT
        if (line.StartsWith("L|"))
        {
            continue;
        }
        
        // Cada linha = um frame separado
        frames.Add(line);
    }
    
    return frames;
}
```

**Resultado:**
- Frame 1: `H|\^&|||PKL Bridge^1.0^PKL125|||||||P|1|20251104060243`
- Frame 2: `P|1|||50|Paciente Cincuenta||19900520|M||||||||||||||||||||`
- Frame 3: `O|2|1^^^^N|50|^^^GLICOSE`^^^CREAT`...`

### 3. **Frame L Após EOT**

```csharp
// Enviar EOT
await SendEotAsync(clientEndpoint, cancellationToken);

// Aguardar 3 segundos
await Task.Delay(3000, cancellationToken);

// Enviar frame L separado
var terminatorFrame = "L|1|N";
await SendFrameAndWaitAckAsync(clientEndpoint, lastFrameNumber, terminatorFrame, cancellationToken);
```

### 4. **ACK Intermediário**

```csharp
if (isResponseToQuery)
{
    // Aguardar 5 segundos após receber EOT do HLAB
    await Task.Delay(5000, cancellationToken);
    
    // Enviar ACK confirmando que vamos responder
    await SendAckAsync(clientEndpoint, cancellationToken);
    
    // Aguardar mais 2 segundos antes de começar
    await Task.Delay(2000, cancellationToken);
}
```

## 📊 Sequência Completa Implementada

```
HLAB → Query (Q)
Sistema → ACK

HLAB → Terminator (L)
Sistema → ACK

HLAB → EOT

⏰ 5 segundos

Sistema → ACK (intermediário)

⏰ 2 segundos

Sistema → Frame 1 (H)
HLAB → ACK

⏰ 3 segundos

Sistema → Frame 2 (P)
HLAB → ACK

⏰ 3 segundos

Sistema → Frame 3 (O)
HLAB → ACK

⏰ 6 segundos

Sistema → EOT

⏰ 3 segundos

Sistema → Frame 4 (L)
HLAB → ACK
```

## 🎯 Resultado Esperado

Com essas correções, o HLAB deve:

1. ✅ Receber os frames no timing correto
2. ✅ Processar cada frame individualmente
3. ✅ Reconhecer a estrutura ASTM correta
4. ✅ Exibir os exames na interface
5. ✅ Permitir que o operador visualize e processe os exames

## 🧪 Próximo Passo

Testar com HLAB real para confirmar que os exames agora aparecem corretamente na interface.

## 📝 Arquivos Modificados

- `PklBridge.Infrastructure/Serial/AstmSessionManager.cs`
  - Método `SendAstmMessageAsync()` - Implementado delays e ACK intermediário
  - Método `SplitIntoFramesSeparated()` - Novo método para separar frames
  - Método `GetDelayForFrame()` - Novo método para calcular delays

## 🔗 Referências

- `memorybank/log.txt` - Log de referência que funciona
- `memorybank/ANALISE-LOGS-DETALHADA.md` - Análise detalhada dos logs
- `memorybank/SOLUCAO-PROTOCOLO-ASTM-COMPLETO.md` - Documentação do protocolo
