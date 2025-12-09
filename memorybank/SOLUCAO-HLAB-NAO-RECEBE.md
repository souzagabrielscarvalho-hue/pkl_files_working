# 🔧 SOLUÇÃO: HLAB Não Recebe Dados

**Problema**: Clica em "Receber" no HLAB mas nada acontece  
**Causa**: HLAB não está configurado para se comunicar com o PKL Bridge

## 🎯 Diagnóstico

Pela imagem, vejo que:
- ✅ PKL Bridge está rodando
- ✅ TCP Server ativo na porta 8081
- ❌ HLAB não está conectado ao PKL Bridge
- ❌ Botão "Receber" não funciona

## 🔌 Solução: Configurar Comunicação HLAB

### **Passo 1: Acessar Configurações do HLAB**

No software HLAB, procure por um dos seguintes menus:
- **Configurações** / **Settings** / **Configuração**
- **Comunicação** / **Communication** / **Interface**
- **LIS** / **Interface LIS** / **Conexão Externa**
- **Ferramentas** / **Tools** / **Opções**

Geralmente está em:
- Menu superior: `Arquivo → Configurações`
- Ou: `Ferramentas → Opções`
- Ou: `Configurações → Comunicação`

### **Passo 2: Configurar Porta de Comunicação**

Procure por uma seção chamada:
- **Porta de Comunicação**
- **Interface Externa**
- **LIS Connection**
- **Conexão TCP/IP**

Configure assim:

```
Tipo de Conexão: TCP/IP Client
Host/IP: 127.0.0.1
Porta: 8081
Protocolo: ASTM E1394-97
Modo: Bidirecional
Timeout: 30 segundos
```

### **Passo 3: Configurar Protocolo ASTM**

Na mesma tela ou em uma aba "Protocolo":

```
Protocolo: ASTM E1394-97
Formato de Dados: ASTM
Delimitadores: STX/ETX
Checksum: Habilitado
Frame Size: 240 bytes
```

### **Passo 4: Configurar Envio/Recebimento**

Procure por opções de envio automático:

```
☑ Enviar Resultados Automaticamente
☑ Receber Pedidos Automaticamente
☑ Confirmar Recebimento (ACK)
☑ Modo Bidirecional
```

### **Passo 5: Salvar e Reiniciar**

1. Clique em **Salvar** ou **OK**
2. **Reinicie o serviço de comunicação** do HLAB
   - Ou reinicie o software HLAB completamente
3. Verifique se aparece "Conectado" ou ícone de conexão ativa

## 🧪 Teste de Conexão

### **Teste 1: Verificar Conexão**

Após configurar, verifique no PKL Bridge se aparece:
```
[HH:mm:ss INF] 🔗 Nova conexão TCP aceita de 127.0.0.1:XXXXX
```

Se aparecer, a conexão foi estabelecida! ✅

### **Teste 2: Testar Botão Receber**

1. No HLAB, clique em **Receber**
2. Verifique nos logs do PKL Bridge se aparece:
```
[HH:mm:ss INF] 📨 Dados recebidos via TCP
[HH:mm:ss INF] 📄 Mensagem ASTM recebida
```

## 🔍 Troubleshooting

### Problema 1: Não encontro as configurações

**Solução:**
- Verifique o manual do HLAB
- Procure por um ícone de "engrenagem" ou "configurações"
- Tente clicar com botão direito na tela principal
- Verifique se há um menu "Admin" ou "Administrador"

### Problema 2: Não há opção TCP/IP

**Solução:**
- Verifique se o HLAB suporta TCP/IP
- Pode ser que precise de uma licença adicional
- Tente usar Named Pipe como alternativa:
  ```
  Tipo: Named Pipe
  Nome: \\.\pipe\pkl_serial
  ```

### Problema 3: Configurei mas não conecta

**Verificar:**

1. **PKL Bridge está rodando?**
   ```bash
   # Deve estar rodando
   dotnet run --project PklBridge.ConsoleApp
   ```

2. **Porta 8081 está livre?**
   ```bash
   netstat -ano | findstr :8081
   ```

3. **Firewall bloqueando?**
   ```bash
   # Liberar porta no firewall
   netsh advfirewall firewall add rule name="PKL Bridge" dir=in action=allow protocol=TCP localport=8081
   ```

4. **IP e porta corretos?**
   - IP: `127.0.0.1` (localhost)
   - Porta: `8081`

### Problema 4: Conecta mas botão Receber não funciona

**Possíveis causas:**

1. **Não há dados para receber**
   - O PKL 125 precisa ter enviado resultados primeiro
   - Ou você precisa solicitar exames antes

2. **Protocolo incorreto**
   - Verifique se está configurado como ASTM E1394-97

3. **Modo não bidirecional**
   - Certifique-se que está em modo bidirecional

## 🎯 Fluxo Correto de Uso

### Para RECEBER resultados do PKL 125:

```
1. PKL 125 processa amostra
   ↓
2. PKL 125 envia resultados para HLAB
   ↓
3. HLAB recebe via serial/USB
   ↓
4. Clique em "Receber" no HLAB
   ↓
5. HLAB envia para PKL Bridge via TCP
   ↓
6. PKL Bridge processa e envia para API VIDA
```

### Para ENVIAR pedidos para PKL 125:

```
1. Sistema VIDA registra pedido
   ↓
2. PKL Bridge busca pedido na API
   ↓
3. PKL Bridge gera mensagem ASTM
   ↓
4. PKL Bridge envia para HLAB via TCP
   ↓
5. HLAB recebe e exibe na tela
   ↓
6. Clique em "Enviar" no HLAB
   ↓
7. HLAB envia para PKL 125
```

## 📋 Checklist de Configuração

- [ ] HLAB configurado para TCP 127.0.0.1:8081
- [ ] Protocolo ASTM E1394-97 selecionado
- [ ] Modo bidirecional habilitado
- [ ] Configurações salvas
- [ ] HLAB reiniciado
- [ ] PKL Bridge rodando
- [ ] Conexão estabelecida (verificar logs)
- [ ] Teste de recebimento bem-sucedido

## 🆘 Se Nada Funcionar

### Alternativa 1: Usar Named Pipe

Configure o HLAB para:
```
Tipo: Named Pipe
Nome: \\.\pipe\pkl_serial
```

### Alternativa 2: Usar Porta Serial Virtual

1. Instale um software de porta serial virtual
2. Configure HLAB para usar a porta virtual
3. Configure PKL Bridge para a mesma porta

### Alternativa 3: Verificar Documentação

- Consulte o manual do HLAB
- Entre em contato com suporte do HLAB
- Verifique se há atualizações disponíveis

## 📞 Informações para Suporte

Se precisar contatar o suporte do HLAB, informe:

```
Sistema: HLAB (Interface Laboratorial)
Objetivo: Conectar com sistema externo via TCP/IP
Protocolo: ASTM E1394-97
Configuração desejada:
  - Host: 127.0.0.1
  - Porta: 8081
  - Modo: Bidirecional
```

---

**Após configurar corretamente, o botão "Receber" funcionará!** 🚀
