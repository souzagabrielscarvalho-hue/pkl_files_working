# Análise da Sequência que Funciona (Log de Referência)

## 📊 Sequência Completa do Log Funcionando

### 1️⃣ HLAB Inicia Comunicação
```
[TX] → <ENQ>
[RX] ← <ACK>
```

### 2️⃣ HLAB Envia Header
```
[TX] → <STX> 1H|\^&|||SAGES|||||Host|||1|20180629215845<CR> <ETX> CC<CR><LF>
[RX] ← <ACK>
```

### 3️⃣ HLAB Envia Query
```
[TX] → <STX> 2Q|1|^112233||ALL||||||||O<CR> <ETX> 46<CR><LF>
[RX] ← <ACK>
```
**Query Format:** `Q|1|^112233||ALL`
- Sample ID: `^112233` (S.No Mode - 6 dígitos)

### 4️⃣ HLAB Envia Terminator
```
[TX] → <STX> 3L|1|N<CR> <ETX> 06<CR><LF>
[RX] ← <ACK>
```

### 5️⃣ HLAB Finaliza
```
[TX] → <ACK>
[RX] ← <EOT>
```

### 6️⃣ Sistema Inicia Resposta (ENQ)
```
[TX] → <ACK>
[RX] ← <STX> 1H|`^&|||LABPLUS|||||E1394-97|20170802102503<CR> <ETX> 5A<CR><LF>
```

### 7️⃣ Sistema Envia Patient Record
```
[TX] → <ACK>
[RX] ← <STX> 2P|1|||Mr.Test1 Surname|||M||||||29^Y<CR> <ETX> 87<CR><LF>
```

### 8️⃣ Sistema Envia Order Record ⭐ CRÍTICO!
```
[TX] → <ACK>
[RX] ← <STX> 3O|2|112233^^^N||^^^UREA`^^^CREA`^^^GLU|R|20170802102503||||||||Plasma||||||||||O<CR> <ETX> 3A<CR><LF>
```

**Order Format Funcionando:**
```
O|2|112233^^^N||^^^UREA`^^^CREA`^^^GLU|R|...
     ^^^^^^
     SEM ^ inicial! (ID Mode)
```

### 9️⃣ Sistema Finaliza
```
[TX] → <ACK>
[RX] ← <EOT>
[TX] → <STX> 4L|1|N<CR> <ETX> 08<CR><LF>
[RX] ← <ACK>
```

## 🔍 Análise Crítica

### ✅ Formato Correto (Funcionando)
```
O|2|112233^^^N||^^^UREA`^^^CREA`^^^GLU|R|...
     ^^^^^^
     ID Mode: 112233^^^N
```

**Estrutura:**
- `112233` = Sample ID (SEM ^ inicial)
- `^` = Separador
- `^` = Rack (vazio)
- `^` = Position (vazio)
- `N` = Type

### ❌ Formato Atual (Nosso Sistema)
```
O|2|031220251276^1^1^N||^^^TGP`^^^COT`...
     ^^^^^^^^^^^^^^^^
     031220251276^1^1^N
```

**Problema:** Estamos enviando Rack e Position preenchidos!

## 💡 Solução Correta

O log de referência mostra que devemos usar:
```
SampleID^^^Type
```

**NÃO:**
```
SampleID^Rack^Position^Type
```

### Correção Necessária

```csharp
// ANTES (ERRADO):
var sampleId = $"{patientId}^1^1^N";  // 031220251276^1^1^N

// DEPOIS (CORRETO):
var sampleId = $"{patientId}^^^N";    // 031220251276^^^N
```

## 📝 Observações Importantes

1. **Rack e Position vazios:** O log mostra `^^^N` (três `^` seguidos)
2. **ID Mode:** Sem `^` inicial no Sample ID
3. **Formato exato:** `SampleID^^^Type`
4. **Backtick nos testes:** `^^^UREA`^^^CREA`^^^GLU` (usando ` como separador)

## 🎯 Próxima Correção

Alterar `BuildAstmOrderMessage()` para usar:
```csharp
var sampleId = $"{patientId}^^^N";  // 112233^^^N
```

Isso deve resolver o problema de "lista em branco" ao clicar em Salvar!
