# Correção do Record Sequence Number

## Mudança Implementada (04/11/2024 - 08:32)

### Problema Identificado
O registro Order estava usando Record Sequence Number = 2:
```
O|2|50^^1^50^N||^^^GLICOSE`...
```

### Exemplo Correto do log.txt
```
O|1|996^^3^40^2||^^^TCO`^^^PCR`^^^GLUC|R|...
```

## Correção Aplicada

Mudamos o Record Sequence Number de `O|2|...` para `O|1|...`

### Antes:
```csharp
sb.Append($"O|2|{specimenId}||{testCodesList}|R|{timestamp}|...");
```

### Depois:
```csharp
sb.Append($"O|1|{specimenId}||{testCodesList}|R|{timestamp}|...");
```

## Formato Final do Registro Order

```
O|1|50^^1^50^N||^^^GLICOSE`^^^CREAT`...`^^^FOSF_ALC|R|20251104082730|||||||||1||||||||||O
```

### Campos Confirmados:
- **Campo 1:** `O` - Tipo de registro
- **Campo 2:** `1` - Record Sequence Number (DEVE SER 1!)
- **Campo 3:** `50^^1^50^N` - Specimen ID (ID Mode)
- **Campo 4:** Vazio - Instrument Specimen ID
- **Campo 5:** `^^^GLICOSE`...` - Universal Test IDs
- **Campo 6:** `R` - Priority (Routine)
- **Campo 7:** `20251104082730` - Requested Date/Time
- **Campo 16:** `1` - Specimen Descriptor (Serum)
- **Campo 26:** `O` - Report Type (Order)

## Status

✅ Compilado com sucesso
✅ Record Sequence Number corrigido para 1
✅ Formato ID Mode implementado
✅ Todos os campos alinhados com o exemplo
