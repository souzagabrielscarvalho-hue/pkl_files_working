# 🔍 GUIA DE CAPTURA - HLAB para VIDA

## ✅ Status da Configuração

- ✅ **HLAB configurado** para enviar dados via COM3
- ✅ **Monitor configurado** para escutar COM3
- ✅ **Baud Rate**: 19200 (padrão ASTM)
- ✅ **Script de captura** criado

## 🚀 Como Capturar Dados

### **Método 1: Script Automatizado**
```bash
# Execute o arquivo .bat
.\iniciar-captura.bat
```

### **Método 2: Manual**
```bash
# Execute diretamente
dotnet run
# Escolha opção: 1. Iniciar Monitor Serial
```

## 📋 Procedimento de Teste

### **Passo 1: Iniciar Captura**
1. Execute `.\iniciar-captura.bat`
2. Aguarde aparecer: "Aguardando dados da PKL..."
3. Monitor ficará escutando na COM3

### **Passo 2: Gerar Dados no HLAB**
Execute no HLAB operações que enviam dados para VIDA:
- Envio de resultados de exames
- Consultas de pacientes
- Solicitações de exames
- Qualquer operação que use COM3

### **Passo 3: Observar Captura**
No monitor você verá algo como:
```
[11:45:30.123] RX ← PKL: <dados capturados>
📋 ASTM Analysis #1:
   Frame: 1
   Type: H
   Checksum: CC (Valid)
```

### **Passo 4: Parar Captura**
- Pressione **'q'** para parar o monitor
- Dados salvos automaticamente

## 📁 Onde Encontrar os Logs

### **Logs Detalhados:**
- `logs/pkl-serial-YYYY-MM-DD.txt` - Análise completa
- `logs/pkl-serial-.log` - Log estruturado

### **Conteúdo dos Logs:**
- ⏰ Timestamp de cada mensagem
- 📄 Dados brutos (texto original)
- 🔍 Hex dump (representação hexadecimal) 
- 📊 ASCII codes (códigos numéricos)
- 🧬 Análise ASTM (se aplicável)

## 🎯 O que Procurar nos Dados

### **Protocolos Possíveis:**
1. **ASTM E1394-97** (protocolo PKL padrão)
2. **HL7** (padrão hospitalar)
3. **Texto simples** (CSV, pipe-delimited)
4. **Protocolo proprietário** do HLAB

### **Estruturas Típicas:**
```
# ASTM
<STX>1H|\^&|||HLAB|||||Host|||1|20250709<CR><ETX>CC<CR><LF>

# HL7  
MSH|^~\&|HLAB|LAB|VIDA|HIS|20250709||ACK|1|P|2.5

# Texto
"ID=12345,NOME=João,EXAME=HEMOGRAMA,RESULTADO=Normal"
```

## 🔧 Troubleshooting

### **Nenhum Dado Capturado?**
- ✅ Verificar se HLAB está enviando para COM3
- ✅ Confirmar se operação no HLAB gera saída
- ✅ Testar com diferentes tipos de operação

### **Erro "Port in Use"?**
- ❌ Outro programa usando COM3
- 🔧 Fechar VIDA temporariamente
- 🔧 Ou usar COM0COM para criar bridge

### **Dados Truncados?**
- 🔧 Ajustar ReadTimeout no appsettings.json
- 🔧 Verificar baud rate (testar 9600, 19200, 38400)

## 📊 Próximos Passos

### **Após Capturar Dados:**
1. **Analisar logs** para entender protocolo
2. **Identificar formato** dos dados HLAB→VIDA
3. **Mapear campos** (ID paciente, resultados, etc.)
4. **Implementar parser** específico
5. **Criar API integration** com sistema VIDA

### **Implementar Windows Service:**
1. **Baseado nos dados capturados**
2. **Parser específico** do protocolo HLAB
3. **API client** para sistema VIDA
4. **Logging e monitoring**

## 🎯 Objetivo Final

Criar serviço que:
- **Intercepta dados** HLAB→VIDA via COM3
- **Processa/valida** informações
- **Envia via API** para sistema VIDA
- **Mantém logs** de todas as operações

**Pronto para capturar! Execute o teste quando tiver dados do HLAB para enviar.**
