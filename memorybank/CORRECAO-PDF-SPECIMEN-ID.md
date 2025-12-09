# Correção do Specimen ID conforme PDF PPC 125 Protocol

## Descoberta Crítica (04/11/2024)

### Problema Identificado
O campo 3 (Specimen ID) do registro O estava usando formato INCORRETO!

### Formato CORRETO segundo PDF (página Test Order Record):

#### Campo 3 - Specimen ID (5 subcampos):
```
3^1 - Sample ID (se ID Mode) ou VAZIO
3^2 - Sample No. (se S.No Mode) <- ID VAI AQUI!
3^3 - Disk ID (1 caracter)
3^4 - Position No. (2 caracteres)
3^5 - Diluent ('Y' ou 'N')
```

### Exemplos do PDF:

**S.No Mode:**
```
^1^1^10^N
```
- Subcampo 1: vazio
- Subcampo 2: Sample No = 1
- Subcampo 3: Disk = 1
- Subcampo 4: Position = 10
- Subcampo 5: Diluent = N

**Sample ID Mode:**
```
CA2201320078^^1^10^N
```
- Subcampo 1: Sample ID = CA2201320078
- Subcampo 2: vazio
- Subcampo 3: Disk = 1
- Subcampo 4: Position = 10
- Subcampo 5: Diluent = N

### Correção Aplicada:

**ANTES (ERRADO):**
```csharp
var sampleId = "1^^^^N";  // Formato errado!
```

**DEPOIS (CORRETO):**
```csharp
var specimenId = $"^{patientId}^1^1^N";  // S.No Mode correto!
```

### Resultado:
- Campo 3: `^50^1^1^N` (ID no subcampo 2 como S.No Mode)
- Campo 4: `50` (pode repetir o ID)
- Campo 16: `1` (Specimen Descriptor = Serum)

## Observações Importantes:

1. **S.No Mode vs ID Mode**: 
   - S.No Mode: ID vai no subcampo 2
   - ID Mode: ID vai no subcampo 1

2. **Campo 16 (Specimen Descriptor)**: 
   - 1 = Serum
   - 2 = Urine
   - 3 = CSF
   - 4 = Suprnt
   - 5 = Others

3. **Formato completo do registro O:**
```
O|2|^50^1^1^N|50|^^^GLICOSE`^^^CREAT`...|R|timestamp|||||||||1||||||||||O
```

Esta correção segue EXATAMENTE o protocolo PPC 125!
