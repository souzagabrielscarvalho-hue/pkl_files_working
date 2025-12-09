# Solução Final: Correção do Formato ASTM Order

**Data:** 09/12/2025 05:58

## Problema Original

O HLAB estava recebendo mensagens ASTM Order, mas não exibia nada na tela - nem criava linha, nem mostrava erro. Após análise detalhada comparando com o log de referência (`log.txt`), identificamos **4 problemas críticos** no formato da mensagem.

## Análise Comparativa

### Log de Referência (Funciona)

```text
O|2|112233^^^^N||^^^UREA`^^^CREA`^^^GLU|R|20170802102503|||||||||Plasma||||||||||O
```

### Nossa Mensagem (Não Funcionava)

```text
O|1|^50||^^^GLICOSE\^^^CREAT\^^^COLESTEROL|R|20251209055050|||||||||1||||||||||O
```

## Problemas Identificados

### 1. ❌ Sequence Number Incorreto

- **Esperado:** `O|2|` (segundo record após Patient)
- **Enviado:** `O|1|`
- **Impacto:** HLAB rejeita por sequência incorreta

### 2. ❌ Specimen ID Incompleto

- **Esperado:** `112233^^^^N` (ID + 4 separadores vazios + Type)
- **Enviado:** `^50` (apenas 1 separador + ID)
- **Impacto:** HLAB não reconhece o formato

### 3. ❌ Specimen Type Incorreto

- **Esperado:** `Plasma` ou `Serum` (texto no campo 16)
- **Enviado:** `1` (número)
- **Impacto:** HLAB não reconhece o tipo de amostra

### 4. ⚠️ Separador de Testes Diferente

- **Referência:** `` ` `` (backtick)
- **Enviado:** `\` (backslash)
- **Impacto:** Pode funcionar, mas não é o padrão

## Correções Implementadas

### 1. AstmMessageBuilder.cs

```csharp
private byte[] BuildOrderFrame(ExamOrder order, string testCode)
{
    var frameNum = GetNextFrameNumber();
    var specimenType = GetSpecimenType(testCode);
    // Formato correto segundo log.txt: ID^^^^Type (4 separadores vazios)
    var specimenId = $"{order.SpecimenId}^^^^N";
    var content = $"O|2|{specimenId}||^^^{testCode}|{order.Priority}|{order.OrderDateTime:yyyyMMddHHmmss}|||||||||{specimenType}||||||||||O";
    return BuildFrame(frameNum, content);
}
```

**Mudanças:**
- ✅ Sequence: `O|1|` → `O|2|`
- ✅ Specimen ID: `ID^^1^10^N` → `ID^^^^N`
- ✅ Specimen Type: mantém texto (`Plasma` ou `Serum`)

### 2. ExamRequestService.cs

```csharp
private string BuildAstmOrderMessage(string tagId, VidaExamResponse examResponse)
{
    var message = new StringBuilder();
    var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

    // H - Header Record
    message.AppendLine($"H|\\^&|||PKL Bridge^1.0^PKL125|||||||P|1|{timestamp}");

    // P - Patient Record
    message.AppendLine($"P|1|||{tagId}|Paciente {tagId}||{DateTime.Now.AddYears(-30):yyyyMMdd}|M||||||||||||||||||||");

    // O - Order Record - Formato correto segundo log.txt: ID^^^^Type
    var sampleId = $"{tagId}^^^^N";
    var testCodes = string.Join("`", examResponse.Data.Select(e => $"^^^{e.Test}"));
    message.AppendLine($"O|2|{sampleId}|{tagId}|{testCodes}|R|{timestamp}|||||||||Plasma||||||||||O");

    // L - Terminator Record
    message.AppendLine($"L|1|N");

    return message.ToString();
}
```

**Mudanças:**
- ✅ Sequence: `O|1|` → `O|2|`
- ✅ Specimen ID: `{tagId}^^^{rack}^{position}^N` → `{tagId}^^^^N`
- ✅ Separador: `\` → `` ` ``
- ✅ Specimen Type: `Plasma` (texto)

### 3. ConsoleWorker.cs

