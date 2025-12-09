# PKL HTTP Monitor

Servidor de monitoramento HTTP para capturar e analisar a comunicação da máquina PKL 125 de hemograma.

## 🎯 Objetivo

Este servidor funciona como um "sniffer" HTTP que captura **todas as requisições** enviadas pela máquina PKL 125, permitindo descobrir:
- Qual método HTTP ela usa (GET, POST, PUT, etc.)
- Quais endpoints/paths ela acessa
- Que headers ela envia
- Qual o formato das mensagens (ASTM, JSON, XML, etc.)
- Como ela estrutura os dados

## 📋 Funcionalidades

- ✅ **Captura Universal**: Intercepta TODAS as requisições HTTP
- ✅ **Múltiplas Portas**: Escuta nas portas 80, 8080 e 5000 simultaneamente
- ✅ **Logs Detalhados**: Salva logs completos em arquivos TXT
- ✅ **Console em Tempo Real**: Mostra informações instantâneas no console
- ✅ **Análise Completa**: Inclui hex dump e ASCII codes das mensagens
- ✅ **Resposta Automática**: Responde com `<ACK>` para manter comunicação ativa

## 🚀 Como Usar

### 1. Compilar e Executar

```bash
# Compilar
dotnet build

# Executar (recomendado como Administrador para porta 80)
dotnet run
```

### 2. Configurar a PKL 125

Na máquina PKL 125:
1. Acesse as configurações de rede/comunicação
2. Configure o modo para **TCP/IP**
3. Configure o **IP do servidor** (onde este monitor está rodando)
4. Configure a **porta** (80, 8080 ou 5000)

### 3. Monitorar Comunicação

O servidor irá:
- Mostrar informações no console em tempo real
- Salvar logs detalhados em `logs/pkl-request-YYYY-MM-DD.txt`
- Responder automaticamente às requisições

## 📁 Estrutura de Logs

### Console
```
[09:15:30.123] 📨 Request #0001 from 192.168.1.50
          POST   /astm (156 bytes)
```

### Arquivo TXT
```
================================================================================
PKL HTTP REQUEST #1
Timestamp: 2025-01-07 09:15:30.123
Client IP: 192.168.1.50
================================================================================

🌐 REQUEST INFO:
   Method: POST
   Path: /astm
   Query: 

📋 HEADERS:
   Content-Type: text/plain
   Content-Length: 156
   Host: 192.168.1.100:80

📄 BODY:
   Length: 156 bytes
   Content: <STX>1H|\^&|||PPC 125|||||Host|||1|20250107091530<CR><ETX>CC<CR><LF>

   Hex Dump:
   02 31 48 7C 5C 5E 26 7C 7C 7C 50 50 43 20 31 32 35 7C 7C 7C 7C 7C 48 6F 73 74 7C 7C 7C 31 7C 32 30 32 35 30 31 30 37 30 39 31 35 33 30 0D 03 43 43 0D 0A

   ASCII Codes:
   002 049 072 124 092 094 038 124 124 124 080 080 067 032 049 050 053 124 124 124 124 124 072 111 115 116 124 124 124 049 124 050 048 050 053 048 049 048 055 048 057 049 053 051 048 013 003 067 067 013 010

📤 RESPONSE: <ACK>
================================================================================
```

## 🔧 Configuração

### Portas Disponíveis
- **80**: Porta padrão HTTP (requer permissão de Administrador)
- **8080**: Porta alternativa comum
- **5000**: Porta de desenvolvimento

### Modificar Resposta
Para alterar a resposta padrão, edite a variável `_responseMessage` no código:

```csharp
private static string _responseMessage = "<ACK>";
```

Exemplos de respostas possíveis:
- `<ACK>` - Acknowledgment padrão
- `OK` - Resposta simples
- `200` - Código HTTP
- `{"status":"ok"}` - JSON

## 🧪 Testes

### Teste Manual
Para testar se o servidor está funcionando:

```bash
# Teste via curl
curl -X POST http://localhost:8080/teste -d "mensagem de teste"

# Teste via PowerShell
Invoke-RestMethod -Uri http://localhost:8080/teste -Method POST -Body "mensagem de teste"
```

### Verificar Logs
Os logs são salvos em:
- `logs/pkl-monitor-YYYY-MM-DD.log` - Log estruturado do Serilog
- `logs/pkl-request-YYYY-MM-DD.txt` - Log detalhado das requisições

## 🎯 Próximos Passos

Após capturar a comunicação da PKL:

1. **Analisar Logs**: Identificar padrões e formato das mensagens
2. **Documentar Protocolo**: Criar especificação baseada nos achados
3. **Implementar Cliente**: Criar client HTTP para comunicação bidirecional
4. **Integrar com Sistema VIDA**: Conectar com a API do sistema VIDA

## 📊 Informações Capturadas

### Dados de Requisição
- **Timestamp**: Momento exato da requisição
- **IP do Cliente**: IP da máquina PKL
- **Método HTTP**: GET, POST, PUT, DELETE, etc.
- **Path/Endpoint**: Caminho acessado
- **Query Parameters**: Parâmetros na URL
- **Headers**: Cabeçalhos HTTP completos
- **Body**: Conteúdo da mensagem

### Análise de Dados
- **Texto Legível**: Conteúdo como texto
- **Hex Dump**: Representação hexadecimal
- **ASCII Codes**: Códigos ASCII de cada byte
- **Estatísticas**: Tamanho, encoding, etc.

## 🚨 Troubleshooting

### Erro de Permissão (Porta 80)
```
❌ Erro ao iniciar servidor: Access denied
💡 Tente executar como Administrador para usar porta 80
```

**Solução**: Execute o prompt de comando como Administrador

### PKL Não Conecta
1. Verifique se a PKL está configurada para TCP/IP
2. Confirme o IP do servidor
3. Teste conectividade com `ping`
4. Verifique firewall do Windows

### Logs Não Aparecem
1. Confirme que o diretório `logs` foi criado
2. Verifique permissões de escrita
3. Monitore o console para erros

## 🔍 Análise Esperada

Com base no protocolo ASTM E1394-97, esperamos capturar:

- **Mensagens de Query**: PKL solicitando informações
- **Mensagens de Result**: PKL enviando resultados
- **Estrutura ASTM**: Registros H, P, O, R, Q, L
- **Checksum**: Verificação de integridade
- **Sequência**: Numeração de frames

Este monitor será fundamental para entender como implementar a comunicação bidirecional no sistema final.
