# 🔧 SOLUÇÃO: HLAB Query Não Retorna Exames

**Problema Identificado**: HLAB envia Query (Q) com ID do paciente mas PKL Bridge não responde com os exames

## 🎯 Análise do Problema

### O que está acontecendo:

1. ✅ HLAB está configurado para TCP 127.0.0.1:8081
2. ✅ PKL Bridge tem TcpServer na porta 8081
3. ✅ HLAB envia Query (Q) com ID do paciente (14102025588)
4. ❌ PKL Bridge não responde com Order (O) contendo os exames
5. ❌ HLAB fica esperando resposta

### Por que não funciona:

1. **SerialBridge não usa TcpServer**
   - SerialBridge só usa Named Pipe
   - TcpServer existe mas não está conectado ao fluxo

2. **MessageProcessor só processa Results (R)**
   - Ignora mensagens Query (Q)
   - Não gera resposta Order (O)

3. **Falta handler para Query**
   - Não há código para processar Query
   - Não há código para buscar exames na API VIDA
   - Não há código para gerar resposta Order

## 🔄 Fluxo Esperado

```
HLAB envia Query (Q):
  "Quais exames para paciente 14102025588?"
     ↓
PKL Bridge recebe via TCP
     ↓
PKL Bridge busca na API VIDA
     ↓
API VIDA retorna: TCO, PCR, GLUC
     ↓
PKL Bridge gera Order (O) ASTM
     ↓
PKL Bridge envia via TCP para HLAB
     ↓
HLAB exibe exames na tela
```

## ✅ Solução Implementada

Já temos os componentes necessários:

### 1. **TcpServer** ✅
- Já implementado
- Porta 8081 funcionando
- Pode enviar e receber dados

### 2. **ExamRequestService** ✅
- Já implementado
- Busca exames na API VIDA
- Gera mensagem ASTM Order

### 3. **AstmSessionManager** ✅
- Já implementado
- Gerencia sessão ASTM
- Envia mensagens com ENQ/ACK/EOT

## 🔧 O que Precisa Ser Feito

### Opção 1: Usar o ConsoleWorker (Mais Simples)

O ConsoleWorker já tem o TcpServer configurado e rodando. Você pode:

1. **Usar o Menu Interativo**
   ```bash
   dotnet run --project PklBridge.ConsoleApp
   Opção: 1 (Menu Interativo)
   Opção: 2 (Solicitar Exames para Paciente)
   ```

2. **Digitar o ID do paciente**: 14102025588

3. **O sistema vai**:
   - Buscar exames na API VIDA
   - Gerar mensagem ASTM Order
   - Enviar via TCP para HLAB

### Opção 2: Integrar TcpServer no Fluxo Automático

Para que funcione automaticamente quando HLAB enviar Query:

1. **Conectar TcpServer ao SerialBridge**
2. **Adicionar handler para Query no MessageProcessor**
3. **Usar ExamRequestService para processar Query**

## 🚀 Teste Rápido (Solução Temporária)

### Passo 1: Executar PKL Bridge
```bash
dotnet run --project PklBridge.ConsoleApp
```

### Passo 2: Escolher Menu Interativo
```
Opção: 1
```

### Passo 3: Solicitar Exames
```
Opção: 2 (Solicitar Exames para Paciente)
```

### Passo 4: Digitar ID do Paciente
```
Digite o ID do paciente: 14102025588
```

### Passo 5: Sistema Vai:
- Buscar exames na API VIDA (ou mock)
- Gerar mensagem ASTM Order
- Tentar enviar via TCP

## 📋 Fluxo Atual vs Esperado

### Fluxo Atual (Não Funciona):
```
HLAB → TCP Query → PKL Bridge → ❌ Nada acontece
```

### Fluxo Esperado:
```
HLAB → TCP Query → PKL Bridge → API VIDA → Order → TCP → HLAB
```

## 🎯 Solução Definitiva

Para implementar a solução completa, seria necessário:

1. **Modificar ConsoleWorker** para escutar TcpServer.DataReceived
2. **Parsear mensagem Query** recebida
3. **Extrair ID do paciente** da Query
4. **Chamar ExamRequestService** para buscar e enviar exames
5. **Responder via TcpServer** para o HLAB

## 💡 Workaround Atual

Enquanto a integração automática não está implementada:

1. **Quando HLAB enviar Query**:
   - Anote o ID do paciente (ex: 14102025588)

2. **No PKL Bridge**:
   - Use o Menu Interativo
   - Opção 2: Solicitar Exames
   - Digite o ID do paciente

3. **Sistema vai processar** e tentar enviar para HLAB

## 🔍 Verificação

Para confirmar que está funcionando:

1. **Logs do PKL Bridge** devem mostrar:
   ```
   [INF] 🔍 Solicitando exames para paciente 14102025588
   [INF] ✅ Encontrados X exames
   [INF] 📤 Enviando mensagem ASTM
   ```

2. **HLAB deve receber** a mensagem Order com os exames

## 📞 Próximos Passos

Para implementar a solução automática completa:

1. Modificar ConsoleWorker para processar Query automaticamente
2. Integrar TcpServer com ExamRequestService
3. Testar fluxo completo Query → Order

---

**Status**: Sistema funcional mas requer intervenção manual para processar Query do HLAB