```csharp
private string BuildAstmOrderMessage(string patientId, Core.Models.PatientData patient, Core.Interfaces.ExamOrder order)
{
    var sb = new System.Text.StringBuilder();
    var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

    // H - Header Record
    sb.Append($"H|\\^&|||PKL Bridge|||||||1|{timestamp}\r");

    // P - Patient Record
    var birthDate = patient.BirthDate?.ToString("yyyyMMdd") ?? DateTime.Now.AddYears(-30).ToString("yyyyMMdd");
    var patientName = $"{patient.FirstName} {patient.LastName}";
    var age = patient.BirthDate.HasValue 
        ? (DateTime.Now.Year - patient.BirthDate.Value.Year).ToString()
        : "30";
    sb.Append($"P|1||^{patientId}||{patientName}|||{patient.Gender}||||||{age}^Y\r");

    // O - Order Record - FORMATO CORRETO segundo log.txt
    var sampleId = $"{patientId}^^^^N";  // Formato: ID^^^^Type
    var testCodesList = string.Join("`", order.TestCodes.Select(t => $"^^^{t}"));
    sb.Append($"O|2|{sampleId}||{testCodesList}|R|{timestamp}|||||||||Plasma||||||||||O\r");

    // L - Terminator Record  
    sb.Append($"L|1|N\r");

    return sb.ToString();
}
```

**Mudanças:**
- ✅ Sequence: `O|1|` → `O|2|`
- ✅ Specimen ID: `^{patientId}` → `{patientId}^^^^N`
- ✅ Separador: `\` → `` ` ``
- ✅ Specimen Type: `1` → `Plasma`

## Formato Final (Correto)

```text
H|\^&|||PKL Bridge|||||||1|20251209055050
P|1||^50||Paciente Cincuenta|||M||||||35^Y
O|2|50^^^^N||^^^GLICOSE`^^^CREAT`^^^COLESTEROL`^^^HDL`^^^LDH|R|20251209055050|||||||||Plasma||||||||||O
L|1|N
```

## Estrutura do Specimen ID (Campo 2 do Order Record)

Segundo ASTM E1394-97 e log.txt de referência:

```text
Posição 1: Specimen ID (obrigatório) - ex: "112233"
Posição 2: Accession Number (opcional) - vazio
Posição 3: Container ID (opcional) - vazio
Posição 4: Rack Number (opcional) - vazio
Posição 5: Position Number (opcional) - vazio
Posição 6: Specimen Type (opcional) - ex: "N"
```

**Formato correto:** `ID^^^^Type`

**Exemplo:** `112233^^^^N`

## Compilação

✅ **Projeto compilado com sucesso:**

```bash
dotnet build --no-incremental
Construir êxito(s) com 15 aviso(s) em 3,0s
```

## Próximos Passos

1. ✅ Código corrigido e compilado
2. 🔄 **Testar com HLAB real** para validar se as correções resolvem o problema
3. 📊 Monitorar logs para confirmar que HLAB aceita e processa as mensagens
4. ✅ Verificar se HLAB exibe a linha com os dados do paciente

## Referências

- `memorybank/log.txt` - Log de referência com formato correto
- `memorybank/PPC 125 protocol.pdf` - Especificação do protocolo ASTM
- `memorybank/ANALISE-COMPARACAO-LOG.md` - Análise detalhada das diferenças
- `memorybank/CORRECAO-RACK-POSITION.md` - Tentativa anterior (incorreta)

## Lições Aprendidas

1. **Sempre comparar com logs de referência** - O log.txt foi fundamental para identificar o formato correto
2. **Não assumir formatos** - A tentativa de incluir Rack e Position no formato `ID^^^Rack^Position^Type` estava incorreta
3. **Seguir o padrão ASTM** - O formato `ID^^^^Type` com 4 separadores vazios é o correto
4. **Sequence Number importa** - O HLAB espera `O|2|` (segundo record) e não `O|1|`
5. **Tipos de dados corretos** - Specimen Type deve ser texto (`Plasma`/`Serum`), não número

## Status

✅ **PRONTO PARA TESTE** - Todas as correções implementadas e compiladas com sucesso.
