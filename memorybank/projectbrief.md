# Project Brief - Interface PKL Hemograma

## Objetivo Principal
Desenvolver um Windows Service em .NET 8 que realize o interfaceamento entre a máquina PKL 125 de hemograma e o sistema VIDA, permitindo comunicação bidirecional para solicitação de exames e recebimento de resultados.

## Escopo do Projeto
- **Comunicação Serial**: Interface com máquina PKL 125 via porta serial (COM2) usando protocolo ASTM E1394-97
- **Comunicação API**: Interface com sistema VIDA via API REST
- **Serviço Windows**: Aplicação executada como serviço do Windows
- **Configuração**: Sistema totalmente configurável via arquivo de configuração
- **Logs**: Sistema de logging robusto para monitoramento e debug

## Requisitos Funcionais
1. **Recebimento de Solicitações**: Receber solicitações de exames do sistema VIDA
2. **Envio para PKL**: Transmitir solicitações para a máquina PKL 125
3. **Coleta de Resultados**: Receber resultados da máquina PKL 125
4. **Envio de Resultados**: Transmitir resultados para o sistema VIDA
5. **Monitoramento**: Logging detalhado de todas as operações

## Requisitos Técnicos
- **.NET 8**: Framework principal
- **Windows Service**: Execução como serviço do sistema
- **Comunicação Serial**: Implementação do protocolo ASTM E1394-97
- **HTTP Client**: Para comunicação com API do sistema VIDA
- **Configuração**: Arquivo appsettings.json configurável
- **Logging**: Serilog ou Microsoft.Extensions.Logging

## Entregáveis
1. Windows Service funcional
2. Arquivo de configuração
3. Documentação de instalação
4. Logs estruturados
5. Tratamento de erros robusto

## Protocolo de Comunicação
Baseado no padrão ASTM E1394-97 com os seguintes tipos de registro:
- **H**: Message Header Record
- **P**: Patient Information Record  
- **O**: Test Order Record
- **R**: Result Record
- **L**: Message Terminator Record
- **Q**: Request Information Record

## Configurações Padrão
- **Porta Serial**: COM2
- **Baud Rate**: 19200
- **Data Bits**: 8
- **Parity**: None
- **Stop Bits**: 1
