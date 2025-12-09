# 🔧 PKL Bridge - Guia de Diagnóstico

## ⚠️ Problema Atual
O HLAB está comunicando com a **COM2**, mas o PKL Bridge não está recebendo dados porque espera dados via **Named Pipe**.

## 🎯 Solução Implementada

A aplicação console agora monitora **AMBOS**:
- ✅ **Named Pipe** (`\\.\pipe\pkl_serial`) - onde o PKL Bridge espera dados
- ✅ **COM2** - onde o HLAB ainda pode estar enviando dados

## 🚀 Como Executar o Diagnóstico

### Passo 1: Execute a aplicação
```bash
cd PklBridge.ConsoleApp
iniciar-teste.bat
```

### Passo 2: O que você verá
```
🚀 PKL Bridge - Aplicação de Console para Testes
===============================================

✅ PKL Bridge iniciado com sucesso!
📡 Named Pipe ativo: \\.\pipe\pkl_serial
🔍 MONITORAMENTO ATIVO: Verificando se HLAB envia dados para COM2
   Se aparecer um alerta, significa que HLAB ainda está usando COM2!

🔌 Aguardando conexão do HLAB...
```

## 🔍 Cenários de Diagnóstico

### Cenário A: HLAB usando COM2 (problema atual)
**O que acontece:**
```
🚨 ATENÇÃO: Dados detectados na COM2!
   Bytes: 45 | Hex: 48454C4C4F...
   O HLAB está enviando para COM2, mas o PKL Bridge espera Named Pipe!
```

**Solução:** Você precisa configurar o HLAB para usar Named Pipe

### Cenário B: HLAB usando Named Pipe (correto)
**O que acontece:**
```
📨 Mensagem recebida de NamedPipe: 45 bytes
🔄 Parsed 1 ASTM messages
MOCK: Sending batch with 3 results
```

**Status:** ✅ Funcionando corretamente!

### Cenário C: Nenhum dado recebido
**O que acontece:**
- Console fica "quieto", sem alertas
- Apenas logs de health check a cada 2 minutos

**Possíveis causas:**
- HLAB não está enviando dados
- Porta/pipe configurado incorretamente
- Problema na PKL 125

## ⚙️ Como Configurar o HLAB

### Opção 1: Configurar HLAB para Named Pipe (Recomendado)
1. Acesse configurações do HLAB
2. Vá para **Comunicação/Serial/RS232**
3. Altere de:
   ```
   Porta: COM2
   ```
   Para:
   ```
   Porta: \\.\pipe\pkl_serial
   ```

### Opção 2: Configurar PKL Bridge para COM2 (Alternativo)
Se não conseguir alterar o HLAB, modifique `appsettings.json`:
```json
{
  "BridgeSettings": {
    "EnableHardwareBridge": true,
    "RealComPort": "COM2"
  }
}
```

## 🧪 Testes de Verificação

### Teste 1: Verificar se COM2 está funcionando
```bash
# No cmd, teste se COM2 existe
mode COM2
```

### Teste 2: Executar exame na PKL 125
1. Execute a aplicação console
2. Na PKL 125, execute um hemograma
3. Observe os logs no console
4. Verifique se aparecem alertas sobre COM2

### Teste 3: Simular dados na COM2
Use um software de terminal serial para enviar dados de teste para COM2

## 📋 Checklist de Diagnóstico

- [ ] PKL Bridge Console App executando
- [ ] Named Pipe ativo (`\\.\pipe\pkl_serial`)
- [ ] Monitoramento COM2 ativo
- [ ] Exame executado na PKL 125
- [ ] Verificar se há alertas sobre dados na COM2
- [ ] Verificar logs em tempo real

## 🎯 Resultados Esperados

### Se HLAB usa COM2:
- ❌ Nenhum dado no Named Pipe
- 🚨 Alertas sobre dados na COM2
- **Ação:** Reconfigurar HLAB

### Se HLAB usa Named Pipe:
- ✅ Dados recebidos via Named Pipe
- 🔄 Processamento ASTM
- 📤 Envio para API (mock)

## 💡 Dicas Importantes

1. **Execute como Administrador** se houver problemas de permissão
2. **Mantenha o console aberto** durante os testes
3. **Execute um exame completo** na PKL 125, não apenas calibração
4. **Observe AMBOS** os monitores (Named Pipe e COM2)
5. **Verifique os logs de arquivo** em `logs/` se necessário

## 🆘 Se Ainda Assim Não Funcionar

1. Verifique se a PKL 125 está configurada corretamente para enviar dados
2. Confirme o protocolo ASTM E1394-97 está habilitado
3. Teste com um software de terminal serial primeiro
4. Verifique se não há outros programas usando COM2
5. Considere usar um cabo serial null-modem se necessário

---

**Execute agora: `iniciar-teste.bat` e observe os resultados!**
