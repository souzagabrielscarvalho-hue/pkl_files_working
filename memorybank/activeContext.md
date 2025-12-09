# Active Context - Interface PKL Hemograma

## Contexto Atual
**NOVA ARQUITETURA DEFINIDA**: O usuário esclareceu o problema real - o sistema HLAB já se comunica com PKL via COM1 e replica dados para COM2, mas há conflito de acesso exclusivo à porta COM2. Decidimos implementar uma solução própria usando Named Pipes para criar um bridge inteligente.

### Problema Identificado
- HLAB comunica com PKL 125 via COM1
- HLAB replica dados para COM2 (porta física)
- HLAB precisa acesso exclusivo à COM2
- Meu sistema também precisa ler/escrever dados da comunicação
- **Conflito**: Apenas um processo pode abrir COM2 por vez

### Solução Arquitetada
**PKL Bridge com Named Pipes**: Implementação própria que cria um pipe nomeado para HLAB conectar, intercepta todos os dados, faz bridge com COM2 real (se necessário) e envia dados relevantes para sistema VIDA via API.

## Análise dos Arquivos de Referência
Foram analisados os seguintes arquivos fornecidos pelo usuário:

### 1. PPC 125 protocol.pdf
- **Conteúdo**: Documentação completa do protocolo ASTM E1394-97
- **Detalhes Importantes**:
  - Protocolo bidirecional com suporte a RS232 e TCP/IP
  - Configuração padrão: 19200,8,N,1
  - Tipos de registro: H, P, O, R, Q, L
  - Cálculo de checksum modulo 256
  - Exemplos completos de comunicação

### 2. log.txt
- **Conteúdo**: Log de exemplo de comunicação real
- **Detalhes Importantes**:
  - Mostra fluxo completo de Query e Response
  - Exemplos de códigos de teste: BUN, CREA, GLUC, etc.
  - Estrutura real de mensagens com checksums

### 3. LIS simulation.docx
- **Conteúdo**: Instruções de simulação usando software LIS
- **Detalhes Importantes**:
  - Lista de testes suportados: BUN, CREA, GLUC, URIC, CHOL, TG, HDL, LDL, TP, ALB, TB, DB, AST, ALT, ALP
  - Exemplos de códigos de barras: 112233, 445566
  - Simulação de comunicação completa

## Decisões de Arquitetura

### 1. Estrutura do Projeto
- **Arquitetura**: Clean Architecture com separação de responsabilidades
- **Projetos**: Service (host), Core (domínio), Infrastructure (implementações)
- **Padrões**: Dependency Injection, Repository Pattern, Mediator Pattern

### 2. Comunicação Serial
- **Porta**: COM2 (configurável)
- **Protocolo**: ASTM E1394-97 completo
- **Implementação**: State machine para gerenciar fluxo de comunicação
- **Resilência**: Retry pattern com backoff exponencial

### 3. Comunicação API
- **Cliente**: HttpClient com Polly para resiliência
- **Formato**: JSON para payloads
- **Autenticação**: Token-based (a definir com usuário)
- **Endpoints**: RESTful para operações CRUD

### 4. Configuração
- **Arquivo**: appsettings.json com override por ambiente
- **Padrão**: Options Pattern para tipagem forte
- **Sensíveis**: Environment variables para credenciais

### 5. Logging
- **Framework**: Serilog com structured logging
- **Níveis**: Debug para desenvolvimento, Information para produção
- **Destinos**: File, Console, EventLog do Windows

## Próximos Passos - PKL Bridge Development
### **Fase 1: Infraestrutura Base** 🔄
1. **Criar PklBridge.Core** - Models, interfaces, configurações
2. **Criar PklBridge.Infrastructure** - Named Pipe Server, Serial Bridge, API Client
3. **Criar PklBridge.Service** - Windows Service host
4. **Configurar DI** - Dependency injection setup
5. **Setup Logging** - Serilog estruturado

