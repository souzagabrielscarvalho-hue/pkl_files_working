# System Patterns - Interface PKL Hemograma

## Arquitetura Geral
```
┌─────────────────┐    API REST     ┌─────────────────┐    Serial ASTM    ┌─────────────────┐
│   Sistema VIDA  │ ◄──────────────► │ Windows Service │ ◄─────────────────► │   PKL 125      │
│                 │                 │                 │                    │                 │
│ - Solicitações  │                 │ - Processamento │                    │ - Hemograma     │
│ - Resultados    │                 │ - Transformação │                    │ - Resultados    │
│ - Pacientes     │                 │ - Logging       │                    │ - Configuração  │
└─────────────────┘                 └─────────────────┘                    └─────────────────┘
```

## Componentes Principais

### 1. Windows Service Host
- **Responsabilidade**: Gerenciar o ciclo de vida do serviço
- **Padrão**: Service Worker Pattern
- **Tecnologia**: Microsoft.Extensions.Hosting
- **Configuração**: Dependency Injection

### 2. Communication Manager
- **Responsabilidade**: Coordenar comunicação entre sistemas
- **Padrão**: Mediator Pattern
- **Componentes**:
  - Serial Communication Handler
  - API Communication Handler
  - Message Queue Manager

### 3. Protocol Handler (ASTM E1394-97)
- **Responsabilidade**: Implementar protocolo ASTM
- **Padrão**: State Machine Pattern
- **Componentes**:
  - Frame Parser
  - Checksum Calculator
  - Message Builder
  - Sequence Manager

### 4. Message Processing Pipeline
- **Responsabilidade**: Processar mensagens entre sistemas
- **Padrão**: Pipeline Pattern
- **Estágios**:
  - Validation
  - Transformation
  - Routing
  - Logging

## Padrões de Comunicação

### Serial Communication (PKL 125)
```
┌─────────────┐    ENQ     ┌─────────────┐
│   Service   │ ──────────► │   PKL 125   │
│             │ ◄────────── │             │
│             │    ACK     │             │
│             │            │             │
│             │   STX+Data │             │
│             │ ──────────► │             │
│             │ ◄────────── │             │
│             │    ACK     │             │
│             │            │             │
│             │    EOT     │             │
│             │ ──────────► │             │
└─────────────┘            └─────────────┘
```

### API Communication (Sistema VIDA)
```
┌─────────────┐   HTTP/REST   ┌─────────────┐
│   Service   │ ◄────────────► │ Sistema VIDA│
│             │               │             │
│ - POST      │               │ - Endpoints │
│ - GET       │               │ - JSON      │
│ - PUT       │               │ - Auth      │
└─────────────┘               └─────────────┘
```

## Gerenciamento de Estado

### Estado da Comunicação Serial
```csharp
public enum SerialState
{
    Idle,
    Sending,
    Receiving,
    WaitingAck,
    Error
}
```

### Estado do Processamento
```csharp
public enum ProcessingState
{
    Waiting,
    Processing,
    Completed,
    Failed
}
```

## Padrões de Tratamento de Erros

### Retry Pattern
- **Tentativas**: 3 tentativas com backoff exponencial
- **Timeout**: 30 segundos por tentativa
- **Fallback**: Log de erro e notificação

### Circuit Breaker Pattern
- **Threshold**: 5 falhas consecutivas
- **Timeout**: 5 minutos
- **Recovery**: Teste automático

### Dead Letter Queue
- **Mensagens com falha**: Armazenamento temporário
- **Reprocessamento**: Manual ou automático
- **Alertas**: Notificação de administrador

## Padrões de Logging

### Structured Logging
```csharp
_logger.LogInformation("Mensagem processada {MessageType} para {PatientId}", 
                       messageType, patientId);
```

### Correlation ID
- **Rastreamento**: ID único por transação
- **Contexto**: Mantido durante todo o fluxo
- **Debugging**: Facilita investigação de problemas

## Configuração e Injeção de Dependências

### Service Registration
```csharp
services.AddScoped<ISerialCommunicationService, SerialCommunicationService>();
services.AddScoped<IApiCommunicationService, ApiCommunicationService>();
services.AddScoped<IProtocolHandler, AstmProtocolHandler>();
services.AddScoped<IMessageProcessor, MessageProcessor>();
```

### Configuration Pattern
```csharp
public class PKLConfiguration
{
    public SerialSettings Serial { get; set; }
    public ApiSettings Api { get; set; }
    public LoggingSettings Logging { get; set; }
}
```

## Padrões de Segurança

### API Authentication
- **Método**: Token-based authentication
- **Refresh**: Renovação automática de tokens
- **Timeout**: Configurável por ambiente

### Data Validation
- **Input**: Validação de entrada
- **Output**: Validação de saída
- **Sanitização**: Limpeza de dados sensíveis

## Monitoramento e Observabilidade

### Health Checks
- **Serial Port**: Verificação de conectividade
- **API Endpoint**: Verificação de disponibilidade
- **Database**: Verificação de acesso (se aplicável)

### Metrics
- **Throughput**: Mensagens por minuto
- **Latency**: Tempo de resposta
- **Error Rate**: Taxa de erro por operação

### Tracing
- **Distributed Tracing**: Rastreamento end-to-end
- **Performance**: Identificação de gargalos
- **Debugging**: Análise de problemas
