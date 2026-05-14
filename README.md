# PKL Bridge - Interface PKL Hemograma

Sistema de interfaceamento entre a máquina **PKL 125** (analisador de hemogramas) e o sistema **VIDA** (laboratório), funcionando como bridge entre o software **HLAB** e a **API VIDA**. Comunicação com o HLAB via **TCP** (padrão) ou **Named Pipe**, usando protocolo **ASTM E1394-97**. Envio de resultados via **HTTP** pra API VIDA.

## 🏗️ Arquitetura

```
┌─────────────┐ COM1  ┌─────────────┐  TCP :8081     ┌────────────────────┐  HTTP   ┌──────────────┐
│   PKL 125   │◄─────►│    HLAB     │◄──────────────►│   PKL Bridge       │────────►│ Sistema VIDA │
└─────────────┘       │  (software  │  (ou Named     │  (Windows Service) │  POST   │  (API REST)  │
                      │   análise)  │   Pipe         │                    │         └──────────────┘
                      │             │   \\.\pipe\    │  ┌──────────────┐  │
                      │             │   pkl_serial)  │  │ ASTM Parser  │  │
                      └─────────────┘                │  │ + Session    │  │
                                                     │  └──────┬───────┘  │
                                                     │         ▼          │
                                                     │  ┌──────────────┐  │
                                                     │  │ Result       │  │
                                                     │  │ Processor    │  │  agrupa por tag_id,
                                                     │  └──────┬───────┘  │  envia em batch
                                                     │         ▼          │
                                                     │  ┌──────────────┐  │
                                                     │  │ VidaApiClient│  │  Polly: retry +
                                                     │  │ (Polly)      │  │  circuit breaker
                                                     │  └──────────────┘  │
                                                     └────────────────────┘
```

## 🎯 Componentes

### **PklBridge.Core**

- **Models**: AstmMessage, ExamResult, PatientData, ExamBatch
- **Interfaces**: ISerialBridge, IAstmParser, IVidaApiClient, IMessageProcessor
- **Configuration**: Todas as configurações tipadas (BridgeSettings, VidaApiSettings, etc.)

### **PklBridge.Infrastructure**

- **Transport** (`Serial/`): `TcpServer` + `TcpAstmTransport` (modo TCP, padrão), `SerialAstmTransport` + `SerialBridge` (modo Serial), `PklNamedPipeServer` (Named Pipe), `AstmSessionManager` (gerencia sessões ASTM por cliente)
- **AstmMessageParser** / **AstmMessageBuilder**: Parser e builder do protocolo ASTM E1394-97
- **MessageProcessor**: Processamento de lotes com deduplicação
- **ResultProcessor**: Agrupa resultados recebidos do HLAB por `tag_id` (specimen ID) e envia em batch pra API VIDA quando a sessão é finalizada
- **ExamOrderService** / **ExamRequestService**: Consulta ordens de exame na API VIDA por etiqueta
- **Api/VidaApiClient**: Cliente HTTP da API VIDA com Polly (retry + circuit breaker)

### **PklBridge.Service**

- **Windows Service**: Execução como serviço do Windows
- **Worker**: Background service principal
- **Health Checks**: Monitoramento de saúde do sistema
- **Logging**: Serilog com console, arquivo e EventLog

## 🔄 Fluxo HLAB → VIDA

1. **HLAB** abre uma sessão ASTM contra a bridge (TCP ou Named Pipe) e envia os registros: `H` (header) → `P` (paciente) → `O` (order, com o specimen ID que serve como `tag_id`) → `R…R` (vários resultados) → `L` (terminator).
2. O **`AstmSessionManager`** mantém estado por cliente conectado e despacha cada record pro `ResultProcessor`.
3. O **`ResultProcessor`** ([PklBridge.Infrastructure/ResultProcessor.cs](PklBridge.Infrastructure/ResultProcessor.cs)) agrupa os resultados em batch por `tag_id` em memória (não envia ainda).
4. Quando a sessão termina (EOT / finalização), o `ResultProcessor` monta um `VidaResultRequest` com `franchise_credential_id` + `tag_id` + lista de resultados e chama `VidaApiClient.SendResultsByTagAsync` → **POST** em `VidaApi.IntegrationEndpoint`.
5. O `VidaApiClient` aplica Polly (retry + circuit breaker conforme config) antes de logar sucesso/falha.

> **`tag_id`** é a etiqueta do tubo (specimen ID que vem no record `O` do HLAB). É a chave que liga os resultados ao pedido na API VIDA.
>
> **`FranchiseCredentialId`** vazio = `ResultProcessor` aborta o envio e loga erro. Comportamento intencional pra não enviar pra franquia errada.

## 🔧 Configuração

