# 🔧 CONFIGURAÇÃO DO HLAB PARA PKL BRIDGE

**Data**: 15/01/2025  
**Sistema**: HLAB (Interface Laboratorial)

## 📊 Análise da Interface HLAB

Pela imagem fornecida, vejo que o HLAB possui:

### ✅ Testes Disponíveis:
- **Bioquímica**: ACIDO_URICO, AEO, ALBUMINA, ALT/TGP, AMILASE, AST/TGO, BILI_DIR, BILI_TT, CALCIO_PP, CALCIO_ARS, CKMB, CKNAC, COLESTEROL, COLINEST, CREAT, FERRITINA, FERRO, FOSFORO_UV, FR, FRUTOS, GAMA_GT, GLICOSE, HDL, LDH, LIPASE_DIR, MAGNESIO, PCR, PROTEINAS_TT, PROTURI, TRIGL, UREIA_UV
- **Monotestes**: ALT/TGP_MONO, AST/TGO_MONO, CREAT_MONO, FOSF_ALC_MONO, GAMA_GT_MONO, HDL_MONO, MAGNESIO_MONO, UREIA_UV_MONO

### 📋 Informações Visíveis:
- Data: 15/10/2025
- Amostra ID: 1
- Tipo: TUBO
- Tipo Amostra: SORO
- Lista de trabalho com testes e posições

## 🔌 Configuração da Comunicação

### **Passo 1: Acessar Configurações do HLAB**

No menu do HLAB, procure por:
- **Configurações** ou **Settings**
- **Comunicação** ou **Communication**
- **Interface** ou **LIS Interface**
- **Conexão Externa** ou **External Connection**

### **Passo 2: Configurar Saída de Dados**

Configure o HLAB para enviar dados para o PKL Bridge:

#### Opção A: TCP/IP (Recomendado)
```
Tipo de Conexão: TCP/IP Client
Host/IP: 127.0.0.1
Porta: 8081
Protocolo: ASTM E1394-97
Modo: Bidirecional
```

#### Opção B: Named Pipe
```
Tipo de Conexão: Named Pipe
Pipe Name: \\.\pipe\pkl_serial
Protocolo: ASTM E1394-97
Modo: Bidirecional
```

### **Passo 3: Configurar Protocolo ASTM**

Certifique-se que o HLAB está configurado para:
```
Protocolo: ASTM E1394-97
Baud Rate: 19200 (se serial)
Data Bits: 8
Parity: None
Stop Bits: 1
Handshake: None
Frame Delimiter: STX/ETX
Checksum: Habilitado
```

### **Passo 4: Configurar Envio Automático**

Configure o HLAB para enviar dados automaticamente:
```
Envio Automático: Habilitado
Enviar ao Finalizar: Sim
Enviar Resultados: Sim
Formato: ASTM
```

## 🧪 Mapeamento de Códigos de Teste

O HLAB usa códigos específicos. Aqui está o mapeamento para o PKL Bridge:

### Testes de Bioquímica:
| HLAB | PKL Bridge | Descrição |
|------|------------|-----------|
| ACIDO_URICO | URIC | Ácido Úrico |
| ALBUMINA | ALB | Albumina |
| ALT/TGP | ALT | Alanina Aminotransferase |
| AST/TGO | AST | Aspartato Aminotransferase |
| BILI_DIR | DB | Bilirrubina Direta |
| BILI_TT | TB | Bilirrubina Total |
| COLESTEROL | CHOL | Colesterol Total |
| CREAT | CREA | Creatinina |
| GLICOSE | GLUC | Glicose |
| HDL | HDL | HDL Colesterol |
| LDH | LDH | Lactato Desidrogenase |
| PCR | PCR | Proteína C Reativa |
| PROTEINAS_TT | TP | Proteína Total |
| TRIGL | TG | Triglicerídeos |
| UREIA_UV | BUN | Ureia |

### Testes de Urina (se aplicável):
| HLAB | PKL Bridge | Descrição |
|------|------------|-----------|
| PROTEIN | PROTEIN | Proteína |
| GLUCOSE | GLUCOSE | Glicose |
| KETONES | KETONES | Cetonas |
| BLOOD | BLOOD | Sangue |
| NITRITE | NITRITE | Nitrito |
| LEUKOCYTES | LEUKOCYTES | Leucócitos |
| SG | SG | Densidade Específica |
| PH | PH | pH |

