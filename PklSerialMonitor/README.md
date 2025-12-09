# PKL Serial Monitor

Monitor de comunicação serial para capturar e analisar a comunicação da máquina PKL 125 de hemograma usando protocolo ASTM E1394-97.

## 🎯 Objetivo

Este monitor funciona como um "sniffer" serial que captura **toda a comunicação** entre a máquina PKL 125 e outros sistemas, permitindo:
- Analisar o protocolo ASTM E1394-97 em tempo real
- Capturar mensagens de solicitação e resultado
- Validar checksums e estrutura de dados
- Testar comunicação com a PKL
- Gerar logs detalhados para análise

## 📋 Funcionalidades

- ✅ **Monitor em Tempo Real**: Captura todas as mensagens TX/RX
- ✅ **Análise ASTM**: Parser completo do protocolo E1394-97
- ✅ **Validação de Checksum**: Verificação automática de integridade
- ✅ **Logs Detalhados**: Console + arquivo TXT com hex dump
- ✅ **Interface Colorida**: Console com cores para melhor visualização
- ✅ **Teste de Comunicação**: Envio de comandos ENQ para teste
- ✅ **Configuração Flexível**: appsettings.json configurável
- ✅ **Detecção de Portas**: Lista portas seriais disponíveis

## 🚀 Como Usar

### 1. Compilar e Executar

```bash
# Compilar
dotnet build

# Executar
dotnet run
```

### 2. Menu Principal

O monitor apresenta um menu interativo:

```
MENU PRINCIPAL
1. Iniciar Monitor Serial
2. Testar Comunicação
3. Enviar Comando ASTM
4. Verificar Configuração
5. Ver Logs
6. Sair
```

### 3. Configurar Conexão

1. **Configure a porta serial** no `appsettings.json`
2. **Conecte a PKL 125** na porta configurada (padrão: COM2)
3. **Inicie o monitor** (opção 1)
4. **Execute operações** na PKL para capturar tráfego

## 🔧 Configuração

### appsettings.json

```json
{
  "Serial": {
    "PortName": "COM2",
    "BaudRate": 19200,
    "DataBits": 8,
    "Parity": "None",
    "StopBits": "One",
    "Handshake": "None",
    "ReadTimeout": 5000,
    "WriteTimeout": 5000
  },
  "Logging": {
    "LogLevel": "Debug",
    "LogToFile": true,
    "LogToConsole": true,
    "LogDirectory": "logs"
  },
  "ASTM": {
    "ValidateChecksum": true,
    "ShowHexDump": true,
    "ShowAsciiCodes": true,
    "ParseRecords": true
  }
}
```

### Configurações da PKL 125

Baseado na documentação ASTM E1394-97:
- **Baud Rate**: 19200 (padrão)
- **Data Bits**: 8
- **Parity**: None
- **Stop Bits**: 1
- **Porta**: COM2 (configurável)

## 📊 Saída do Monitor

### Console (Tempo Real)
```
[09:45:30.123] RX ← PKL: <ENQ>
[09:45:30.145] TX → PKL: <ACK>
[09:45:30.200] RX ← PKL: <STX>1H|\^&|||PPC 125|||||Host|||1|20250709094530<CR><ETX>CC<CR><LF>

📋 ASTM Analysis #1:
   Frame: 1
   Type: H
   Checksum: CC (Valid)
   Content: Header - Sender: PPC 125, Receiver: Host
```

### Arquivo de Log (Detalhado)
```
================================================================================
PKL SERIAL MESSAGE #1
Timestamp: 2025-07-09 09:45:30.200
Direction: RX (Received from PKL)
================================================================================

📄 RAW DATA:
   Length: 45 bytes
   Content: <STX>1H|\^&|||PPC 125|||||Host|||1|20250709094530<CR><ETX>CC<CR><LF>

🔍 HEX DUMP:
   02 31 48 7C 5C 5E 26 7C 7C 7C 50 50 43 20 31 32 35 7C 7C 7C 7C 7C 48 6F 73 74 7C 7C 7C 31 7C 32 30 32 35 30 37 30 39 30 39 34 35 33 30 0D 03 43 43 0D 0A

📊 ASCII CODES:
   002 049 072 124 092 094 038 124 124 124 080 080 067 032 049 050 053 124 124 124 124 124 072 111 115 116 124 124 124 049 124 050 048 050 053 048 055 048 057 048 057 052 053 051 048 013 003 067 067 013 010

🧬 ASTM ANALYSIS:
   Frame Number: 1
   Record Type: H
   Checksum: CC
   Valid Checksum: True
   Parsed Content: Header - Sender: PPC 125, Receiver: Host
================================================================================
```

