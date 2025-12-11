# Erro "1-996" Persiste Após Implementação da API Real

## 📊 Status Atual

✅ **API de Produção FUNCIONANDO:**
- Busca exames com sucesso: `031220251276` retorna 11 exames
- Dados são enviados para HLAB
- Protocolo ASTM completo funciona

❌ **Erro "1-996" PERSISTE:**
- HLAB mostra: "O valor do ID deve ser entre 1-996"
- Ocorre ao clicar em "OK" após receber os dados

## 🔍 Análise do Log

### Mensagem Enviada (Frame 3):
```
O|2|^031220251276^1^031220251276^N||^^^TGP`^^^COT`...
```

**Formato S.No Mode:**
- `^031220251276` = Sample Number (tag_id completo)
- `^1` = Rack Number
- `^031220251276` = **Position Number** ← PROBLEMA!
- `^N` = Type

## ❌ Problema Identificado

O **Position Number** está recebendo `031220251276` (12 dígitos), mas o HLAB **só aceita valores entre 1-996**!

### Formato Atual (ERRADO):
```
^031220251276^1^031220251276^N
                 ^^^^^^^^^^^^
                 Position = 031220251276 (INVÁLIDO!)
```

### Formato Correto:
```
^031220251276^1^1^N
                ^
                Position = 1 (VÁLIDO!)
```

## 💡 Solução

O **Position Number** deve ser um valor fixo entre 1-996, **NÃO** o tag_id completo.

### Opções:

1. **Usar posição fixa = 1:**
   ```csharp
   var sampleId = $"^{patientId}^1^1^N";  // Position sempre 1
   ```

2. **Extrair últimos 3 dígitos do tag_id:**
   ```csharp
   var position = patientId.Length > 3 ? patientId.Substring(patientId.Length - 3) : patientId;
   var positionNum = int.Parse(position) % 996 + 1;  // Garante 1-996
   var sampleId = $"^{patientId}^1^{positionNum}^N";
   ```

3. **Usar hash do tag_id:**
   ```csharp
   var positionNum = (patientId.GetHashCode() & 0x7FFFFFFF) % 996 + 1;
   var sampleId = $"^{patientId}^1^{positionNum}^N";
   ```

## 📝 Recomendação

**Usar posição fixa = 1** é a solução mais simples e segura, pois:
- Sempre válida (1 está entre 1-996)
- Não depende do formato do tag_id
- HLAB não usa Position Number para identificação (usa Sample Number)

## 🔧 Correção Necessária

**Arquivo:** `PklBridge.ConsoleApp/ConsoleWorker.cs`

**Linha atual:**
```csharp
var sampleId = $"^{patientId}^1^{patientId}^N";  // ❌ Position = patientId (ERRADO!)
```

**Correção:**
```csharp
var sampleId = $"^{patientId}^1^1^N";  // ✅ Position = 1 (CORRETO!)
```
