# 🎯 SOLUÇÃO DEFINITIVA - PKL Bridge + HLAB

## 💡 **PROBLEMA RAIZ IDENTIFICADO:**
No Windows, **apenas UMA aplicação pode usar COM2 por vez**:
- Se **PKL Bridge** usar COM2 → HLAB dá erro "porta Com não pode ser acessada"
- Se **HLAB** usar COM2 → PKL Bridge não recebe dados

## ✅ **SOLUÇÃO FINAL:**

### **PKL Bridge**: Usar Named Pipe APENAS
### **HLAB**: Configurar para usar Named Pipe em vez de COM2

## 🚀 **IMPLEMENTAÇÃO:**

### **1. PKL Bridge já está configurado corretamente:**
```json
{
  "BridgeSettings": {
    "EnableHardwareBridge": false,  ← SÓ NAMED PIPE
    "PipeName": "pkl_serial"
  }
}
```

### **2. Configure HLAB para Named Pipe:**

**No HLAB, altere a configuração de comunicação:**
```
❌ ANTES: 
   Porta: COM2

✅ AGORA:
   Porta: \\.\pipe\pkl_serial
```

## 📋 **PASSOS EXATOS:**

### **Passo 1: Execute PKL Bridge**
```bash
cd PklBridge.ConsoleApp
iniciar-teste.bat
```

**Você deve ver:**
```
✅ PKL Bridge iniciado com sucesso!
📡 Named Pipe ativo: \\.\pipe\pkl_serial
🔍 MONITORAMENTO ATIVO: Verificando se HLAB envia dados para COM2
   Se aparecer um alerta, significa que HLAB ainda está usando COM2!
```

### **Passo 2: Configure HLAB**
1. Abra **HLAB**
2. Vá para **Configurações → Comunicação**
3. **Altere de:**
   ```
   Porta Serial: COM2
   ```
4. **Para:**
   ```
   Named Pipe: \\.\pipe\pkl_serial
   ```

### **Passo 3: Teste**
1. Execute um **hemograma na PKL 125**
2. **HLAB deve conectar SEM ERRO**
3. **PKL Bridge deve mostrar:**
   ```
   📨 Mensagem recebida de NamedPipe: XXX bytes  ← SUCESSO!
   ```

## 🔍 **DIAGNÓSTICO:**

### **✅ Se Funcionar:**
```
[INF] 📨 Mensagem recebida de NamedPipe: 156 bytes
[INF] 🔄 Parsed 1 ASTM messages
[INF] MOCK: Sending batch with results
```

### **❌ Se HLAB ainda usar COM2:**
```
🚨 ATENÇÃO: Dados detectados na COM2!
   O HLAB está enviando para COM2, mas o PKL Bridge espera Named Pipe!
```
**Solução:** Reconfigure HLAB para usar `\\.\pipe\pkl_serial`

### **❌ Se Nada Acontecer:**
- Verifique se HLAB está configurado para `\\.\pipe\pkl_serial`
- Certifique-se que protocolo é ASTM E1394-97
- Execute exame COMPLETO, não apenas calibração

## 🎯 **CONFIGURAÇÕES FINAIS:**

### **HLAB:**
```
Comunicação: Named Pipe
Caminho: \\.\pipe\pkl_serial
Protocolo: ASTM E1394-97
```

### **PKL Bridge:**
```
Named Pipe: pkl_serial (já configurado)
Hardware Bridge: Desabilitado (já configurado)
```

## 💪 **VANTAGENS DESTA SOLUÇÃO:**

✅ **Não há conflito de porta** - cada um usa seu canal  
✅ **HLAB funciona normalmente** - sem erro de acesso  
✅ **PKL Bridge recebe dados** - via Named Pipe  
✅ **Processamento ASTM** - funciona perfeitamente  
✅ **API VIDA** - recebe resultados (mock)  

## 🔄 **Fluxo Final:**
```
HLAB → Named Pipe (\\.\pipe\pkl_serial) → PKL Bridge → API VIDA
```

---

## 🚨 **AÇÃO NECESSÁRIA:**

**Configure o HLAB para usar `\\.\pipe\pkl_serial` em vez de COM2!**

**Depois execute `iniciar-teste.bat` e teste um exame!** 🚀
