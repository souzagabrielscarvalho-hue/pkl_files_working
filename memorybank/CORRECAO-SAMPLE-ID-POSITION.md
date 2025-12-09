# Correção: Erro "O valor do ID deve ser entre 1-996" no HLAB

## 🔍 Problema Identificado

Ao tentar salvar dados do paciente 50 no HLAB, o sistema apresentava o erro:
**"O valor do ID deve ser entre 1-996"**

## 📋 Análise do Problema

### Problema 1: Campo Sample ID Vazio

**Em `ExamRequestService.cs`:**

```csharp
// ❌ ANTES - Formato incompleto
O|1|{tagId}||{testCodes}|R||{timestamp}|||||||||||||||||||F
```

- Campo 2 (Sample ID) = apenas `{tagId}` (ex: "50")
- Campo 3 (Instrument Specimen ID) = **vazio**
- **Sem formato completo** `^^rack^position^N`

### Problema 2: Valores Hardcoded

**Em `AstmMessageBuilder.cs`:**

```csharp
// ❌ ANTES - Valores fixos
O|1|{order.SpecimenId}^^1^10^N||^^^{testCode}|{order.Priority}|...
```

- Rack position = **1** (fixo)
- Position number = **10** (fixo)

O erro **"O valor do ID deve ser entre 1-996"** se referia ao **position number** que deveria vir dinamicamente da API VIDA.

## ✅ Solução Implementada

### 1. Atualização do Modelo de Dados

**Arquivo:** `PklBridge.Core/Models/VidaApiModels.cs`

```csharp
public class VidaExamResponse
{
    public string Message { get; set; } = string.Empty;
    public List<VidaExamData> Data { get; set; } = new();
    public int RackPosition { get; set; } = 1;      // ✅ NOVO
    public int PositionNumber { get; set; } = 1;    // ✅ NOVO
}
```

### 2. Mock Atualizado com Valores Dinâmicos

**Arquivo:** `PklBridge.Infrastructure/Api/MockVidaApiClient.cs`

```csharp
public async Task<ApiResponse<VidaExamResponse>> GetExamsByTagAsync(...)
{
    // ✅ Definir rack/position baseado no tagId
    int rackPosition = 1;
    int positionNumber = int.TryParse(tagId, out var pos) ? pos : 1;
    
    // Garantir que position está no intervalo válido (1-996)
    if (positionNumber < 1 || positionNumber > 996)
    {
        positionNumber = 1;
    }

    var response = new VidaExamResponse
    {
        Message = "Procedimentos encontrados com sucesso!",
        RackPosition = rackPosition,         // ✅ DINÂMICO
        PositionNumber = positionNumber,     // ✅ DINÂMICO baseado no tagId
        Data = new List<VidaExamData>
        {
            new VidaExamData { ExamCode = "TCO", Test = "TCO" },
            new VidaExamData { ExamCode = "PCR", Test = "PCR" },
            new VidaExamData { ExamCode = "GLUC", Test = "GLUC" }
        }
    };

    return ApiResponse<VidaExamResponse>.SuccessResult(response, 200, TimeSpan.FromMilliseconds(50));
}
```

### 3. Mensagem ASTM com Formato Completo

**Arquivo:** `PklBridge.Infrastructure/ExamRequestService.cs`

```csharp
private string BuildAstmOrderMessage(string tagId, VidaExamResponse examResponse)
{
    var message = new StringBuilder();
    var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

    // H - Header Record
    message.AppendLine($"H|\\^&|||PKL Bridge^1.0^PKL125|||||||P|1|{timestamp}");

    // P - Patient Record
    message.AppendLine($"P|1|{tagId}|||||||||||||||||||||||||||");

    // O - Order Record com formato completo: Sample ID^^Rack^Position^N
    var sampleId = $"{tagId}^^{examResponse.RackPosition}^{examResponse.PositionNumber}^N";
    var testCodes = string.Join("\\", examResponse.Data.Select(e => $"^^^{e.Test}"));
    message.AppendLine($"O|1|{sampleId}||{testCodes}|R||{timestamp}|||||||||||||||||||F");

    // L - Terminator Record
    message.AppendLine($"L|1|N");

    return message.ToString();
}
```

**Chamada atualizada:**

```csharp
// 2. Gerar mensagem ASTM Order
_logger.LogDebug("📝 Gerando mensagem ASTM Order com Rack={RackPosition}, Position={PositionNumber}", 
    apiResponse.Data.RackPosition, apiResponse.Data.PositionNumber);

var astmMessage = BuildAstmOrderMessage(tagId, apiResponse.Data);  // ✅ Passa objeto completo
result.AstmMessage = astmMessage;
```

## 📊 Resultado

### Para o paciente ID = 50

**Mensagem ASTM gerada:**

```
H|\^&|||PKL Bridge^1.0^PKL125|||||||P|1|20251029050000
P|1|50|||||||||||||||||||||||||||
O|1|50^^1^50^N||^^^TCO\^^^PCR\^^^GLUC|R||20251029050000|||||||||||||||||||F
L|1|N
```

**Onde:**

- `50^^1^50^N` = Sample ID com:
  - `50` = ID da amostra (tagId)
  - `1` = Rack/bandeja position
  - `50` = Position number (válido entre 1-996) ✅
  - `N` = Indicador

## 🎯 Benefícios

1. ✅ **Campo Sample ID preenchido corretamente** com formato `^^rack^position^N`
2. ✅ **Position number dinâmico** vindo da API (ou do Mock baseado no tagId)
3. ✅ **Validação automática** garantindo que position está entre 1-996
4. ✅ **Logs informativos** mostrando rack e position usados
5. ✅ **Preparado para API VIDA real** - basta retornar os campos corretos

## 🔄 Próximos Passos

Quando integrar com a **API VIDA real**, certifique-se de que o endpoint retorne:

```json
{
  "message": "Procedimentos encontrados com sucesso!",
  "rackPosition": 1,
  "positionNumber": 50,
  "data": [
    { "examCode": "TCO", "test": "TCO" },
    { "examCode": "PCR", "test": "PCR" }
  ]
}
```

## 📝 Arquivos Modificados

1. `PklBridge.Core/Models/VidaApiModels.cs` - Campos RackPosition e PositionNumber adicionados
2. `PklBridge.Infrastructure/Api/MockVidaApiClient.cs` - Valores dinâmicos no mock
3. `PklBridge.Infrastructure/ExamRequestService.cs` - Formato ASTM completo com rack/position

## ✅ Status

**CONCLUÍDO** - Build bem-sucedido, pronto para testes com HLAB
