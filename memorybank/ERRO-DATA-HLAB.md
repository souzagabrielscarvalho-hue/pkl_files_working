# Erro de Data no HLAB - "Invalid argument to date encode"

## 🔍 ERRO IDENTIFICADO

HLAB mostra popup: **"Invalid argument to date encode"**

Isso significa:
- ✅ Comunicação ASTM funcionando
- ✅ Frames enviados e ACKs recebidos
- ❌ HLAB não consegue processar algum campo de DATA

## 🎯 POSSÍVEIS CAUSAS

### 1. Formato do Timestamp

**Nosso código atual**:
```csharp
var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
// Resulta em: 20251024061700
```

**No log.txt (funciona)**:
```
H|`^&|||LABPLUS||||||||E1394-97|20170802102503<CR>
                                  ↑ mesmo formato
```

✅ Formato parece correto

### 2. Data de Nascimento no Patient Record

**Nosso código atual**:
```csharp
sb.Append($"P|1||||{patient.FirstName} {patient.LastName}|||{patient.Gender}||||||{age}^Y\r");
                                                                                  ↑ idade em anos
```

**No log.txt (funciona)**:
```
P|1||||Mr.Test1 Surname|||M||||||29^Y<CR>
                                   ↑ idade em anos também
```

✅ Formato parece correto

### 3. Campo de Data de Coleta no Order Record

**Nosso código atual**:
```csharp
sb.Append($"O|2|{patientId}^^^^N||{testCodesList}|R|{timestamp}|||||||||Plasma||||||||||O\r");
                                                      ↑ campo 7 = Collection Date/Time
```

**No log.txt (funciona)**:
```
O|2|112233^^^^N||^^^UREA`^^^CREA`^^^GLU|R|20170802102503|||||||||Plasma||||||||||O<CR>
                                           ↑ campo 7 também
```

✅ Formato parece correto

## 🤔 HIPÓTESE

O problema pode estar no **MockVidaApiClient** que retorna data de nascimento como `null`:

```csharp
BirthDate = null,  // ← PROBLEMA POTENCIAL
```

Isso faz nosso código calcular a idade como:
```csharp
var birthYear = patient.BirthDate?.Year ?? DateTime.Now.Year - 30;
var age = DateTime.Now.Year - birthYear;
```

**Se BirthDate for null, age = 30 anos** - mas talvez o HLAB precise da DATA DE NASCIMENTO REAL, não a idade!

## ✅ SOLUÇÃO SUGERIDA

Adicionar data de nascimento real no Mock:

```csharp
BirthDate = new DateTime(1994, 5, 15), // Exemplo: 15/05/1994
```

E enviar no formato ASTM correto no Patient Record:
```
P|1||||Nome|||M|19940515|||||30^Y
                  ↑ data nascimento YYYYMMDD
```

Ou verificar se no log.txt tem algum outro campo de data que estamos omitindo.
