# Mudança Crítica - ID Mode vs S.No Mode

## Descoberta (04/11/2024 - 08:25)

### Existem DOIS modos de Specimen ID no protocolo ASTM:

1. **S.No Mode**: `^SampleNo^Disk^Position^Diluent`
   - Começa com ^ 
   - Sample Number no segundo subcampo
   
2. **ID Mode**: `SampleID^^Disk^Position^Diluent`  
   - Sem ^ inicial
   - ID direto no primeiro subcampo

### Implementação Anterior (S.No Mode)
```
O|2|^50^1^50^N|50|^^^GLICOSE`...
```

### Nova Implementação (ID Mode)
```
O|2|50^^1^50^N||^^^GLICOSE`...
```

## Mudanças no Código

- Campo 3: `50^^1^50^N` (ID Mode)
- Campo 4: Vazio (||)
- Position: Continua usando o ID (50)

## Hipótese

O HLAB pode estar configurado para aceitar **ID Mode** ao invés de S.No Mode. Esta é uma diferença sutil mas CRÍTICA no protocolo ASTM.

## Teste

1. Reiniciar PKL Bridge
2. Testar no HLAB com paciente ID 50
3. Verificar se aceita o novo formato
