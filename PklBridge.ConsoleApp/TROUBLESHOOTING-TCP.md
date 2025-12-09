# 🔧 TROUBLESHOOTING - "Can not connect to the server"

## 🚨 **Problema:**
HLAB não consegue conectar no TCP Server (porta 8080)

## 🔍 **DIAGNÓSTICOS OBRIGATÓRIOS:**

### **1. Verificar se PKL Bridge está executando:**
Execute `iniciar-teste.bat` e confirme se você vê:
```
✅ PKL Bridge iniciado com sucesso!
🌐 TCP SERVER ATIVO na porta 8080
```

### **2. Verificar se porta 8080 está ativa:**
```bash
# No cmd/PowerShell:
netstat -an | findstr 8080
```

**Resultado esperado:**
```
TCP    0.0.0.0:8080           0.0.0.0:0              LISTENING
```

**Se NÃO aparecer:** O TCP Server não iniciou.

### **3. Testar conectividade manual:**
```bash
# Teste simples:
telnet localhost 8080
```

**Se conectar:** TCP Server funciona  
**Se falhar:** Problema de configuração

## 🔧 **SOLUÇÕES POR CENÁRIO:**

### **Cenário A: TCP Server não inicia**

**1. Execute como Administrador:**
- Clique direito em `iniciar-teste.bat`
- Selecione "Executar como administrador"

**2. Verifique logs para erros:**
Procure por mensagens de erro como:
```
[ERR] Erro ao iniciar TCP Server na porta 8080
```

**3. Tente porta alternativa:**
Edite `appsettings.json`:
```json
{
  "BridgeSettings": {
    "TcpPort": 8081  ← Mude para 8081
  }
}
```

### **Cenário B: Firewall bloqueando**

**1. Windows Defender:**
```
- Abra Windows Defender Firewall
- Clique "Permitir um aplicativo..."
- Adicione PklBridge.ConsoleApp.exe
- Marque "Privado" e "Público"
```

**2. Firewall de terceiros:**
- Temporariamente desabilite antivírus/firewall
- Teste novamente
- Se funcionar, adicione exceção permanente

### **Cenário C: Configuração HLAB incorreta**

**1. Teste configurações alternativas no HLAB:**

**Opção 1:**
```
Servidor IP: 127.0.0.1
Porta: 8080
```

**Opção 2:**
```
Servidor IP: localhost
Porta: 8080
```

**Opção 3 (se mudou porta):**
```
Servidor IP: 127.0.0.1
Porta: 8081
```

### **Cenário D: Problema de binding IP**

**Modifique TcpServer para usar localhost:**

Edite `TcpServer.cs` (temporariamente):
```csharp
// ANTES:
_listener = new TcpListener(IPAddress.Any, _settings.TcpPort);

// DEPOIS (teste):
_listener = new TcpListener(IPAddress.Loopback, _settings.TcpPort);
```

## 🧪 **TESTE PASSO-A-PASSO:**

### **1. Teste básico de rede:**
```bash
ping localhost
ping 127.0.0.1
```
Ambos devem responder.

### **2. Teste de porta específica:**
```bash
# PowerShell:
Test-NetConnection -ComputerName localhost -Port 8080
```

### **3. Verificar processos usando porta 8080:**
```bash
netstat -ano | findstr 8080
```

Se outro processo estiver usando, termine-o ou mude a porta.

## ⚡ **SOLUÇÕES RÁPIDAS:**

### **Solução 1: Reiniciar tudo**
1. Feche PKL Bridge (Ctrl+C)
2. Feche HLAB
3. Execute PKL Bridge como Administrador
4. Configure HLAB novamente
5. Teste

### **Solução 2: Usar IP específico**
1. Descubra seu IP: `ipconfig`
2. Use o IP real em vez de 0.0.0.0
3. Configure HLAB com esse IP

### **Solução 3: Porta alternativa**
1. Mude para porta 8081, 8082, etc.
2. Rebuild: `dotnet build --configuration Release`
3. Teste novamente

## 📋 **CHECKLIST DE VERIFICAÇÃO:**

- [ ] PKL Bridge executando como Administrador
- [ ] Mensagem "TCP SERVER ATIVO" aparece
- [ ] `netstat` mostra porta 8080 LISTENING
- [ ] Firewall permite conexões
- [ ] HLAB configurado com IP correto
- [ ] Ping para localhost funciona
- [ ] Nenhum outro programa usa porta 8080

## 🆘 **SE NADA FUNCIONAR:**

### **Alternativa 1: Named Pipe**
Volte para Named Pipe (funcionava antes):
```json
{
  "BridgeSettings": {
    "EnableTcpServer": false,
    "EnableHardwareBridge": false
  }
}
```
Configure HLAB: `\\.\pipe\pkl_serial`

### **Alternativa 2: COM Bridge**
Use COM2 bridge:
```json
{
  "BridgeSettings": {
    "EnableTcpServer": false,
    "EnableHardwareBridge": true,
    "RealComPort": "COM2"
  }
}
```

---

## 🔍 **PRÓXIMOS PASSOS:**

1. **Execute diagnósticos acima**
2. **Anote qual cenário se aplica**
3. **Aplique solução correspondente**
4. **Teste novamente**

**Reporte qual diagnóstico falhou para ajuda específica!**
