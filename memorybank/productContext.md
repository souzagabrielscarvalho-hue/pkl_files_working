# Product Context - Interface PKL Hemograma

## Problema a Resolver
Atualmente existe uma lacuna de comunicação entre a máquina PKL 125 de hemograma e o sistema VIDA. Esta falta de integração resulta em:
- Processo manual de solicitação de exames
- Digitação manual de resultados
- Possibilidade de erros humanos
- Demora no processamento
- Falta de rastreabilidade

## Solução Proposta
Um Windows Service que atua como ponte de comunicação, automatizando completamente o fluxo de trabalho:

### Fluxo de Trabalho Automatizado
1. **Solicitação de Exames**:
   - Sistema VIDA envia solicitação via API
   - Service recebe e processa a solicitação
   - Service envia comando para PKL 125 via serial
   - PKL 125 recebe e programa o exame

2. **Processamento de Resultados**:
   - PKL 125 processa a amostra
   - PKL 125 envia resultados via serial
   - Service recebe e processa os resultados
   - Service envia resultados para sistema VIDA via API

## Benefícios da Solução
- **Automação Completa**: Eliminação de processos manuais
- **Redução de Erros**: Eliminação de digitação manual
- **Agilidade**: Comunicação instantânea entre sistemas
- **Rastreabilidade**: Log completo de todas as operações
- **Confiabilidade**: Sistema robusto com tratamento de erros

## Experiência do Usuário
### Para o Operador do Sistema VIDA
- Solicita exames normalmente no sistema
- Recebe resultados automaticamente
- Não precisa interagir com a máquina PKL diretamente

### Para o Operador da PKL 125
- Vê solicitações aparecerem automaticamente na máquina
- Coloca as amostras e executa normalmente
- Resultados são enviados automaticamente

## Tipos de Exames Suportados
Baseado na documentação da PKL 125, os seguintes exames são suportados:
- **BUN** - Blood Urea Nitrogen
- **CREA** - Creatinina
- **GLUC** - Glicose
- **URIC** - Ácido Úrico
- **CHOL** - Colesterol Total
- **TG** - Triglicerídeos
- **HDL** - HDL Colesterol
- **LDL** - LDL Colesterol
- **TP** - Proteína Total
- **ALB** - Albumina
- **TB** - Bilirrubina Total
- **DB** - Bilirrubina Direta
- **AST** - Aspartato Aminotransferase
- **ALT** - Alanina Aminotransferase
- **ALP** - Fosfatase Alcalina

## Modos de Operação
### Modo Solicitação (Query Mode)
- Sistema VIDA solicita exames específicos
- Service envia solicitação para PKL 125
- PKL 125 programa os exames solicitados

### Modo Resultado (Result Mode)
- PKL 125 envia resultados disponíveis
- Service processa e envia para sistema VIDA
- Confirmação de recebimento

## Requisitos de Qualidade
- **Disponibilidade**: 99.9% de uptime
- **Performance**: Processamento em tempo real
- **Segurança**: Comunicação segura entre sistemas
- **Conformidade**: Aderência ao protocolo ASTM E1394-97
- **Monitoramento**: Logs detalhados para auditoria
