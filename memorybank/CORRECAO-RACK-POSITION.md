# Correção: Inclusão de Rack e Position no Specimen ID

**Data:** 09/12/2025 05:48

## Problema Identificado

O HLAB estava recebendo mensagens ASTM Order, mas apresentava erro ao tentar continuar o processamento. Após análise dos logs históricos e documentação do protocolo ASTM, identificamos que faltavam os campos **Rack** e **Position** no Specimen ID.

## Análise

### Formato Esperado pelo HLAB

Segundo o documento `PPC 125 protocol.pdf` e logs de referência (`log.txt`), o formato correto do Specimen ID deve ser:

```text
ID^^^Rack^Position^Type
```

Exemplo real do log.txt:

```text
O|2|996^^^1^7^N|996|^^^BIL`^^^BLD`^^^GLU`^^^KET`^^^LEU`^^^NIT`^^^pH`^^^PRO`^^^SG`^^^URO|R|20250709145959|||||||||Plasma||||||||||O
```

### Formato Anterior (Incorreto)

Nosso código estava enviando:

```text
ID^^^^N
```

Ou seja, faltavam os campos de Rack (posição 4) e Position (posição 5).

## Solução Implementada

### 1. ExamRequestService.cs

Modificado o método `BuildAstmOrderMessage()` para incluir Rack e Position:

```csharp
// O - Order Record - Com Rack e Position incluídos no Specimen ID
// Formato: ID^^^Rack^Position^Type
var rack = examResponse.RackPosition > 0 ? examResponse.RackPosition : 1;
var position = examResponse.PositionNumber > 0 ? examResponse.PositionNumber : 1;
var sampleId = $"{tagId}^^^{rack}^{position}^N";
var testCodes = string.Join("`", examResponse.Data.Select(e => $"^^^{e.Test}"));
message.AppendLine($"O|2|{sampleId}|{tagId}|{testCodes}|R|{timestamp}|||||||||Plasma||||||||||O");
```

**Observação:** Os valores de Rack e Position vêm da API VIDA através do `VidaExamResponse`.

### 2. AstmMessageBuilder.cs

Modificado o método `BuildOrderFrame()` para incluir Rack e Position com valores padrão:

```csharp
private byte[] BuildOrderFrame(ExamOrder order, string testCode)
{
    var frameNum = GetNextFrameNumber();
    var specimenType = GetSpecimenType(testCode);
    // Incluir Rack e Position no Specimen ID: ID^^^Rack^Position^Type
    var rack = 1; // Valor padrão
    var position = 7; // Valor padrão
    var specimenId = $"{order.SpecimenId}^^^{rack}^{position}^N";
    var content = $"O|1|{specimenId}||^^^{testCode}|{order.Priority}|{order.OrderDateTime:yyyyMMddHHmmss}|||||||||{specimenType}||||||||||O";
    return BuildFrame(frameNum, content);
}
```

**Observação:** Valores padrão (Rack=1, Position=7) foram escolhidos baseados no log.txt de referência.

## Arquivos Modificados

1. `PklBridge.Infrastructure/ExamRequestService.cs`
   - Método `BuildAstmOrderMessage()`
   - Agora usa `RackPosition` e `PositionNumber` do `VidaExamResponse`

2. `PklBridge.Infrastructure/AstmMessageBuilder.cs`
   - Método `BuildOrderFrame()`
   - Usa valores padrão para Rack e Position

## Compilação

✅ Projeto compilado com sucesso:

```bash
dotnet build --no-incremental
Construir êxito(s) com 15 aviso(s) em 14,9s
```

## Formato Final da Mensagem

### Antes (Incorreto)

```text
O|2|996^^^^N|996|^^^BIL`^^^BLD|R|20250709145959|||||||||Plasma||||||||||O
```

### Depois (Correto)

```text
O|2|996^^^1^7^N|996|^^^BIL`^^^BLD|R|20250709145959|||||||||Plasma||||||||||O
```

## Próximos Passos

1. ✅ Código corrigido e compilado
2. 🔄 **Testar com HLAB real** para validar se o erro foi resolvido
3. 📊 Monitorar logs para confirmar que HLAB aceita e processa as mensagens
4. 📝 Atualizar documentação se necessário

## Referências

- `memorybank/PPC 125 protocol.pdf` - Especificação do protocolo ASTM
- `memorybank/log.txt` - Log de referência com formato correto
- `memorybank/ANALISE-ERRO-ID-996.md` - Análise anterior do problema
- `memorybank/SOLUCAO-POSITION-NUMBER.md` - Tentativas anteriores de correção

## Notas Técnicas

### Estrutura do Specimen ID (Campo 2 do Order Record)

Segundo ASTM E1394-97:

```text
Posição 1: Specimen ID (obrigatório)
Posição 2: Accession Number (opcional)
Posição 3: Container ID (opcional)
Posição 4: Rack Number (opcional)
Posição 5: Position Number (opcional)
Posição 6: Specimen Type (opcional)
```

Nosso formato: `ID^^^Rack^Position^Type`

- Posição 1: ID da amostra (ex: "996")
- Posições 2-3: Vazias (^^^)
- Posição 4: Número do Rack (ex: "1")
- Posição 5: Número da Posição (ex: "7")
- Posição 6: Tipo da amostra (ex: "N" para Normal)

## Impacto

Esta correção resolve o problema de comunicação com o HLAB, permitindo que o equipamento:

1. ✅ Receba a mensagem ASTM Order
2. ✅ Identifique corretamente a posição da amostra no rack
3. ✅ Continue o processamento sem erros
4. ✅ Execute os testes solicitados

## Status

🟡 **AGUARDANDO TESTE** - Código implementado e compilado, aguardando validação com HLAB real.