A configuração fica em [PklBridge.Service/appsettings.json](PklBridge.Service/appsettings.json) (produção / Windows Service) e [PklBridge.ConsoleApp/appsettings.json](PklBridge.ConsoleApp/appsettings.json) (modo console pra testes). Todos os campos abaixo devem estar **explícitos no JSON** — os defaults no código existem só pra evitar `NullReferenceException`, não pra configurar o sistema.

### **Exemplo completo**

```json
{
  "BridgeSettings": {
    "TransportMode": "Tcp",
    "PipeName": "pkl_serial",
    "RealComPort": "COM2",
    "EnableTcpServer": true,
    "TcpPort": 8081,
    "EnableHardwareBridge": false,
    "LogAllTraffic": true,
    "BufferSize": 4096,
    "TimeoutMs": 5000,
    "Serial": {
      "BaudRate": 19200,
      "DataBits": 8,
      "Parity": "None",
      "StopBits": "One",
      "Handshake": "None",
      "ReadTimeout": 5000,
      "WriteTimeout": 5000,
      "DtrEnable": false,
      "RtsEnable": false
    }
  },
  "VidaApi": {
    "BaseUrl": "https://vida.hospital.local/api",
    "ResultEndpoint": "/exams/results",
    "PatientEndpoint": "/patients",
    "AuthEndpoint": "/auth/token",
    "IntegrationEndpoint": "/api/integration/pkl-125",
    "FranchiseCredentialId": "",
    "ApiKey": "",
    "Username": "",
    "Password": "",
    "TimeoutSeconds": 30,
    "MaxRetryAttempts": 3,
    "RetryDelayMs": 1000,
    "EnableCircuitBreaker": true,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerTimeoutMinutes": 5
  }
}
```

### **Campos obrigatórios antes de subir o serviço**

| Campo | Descrição |
| ----- | --------- |
| `VidaApi.BaseUrl` | URL base da API VIDA do ambiente (ex: `https://apoio.internal.vidaexame.com`) |
| `VidaApi.IntegrationEndpoint` | Endpoint que recebe os resultados HLAB → VIDA. Default funcional: `/api/integration/pkl-125` |
| `VidaApi.FranchiseCredentialId` | **UUID da franquia** — fornecido pela Siqueira. Se ficar vazio, o `ResultProcessor` aborta o envio e loga erro (comportamento intencional pra evitar enviar pra franquia errada) |
| `VidaApi.ApiKey` | Chave de autenticação da API (header `X-API-Key`) |
| `BridgeSettings.TransportMode` | `Tcp` (padrão) ou `Serial` |

### **Configuração do HLAB**

O HLAB precisa apontar pra esta bridge no mesmo modo configurado em `TransportMode`:

- **Modo TCP** (`TransportMode: "Tcp"`): HLAB conecta em `localhost:8081` (ou IP do servidor onde a bridge roda, na porta de `TcpPort`).
- **Modo Named Pipe** (`TransportMode: "Serial"` com pipe): HLAB conecta em `\\.\pipe\pkl_serial`.

Protocolo nos dois modos: **ASTM E1394-97**.

## 🚀 Instalação

### **Pré-requisitos**

- Windows 10/11 ou Windows Server 2019+
- .NET 8 Runtime
- Privilégios administrativos para instalação do serviço

### **Modo console (testes / desenvolvimento)**

Recomendado pra validar a config e o fluxo antes de instalar como serviço:

```powershell
dotnet run --project PklBridge.ConsoleApp
```

Ou via script: [PklBridge.ConsoleApp/iniciar-teste.bat](PklBridge.ConsoleApp/iniciar-teste.bat).

### **Windows Service (produção)**

O guia completo passo a passo está em [docs/INSTALAR-SERVICO-WINDOWS.md](docs/INSTALAR-SERVICO-WINDOWS.md). Resumo:

```powershell
# 1. Publicar
dotnet publish PklBridge.Service -c Release -r win-x64 --self-contained false -o "C:\PklBridge"

# 2. Editar C:\PklBridge\appsettings.json (preencher VidaApi.BaseUrl, ApiKey, FranchiseCredentialId)

# 3. Registrar e iniciar o serviço
New-Service -Name "PKLBridgeService" `
    -BinaryPathName "C:\PklBridge\PklBridge.Service.exe" `
    -DisplayName "PKL Bridge Service" `
    -Description "Integração PKL-125 ↔ API VIDA" `
    -StartupType Automatic
Start-Service -Name "PKLBridgeService"
```

## 📊 Monitoramento

### **Logs**

- **Console**: Durante desenvolvimento (e ao rodar via `PklBridge.ConsoleApp`)
- **Arquivo**: `logs/pkl-bridge-YYYY-MM-DD.log` (rolling diário, retenção 30 dias)
- **EventLog**: Warnings/erros vão pro Windows Event Viewer quando rodando como serviço

### **Health Checks**

O serviço monitora automaticamente (intervalo configurável em `HealthCheckSettings`):

