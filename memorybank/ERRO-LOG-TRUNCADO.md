# Erro de Log Truncado - Análise Detalhada

## Problema Identificado (04/11/2024 - 08:30)

### Mensagem no Log
```
📄 ====== MENSAGEM ASTM COMPLETA ======
O|2|50^^1^50^N||^^^GLICOSE`...`^^^FOSF_ALCL|1|N251104082730|...
📄 ====== FIM DA MENSAGEM ======
```

### Problemas Visíveis:
1. **FOSF_ALCL** - Tem um L extra (deveria ser FOSF_ALC)
2. **L|1|N** está colando com o timestamp (N251104082730)
3. **Falta registros H e P** na visualização

## Análise Detalhada

O log está mostrando apenas uma parte da mensagem ASTM. Isso sugere que:

1. **O log está truncando a exibição** - Não mostra registros H, P
2. **Há um erro de concatenação** - O registro L está colando com o registro O

### Mensagem Correta Esperada:
```
H|\^&|||PKL Bridge|||||||1|20251104082730
P|1||50||Paciente Cincuenta|||M||||||35^Y
O|2|50^^1^50^N||^^^GLICOSE`^^^CREAT`...`^^^FOSF_ALC|R|20251104082730|||||||||1||||||||||O
L|1|N
```

## Possível Causa

O método `BuildAstmOrderMessage` está usando `\r` como separador de linha, mas parece que o registro L está sendo anexado incorretamente.

## Solução a Implementar

1. Verificar se há um `\r` faltando entre o registro O e L
2. Garantir que cada registro termina com `\r`
3. Verificar se o log está cortando a mensagem multi-linha