### **Fase 2: Named Pipe Server**
1. **Implementar PklNamedPipeServer** - Servidor que HLAB conectará
2. **Testes de conexão** - Validar comunicação pipe
3. **Error handling** - Reconexão, timeouts
4. **Buffer management** - Gerenciamento de dados seriais

### **Fase 3: Serial Bridge Core**
1. **Implementar SerialBridge** - Core do sistema
2. **Conexão COM2** - Para hardware real (se existir)
3. **Bridge bidirecional** - Pipe ↔ COM2
4. **Interceptação inteligente** - Captura de dados

### **Fase 4: ASTM Integration**
1. **Migrar parser** - Do PklSerialMonitor existente
2. **Adapter byte arrays** - Trabalhar com dados raw
3. **Extração resultados** - Focar em registros R
4. **Validação** - Checksums, estrutura

### **Fase 5: VIDA Integration**
1. **VidaApiClient** - Cliente HTTP com Polly
2. **Mapeamento dados** - ASTM → JSON VIDA
3. **Retry policies** - Resiliência
4. **Monitoring** - Health checks

### **Fase 6: Windows Service**
1. **Service Worker** - Host service
2. **Instalação** - Scripts e documentação
3. **Monitoramento** - Logs, métricas
4. **Deployment** - Configuração produção

## Questões Pendentes
1. **API VIDA**: Quais são os endpoints específicos?
2. **Autenticação**: Que tipo de autenticação usar com o sistema VIDA?
3. **Frequência**: Com que frequência verificar por novas solicitações?
4. **Erro Handling**: Como tratar falhas na comunicação?
5. **Deployment**: Onde será instalado o serviço?

## Considerações Técnicas - PKL Bridge
- **Named Pipes**: Comunicação IPC para substituir COM2 do HLAB
- **Thread Safety**: Bridge bidirecional assíncrono
- **Buffer Management**: Dados seriais em tempo real
- **ASTM Protocol**: Parser robusto com checksum validation
- **Error Recovery**: Reconexão automática de pipes e COM
- **Performance**: Zero latência na comunicação HLAB ↔ Hardware
- **Observability**: Logs estruturados de todo tráfego
- **Configuration**: appsettings.json para todos parâmetros
- **Security**: Validação ASTM, sanitização dados API

## Arquitetura Final
```
PKL 125 ←COM1→ HLAB ←NamedPipe→ PKL Bridge ←COM2→ Hardware (opcional)
                                      ↓
                               API Client → Sistema VIDA
```

### Componentes Principais
1. **PklNamedPipeServer**: Servidor pipe que HLAB conecta
2. **SerialBridge**: Core que gerencia todo fluxo
3. **AstmParser**: Parser de mensagens (migrado do PklSerialMonitor)
4. **VidaApiClient**: Cliente HTTP para sistema VIDA
5. **Windows Service Host**: Execução como serviço do sistema

## Padrões Identificados
- **Fluxo de Solicitação**: VIDA → Service → PKL 125
- **Fluxo de Resultado**: PKL 125 → Service → VIDA
- **Protocolo**: Sempre inicia com ENQ, termina com EOT
- **Sequência**: Frames numerados sequencialmente (1-7, 0, 1...)
- **Acknowledgment**: Cada frame deve ser confirmado com ACK

## Configurações Críticas - PKL Bridge
- **Named Pipe**: `\\.\pipe\pkl_serial` (HLAB conectará aqui)
- **COM2**: Porta real para hardware (se existir)
- **Baud Rate**: 19200 (padrão PKL 125)
- **ASTM Protocol**: Checksum validation, frame parsing
- **VIDA API**: Endpoints, autenticação, retry policies
- **Service**: Windows Service com auto-start
- **Logging**: Structured logs com Serilog
- **Buffer Size**: Configurável para throughput serial

## Status Atual
**🚀 INICIANDO DESENVOLVIMENTO**: Fase 1 - Criando estrutura base dos projetos