## 🧪 Testes

### 1. Teste de Conectividade
- Opção 2 do menu principal
- Envia comando ENQ para PKL
- Verifica se PKL responde com ACK

### 2. Monitor Passivo
- Opção 1 do menu principal
- Captura toda comunicação entre PKL e outros sistemas
- Ideal para analisar protocolos existentes

### 3. Validação de Protocolo
- Análise automática de mensagens ASTM
- Validação de checksums
- Identificação de tipos de registro (H, P, O, R, Q, L)

## 📋 Tipos de Registro ASTM

### Suportados pela PKL 125:
- **H**: Header Record - Cabeçalho da mensagem
- **P**: Patient Record - Informações do paciente
- **O**: Order Record - Solicitação de exame
- **R**: Result Record - Resultado do exame
- **Q**: Query Record - Consulta de informações
- **L**: Terminator Record - Terminador da mensagem

### Exemplo de Fluxo Típico:
```
1. ENQ (Enquiry) → ACK
2. STX + H (Header) + ETX + Checksum → ACK
3. STX + P (Patient) + ETX + Checksum → ACK
4. STX + O (Order) + ETX + Checksum → ACK
5. STX + L (Terminator) + ETX + Checksum → ACK
6. EOT (End of Transmission)
```

## 🔍 Análise de Dados

### Protocolo ASTM E1394-97
- **STX** (0x02): Início do frame
- **Frame Number**: 1-7, depois 0, 1...
- **Record Type**: H, P, O, R, Q, L
- **Field Delimiter**: | (pipe)
- **ETX** (0x03): Fim do frame
- **Checksum**: 2 bytes hex (modulo 256)
- **CR + LF**: Terminador de linha

### Validação de Checksum
O monitor calcula automaticamente o checksum e compara com o recebido:
```
Checksum = (sum of all bytes from Frame Number to ETX) mod 256
```

## 📁 Estrutura de Logs

### Arquivos Gerados:
- `logs/pkl-serial-YYYY-MM-DD.txt` - Log detalhado das mensagens
- `logs/pkl-serial-.log` - Log estruturado do Serilog

### Informações Capturadas:
- **Timestamp**: Momento exato da mensagem
- **Direction**: TX (enviado) ou RX (recebido)
- **Raw Data**: Conteúdo original da mensagem
- **Hex Dump**: Representação hexadecimal
- **ASCII Codes**: Códigos ASCII de cada byte
- **ASTM Analysis**: Parser estruturado do protocolo

## 🚨 Troubleshooting

### Porta Serial Não Encontrada
```
❌ Porta configurada COM2 não encontrada!
```
**Solução**: 
1. Verifique se a porta COM2 existe
2. Altere `appsettings.json` para porta correta
3. Use opção 4 do menu para verificar configuração

### Erro ao Abrir Porta
```
❌ Erro ao iniciar monitor: Access to the port 'COM2' is denied
```
**Solução**:
1. Feche outros programas usando a porta
2. Execute como Administrador
3. Verifique drivers da porta serial

### Nenhuma Mensagem Capturada
```
Aguardando dados da PKL...
```
**Solução**:
1. Verifique se PKL está conectada na porta correta
2. Configure PKL para modo Serial (não TCP/IP)
3. Execute operações na PKL para gerar tráfego
4. Use opção 2 para testar comunicação

## 🎯 Próximos Passos

Após capturar a comunicação da PKL:

1. **Analisar Logs**: Identificar padrões de mensagens
2. **Documentar Protocolo**: Criar especificação baseada nos achados
3. **Implementar Cliente**: Criar simulador baseado nos dados reais
4. **Integrar com Sistema VIDA**: Usar dados para implementar interface

## 📊 Cenários de Uso

### Para Desenvolvimento:
- Capturar comunicação entre PKL e sistema existente
- Analisar estrutura de dados real
- Validar implementação de protocolo

### Para Troubleshooting:
- Identificar problemas de comunicação
- Validar checksums e formatação
- Analisar timing de mensagens

### Para Documentação:
- Gerar especificações baseadas em dados reais
- Criar exemplos de uso do protocolo
- Validar conformidade com ASTM E1394-97

Este monitor é a ferramenta **essencial** para entender como implementar a comunicação bidirecional com a PKL 125 de forma correta e confiável.