## 🚀 Teste de Conexão

### **Passo 1: Iniciar PKL Bridge**
```bash
cd E:\Projetos\Andrey\Projetos\InterfacePKL
dotnet run --project PklBridge.ConsoleApp
```

Você deve ver:
```
✅ PKL Bridge iniciado com sucesso!
🌐 TCP Server ativo na porta 8081
🔌 Aguardando conexão do HLAB...
```

### **Passo 2: Conectar HLAB**

No HLAB:
1. Salve as configurações de comunicação
2. Reinicie o serviço de comunicação (se necessário)
3. Teste a conexão

### **Passo 3: Verificar Conexão**

No console do PKL Bridge, você deve ver:
```
[HH:mm:ss INF] 🔗 Nova conexão TCP aceita de 127.0.0.1:XXXXX
[HH:mm:ss INF] 📡 Processando dados do cliente 127.0.0.1:XXXXX
```

## 📤 Teste de Envio de Dados

### **Teste 1: Enviar Resultado Manual**

No HLAB:
1. Selecione uma amostra da lista de trabalho
2. Insira resultados manualmente (para teste)
3. Clique em "Enviar" ou "Send Results"

### **Teste 2: Processar Amostra Real**

1. Coloque a amostra no PKL 125
2. Escaneie o código de barras
3. Aguarde o processamento
4. HLAB receberá os resultados
5. HLAB enviará para PKL Bridge automaticamente

## 🔍 Monitoramento

### No PKL Bridge:
```
[HH:mm:ss INF] 📨 Dados recebidos via TCP: 256 bytes
[HH:mm:ss INF] 📄 Mensagem ASTM recebida
[HH:mm:ss INF] 🔬 Processando resultados
[HH:mm:ss INF] ✅ Resultados enviados para API VIDA
```

### No HLAB:
- Verifique o log de comunicação
- Confirme que dados foram enviados
- Verifique se recebeu ACK do PKL Bridge

## 🔧 Troubleshooting

### Problema: HLAB não conecta

**Verificar:**
1. PKL Bridge está rodando?
2. Porta 8081 está livre?
3. Firewall bloqueando?
4. IP e porta corretos no HLAB?

**Solução:**
```bash
# Verificar se porta está em uso
netstat -ano | findstr :8081

# Liberar porta no firewall
netsh advfirewall firewall add rule name="PKL Bridge" dir=in action=allow protocol=TCP localport=8081
```

### Problema: Dados não chegam

**Verificar:**
1. HLAB está enviando? (verificar log do HLAB)
2. Protocolo ASTM configurado?
3. PKL Bridge está recebendo? (verificar logs)

**Solução:**
- Ative "LogAllTraffic: true" no appsettings.json
- Verifique logs detalhados em `logs/pkl-bridge-console-*.log`

### Problema: Códigos de teste não reconhecidos

**Verificar:**
1. Mapeamento de códigos está correto?
2. API VIDA reconhece os códigos?

**Solução:**
- Ajuste o mapeamento no `AstmMessageBuilder.cs`
- Consulte documentação da API VIDA

## 📋 Checklist de Configuração

- [ ] HLAB configurado para TCP 127.0.0.1:8081
- [ ] Protocolo ASTM E1394-97 selecionado
- [ ] Envio automático habilitado
- [ ] PKL Bridge rodando
- [ ] Conexão estabelecida (verificar logs)
- [ ] Teste de envio manual bem-sucedido
- [ ] Mapeamento de códigos validado

## 🎯 Próximos Passos

Após configurar o HLAB:

1. **Teste com dados mockados** no PKL Bridge
2. **Teste envio manual** do HLAB
3. **Teste com amostra real** no PKL 125
4. **Valide resultados** no Sistema VIDA
5. **Documente configurações** finais

## 📞 Suporte

Se precisar de ajuda:
1. Capture screenshot das configurações do HLAB
2. Copie os logs do PKL Bridge
3. Anote mensagens de erro
4. Documente o comportamento observado

---

**O PKL Bridge está pronto para receber dados do HLAB!** 🚀
