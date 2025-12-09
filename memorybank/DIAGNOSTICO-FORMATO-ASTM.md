# Diagnóstico do Formato ASTM - HLAB não exibe exames

## 📊 SITUAÇÃO ATUAL

✅ Comunicação ASTM funcionando (ACKs recebidos)
✅ Frames enviados e confirmados
✅ Sessão ASTM concluída
❌ HLAB não exibe os exames na tela

## 🔍 ANÁLISE DOS LOGS

### Log do Usuário (06:02:41)

```
Query recebida: <STX>2Q|1|^14102025588||ALL||||||||O<CR>
Resposta enviada: 4 frames
Sessão concluída com sucesso
```

### log.txt (Exemplo que FUNCIONA)

```
Query: <STX>2Q|1|^112233||ALL||||||||O<CR>
Resposta:
  Frame 1: H|`^&|||LABPLUS||||||||E1394-97|20170802102503<CR>
  Frame 2: P|1||||Mr.Test1 Surname|||M||||||29^Y<CR>
  Frame 3: O|2|112233^^^^N||^^^UREA`^^^CREA`^^^GLU|R|20170802102503|||||||||Plasma||||||||||O<CR>
  Frame 4: L|1|N<CR>
```

## 🎯 DIFERENÇAS IDENTIFICADAS

### 1. Campo Patient ID no registro P

**log.txt (funciona)**:
```
P|1||||Mr.Test1 Surname|||M||||||29^Y
     ↑ vazio
```

**Nosso código atual**:
```
P|1||14102025588||Paciente Teste|||M||||||30^Y
     ↑ tem o ID
```

### 2. Ordem dos campos no registro P

No log.txt, o Patient ID aparece VAZIO no registro P (campo 3).

O Patient ID aparece apenas no registro O (Order).

## ✅ CORREÇÃO NECESSÁRIA

Mudar o registro P para:
```csharp
// P - Patient Record (SEM Patient ID no campo 3)
sb.Append($"P|1||||{patient.FirstName} {patient.LastName}|||{patient.Gender}||||||{age}^Y\r");
```

## 📋 FORMATO CORRETO FINAL

```
H|`^&|||PKL Bridge||||||||E1394-97|{timestamp}<CR>
P|1||||Paciente Teste|||M||||||30^Y<CR>
O|2|14102025588^^^^N||^^^PROTEIN`^^^GLUCOSE`^^^KETONES`^^^BLOOD`^^^NITRITE|R|{timestamp}|||||||||Plasma||||||||||O<CR>
L|1|N<CR>
```

## 🔍 NOTAS IMPORTANTES

- Patient ID vai APENAS no Order Record (O), não no Patient Record (P)
- No Patient Record, o campo 3 (Patient ID) fica VAZIO
- O nome do paciente vai no campo 5 (Patient Name)
