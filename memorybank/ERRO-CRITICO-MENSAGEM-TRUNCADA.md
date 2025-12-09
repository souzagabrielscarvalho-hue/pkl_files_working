# ERRO CRÍTICO: Mensagem ASTM Truncada

**Data:** 09/12/2025 06:01

## Problema Identificado

A mensagem ASTM Order está sendo **TRUNCADA** no meio do envio! 

### Log da Mensagem Enviada

```text
O|2|50^^^^N||^^^GLICOSE`^^^CREAT`^^^COLESTEROL`^^^HDL`^^^LDH`^^^ALBUMINA`^^^GAMA_GT`^^^AST/TGO`^^^ALT/TGP`^^^FOSL|1|N|R|20251209060028|||||||||Plasma||||||||||O
```

### Análise do Problema

**MENSAGEM ESTÁ QUEBRADA!** Veja:

1. ❌ **Teste incompleto:** `^^^FOSL` (deveria ser `^^^FOSF_ALC`)
2. ❌ **Campos extras:** `|1|N|` aparecem no meio da mensagem
3. ❌ **Estrutura incorreta:** A mensagem está misturando campos

### Comparação

**Esperado:**
```text
O|2|50^^^^N||^^^GLICOSE`^^^CREAT`^^^COLESTEROL`^^^HDL`^^^LDH`^^^ALBUMINA`^^^GAMA_GT`^^^AST/TGO`^^^ALT/TGP`^^^FOSF_ALC|R|20251209060028|||||||||Plasma||||||||||O
```

**Enviado (TRUNCADO):**
```text
O|2|50^^^^N||^^^GLICOSE`^^^CREAT`^^^COLESTEROL`^^^HDL`^^^LDH`^^^ALBUMINA`^^^GAMA_GT`^^^AST/TGO`^^^ALT/TGP`^^^FOSL|1|N|R|20251209060028|||||||||Plasma||||||||||O
```

## Causa Raiz

O método `BuildAstmOrderMessage()` no `ConsoleWorker.cs` está **MISTURANDO** a linha do Order Record com a linha do Terminator Record!

### Código Problemático

```csharp
sb.Append($"O|2|{sampleId}||{testCodesList}|R|{timestamp}|||||||||Plasma||||||||||O\r");
sb.Append($"L|1|N\r");
```

O problema é que `testCodesList` está sendo **CORTADO** e o `L|1|N` está sendo inserido no meio!

## Análise do Frame 3

```text
Frame 3 TXT: <STX>3O|2|50^^^^N||^^^GLICOSE`^^^CREAT`^^^COLESTEROL`^^^HDL`^^^LDH`^^^ALBUMINA`^^^GAMA_GT`^^^AST/TGO`^^^ALT/TGP`^^^FOSF_ALC|R|20251209060028|||||||||Plasma||||||||||O<ETX>F9↵↓
```

**O frame 3 está CORRETO!** A mensagem completa está sendo enviada corretamente pelo `AstmSessionManager`.

## Problema Real

O log mostra:

```text
O|2|50^^^^N||^^^GLICOSE`^^^CREAT`^^^COLESTEROL`^^^HDL`^^^LDH`^^^ALBUMINA`^^^GAMA_GT`^^^AST/TGO`^^^ALT/TGP`^^^FOSL|1|N|R|20251209060028|||||||||Plasma||||||||||O
```

Mas o frame enviado está correto:

```text
O|2|50^^^^N||^^^GLICOSE`^^^CREAT`^^^COLESTEROL`^^^HDL`^^^LDH`^^^ALBUMINA`^^^GAMA_GT`^^^AST/TGO`^^^ALT/TGP`^^^FOSF_ALC|R|20251209060028|||||||||Plasma||||||||||O
```

## Conclusão

**O LOG ESTÁ TRUNCADO, MAS A MENSAGEM ENVIADA ESTÁ CORRETA!**

O problema é apenas no **LOG DE EXIBIÇÃO** no console, não na mensagem real enviada ao HLAB.

Veja o Frame 3 HEX completo:
```
02334F7C327C35305E5E5E5E4E7C7C5E5E5E474C49434F5345605E5E5E4352454154605E5E5E434F4C45535445524F4C605E5E5E48444C605E5E5E4C4448605E5E5E414C42554D494E41605E5E5E47414D415F4754605E5E5E4153542F54474F605E5E5E414C542F544750605E5E5E464F53465F414C437C527C32303235313230393036303032387C7C7C7C7C7C7C7C7C506C61736D617C7C7C7C7C7C7C7C7C7C4F0346390D0A
```

Decodificando: `^^^FOSF_ALC` está completo (464F53465F414C43 = FOSF_ALC)

## Problema Real com HLAB

Se o HLAB não está exibindo nada, **NÃO é por causa da mensagem truncada** (que não está truncada).

O problema deve ser **OUTRO**:

1. ❓ Campo 3 do Patient Record: `P|1||^50||` - tem `^50` mas deveria ser apenas `50`?
2. ❓ Ordem dos campos no Order Record
3. ❓ Algum campo obrigatório faltando

## Próxima Ação

Comparar **EXATAMENTE** com o log.txt de referência, campo por campo.