- ✅ Status do transporte ativo (TCP server ou Named Pipe)
- ✅ Conexão serial COM (se `TransportMode: "Serial"`)
- ✅ Conectividade com API VIDA
- ✅ Uso de memória e recursos do sistema

### **Métricas**

- Mensagens processadas por minuto
- Taxa de sucesso da API VIDA
- Latência de processamento
- Uptime do serviço

## 🔍 Troubleshooting

### **Problemas Comuns**

**1. Named Pipe não conecta**
```bash
# Verificar se o pipe está ativo
handle.exe -a | findstr pkl_serial

# Verificar logs
Get-EventLog -LogName Application -Source "PKL Bridge Service" -Newest 50
```

**2. HLAB não consegue conectar**
- Verificar se HLAB está configurado para `\\.\pipe\pkl_serial`
- Verificar se o serviço PKL Bridge está rodando
- Verificar logs do serviço

**3. Erro na API VIDA**
- Verificar conectividade de rede
- Validar configurações de API (BaseUrl, ApiKey)
- Verificar logs de circuit breaker

**4. COM2 em uso**
```bash
# Verificar quais processos usam COM2
handle.exe | findstr COM2
```

### **Comandos Úteis**

```powershell
# Status do serviço
Get-Service -Name "PKLBridgeService"

# Parar / iniciar
Stop-Service -Name "PKLBridgeService"
Start-Service -Name "PKLBridgeService"

# Ver logs em tempo real
Get-Content -Path "C:\PklBridge\logs\pkl-bridge-$(Get-Date -f yyyy-MM-dd).log" -Wait -Tail 50
```

> **Health check:** é interno ao serviço (registrado via `Microsoft.Extensions.Diagnostics.HealthChecks`), os resultados aparecem no log. Não há endpoint HTTP exposto — verifique os logs com `[Health]` ou `HealthCheck` pra ver o status.

## 🧪 Teste e Validação

### **Teste do transporte (TCP)**

1. Suba a bridge (`dotnet run --project PklBridge.ConsoleApp` ou o serviço)
2. Conecte um cliente em `localhost:8081` (ex: `Test-NetConnection -ComputerName localhost -Port 8081` no PowerShell)
3. Envie um frame ASTM de teste
4. Verifique o processamento nos logs (`[HLAB→Bridge]`)

### **Teste do transporte (Named Pipe)**

1. Configure `TransportMode: "Serial"` ou ative o pipe server
2. Conecte ao pipe `\\.\pipe\pkl_serial` com uma ferramenta de pipe
3. Envie dados ASTM de teste

### **Teste da API VIDA**

1. Preencha `BaseUrl`, `ApiKey`, `FranchiseCredentialId` no `appsettings.json`
2. Suba o serviço — no startup, [Worker.cs](PklBridge.Service/Worker.cs) loga o `FranchiseCredentialId` e a `BaseUrl` em uso (conferir que batem com o esperado)
3. Acompanhe os logs ao receber resultados: `[Bridge→VIDA] Enviando N resultados para tag X`

### **Teste End-to-End**

1. Configure HLAB pra enviar pro endereço da bridge (TCP ou Named Pipe)
2. Execute um exame de teste na PKL 125
3. Acompanhe o trajeto nos logs: `[HLAB→Bridge]` → `[Bridge→VIDA]` → `✅ Resultados enviados com sucesso`
4. Confirme no sistema VIDA que os resultados chegaram associados à etiqueta (`tag_id`)

## 📈 Performance

### **Throughput**
- **Mensagens**: ~1000 mensagens/minuto
- **Latência**: <100ms por mensagem
- **Concorrência**: Até 5 processamentos simultâneos

### **Recursos**
- **Memória**: ~50MB em execução normal
- **CPU**: <5% em carga normal
- **Disco**: Logs com rotação diária

## 🔒 Segurança

- **Named Pipes**: Acesso restrito ao sistema local
- **API**: Autenticação via API Key
- **Logs**: Dados sensíveis são sanitizados
- **Validação**: Checksum ASTM obrigatório

## 📝 Protocolo ASTM E1394-97

### **Tipos de Registro Suportados**
- **H**: Header Record
- **P**: Patient Information Record
- **O**: Test Order Record
- **R**: Result Record (principal)
- **Q**: Query Record
- **L**: Terminator Record

### **Formato de Mensagem**
```
STX + Frame + Record + ETX + Checksum + CR + LF
```

### **Exemplo de Resultado**
```
|R|1|||^^^BUN^Ureia||mg/dL|15-45|N||F||20241121143022|
```

## 🤝 Contribuição

1. Clone o repositório
2. Crie uma branch para sua feature
3. Desenvolva com testes
4. Submeta um Pull Request


## 📞 Suporte

Para suporte técnico:
- **Logs**: Verifique sempre os logs primeiro
- **Health Check**: Use o endpoint de health para diagnósticos
- **Event Viewer**: Eventos críticos são registrados no Windows
- **Performance Monitor**: Use PerfMon para monitoramento avançado
