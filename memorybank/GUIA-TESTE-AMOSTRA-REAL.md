# 🧪 GUIA DE TESTE COM AMOSTRA REAL

**Data**: 15/01/2025  
**Amostra**: Tubo com etiqueta de código de barras

## 📋 Informações da Amostra

Pela imagem, vejo que você tem:
- ✅ Tubo de amostra (aparenta ser urina)
- ✅ Etiqueta com código de barras
- ✅ Número identificador visível na etiqueta

## 🎯 Objetivo do Teste

Testar o fluxo completo:
1. Escanear etiqueta
2. Buscar exames na API VIDA
3. Enviar solicitação para PKL 125 via HLAB
4. Receber resultados
5. Enviar resultados para API VIDA

## 🚀 Passo a Passo para Teste

### **Passo 1: Preparar o Ambiente**

#### 1.1. Iniciar o PKL Bridge
```bash
cd E:\Projetos\Andrey\Projetos\InterfacePKL
dotnet run --project PklBridge.ConsoleApp
```

#### 1.2. Verificar Status
Você deve ver:
```
✅ PKL Bridge iniciado com sucesso!
🌐 TCP Server ativo na porta 8081
📡 Named Pipe ativo: \\.\pipe\pkl_serial
🔌 Aguardando conexão do HLAB...
```

### **Passo 2: Configurar o HLAB**

#### 2.1. Configurar Saída do HLAB
No software HLAB, configure:
- **Tipo de Conexão**: TCP/IP
- **Host**: `127.0.0.1` (localhost)
- **Porta**: `8081`
- **Protocolo**: ASTM E1394-97

OU (alternativa):
- **Tipo de Conexão**: Named Pipe
- **Pipe Name**: `\\.\pipe\pkl_serial`

#### 2.2. Testar Conexão
- Inicie o HLAB
- Verifique se conectou ao PKL Bridge
- No console do PKL Bridge você verá: "🔗 Nova conexão TCP aceita"

### **Passo 3: Testar com Modo Mock (Primeiro)**

Antes de usar a API VIDA real, teste com mock:

#### 3.1. Executar PKL Bridge
```bash
dotnet run --project PklBridge.ConsoleApp
```

#### 3.2. Escolher Menu Interativo
```
Opção: 1
```

#### 3.3. Simular Exame
```
Escolha: 1 (Simular Exame de Urina Completo)
```

#### 3.4. Verificar Resultado
Você deve ver:
- ✅ Solicitação enviada
- ✅ Resultados simulados recebidos
- ✅ Dados enviados para API (mock)

### **Passo 4: Testar com Etiqueta Real**

#### 4.1. Ler o Código da Etiqueta
Olhando a imagem, identifique o número da etiqueta (exemplo: `22012025008`)

#### 4.2. Verificar se Etiqueta Existe na API VIDA
Teste manualmente primeiro:
```bash
# Abra um navegador ou use curl
curl "https://internal_laboratory.siqueira.vidaexame.com/api/integration/pkl-125?franchise_credential_id=88cf9273-5044-47f4-b8f6-01160345a190&tag_id=SEU_NUMERO_AQUI"
```

#### 4.3. Se a Etiqueta Existir
Você verá algo como:
```json
{
  "message": "Procedimentos encontrados com sucesso!",
  "data": [
    {"exam_code": "TCO", "test": "TCO"},
    {"exam_code": "PCR", "test": "PCR"}
  ]
}
```

### **Passo 5: Teste Completo com Hardware Real**

#### 5.1. Preparar PKL 125
- Ligue o equipamento PKL 125
- Verifique se está conectado ao HLAB
- Certifique-se que está pronto para receber amostras

#### 5.2. Preparar Amostra
- Tenha a amostra (tubo da foto) pronta
- Certifique-se que a etiqueta está legível
- Verifique se o código de barras pode ser lido

#### 5.3. Fluxo Completo

**A) Iniciar Sistema:**
```bash
dotnet run --project PklBridge.ConsoleApp
Opção: 2 (Modo Monitor)
```

**B) Escanear Etiqueta no Sistema VIDA:**
- No sistema VIDA, registre a solicitação de exame
- Escaneie o código de barras da amostra
- O sistema VIDA deve ter a etiqueta cadastrada

**C) PKL Bridge Busca Exames:**
- PKL Bridge consulta API VIDA automaticamente
- Busca exames para aquela etiqueta
- Gera mensagem ASTM Order

**D) Enviar para PKL 125:**
- PKL Bridge envia Order via HLAB
- HLAB encaminha para PKL 125
- PKL 125 recebe solicitação

**E) Processar Amostra:**
- Coloque a amostra no PKL 125
- Escaneie o código de barras no equipamento
- PKL 125 processa a amostra

**F) Receber Resultados:**
- PKL 125 envia resultados para HLAB
- HLAB envia para PKL Bridge via TCP
- PKL Bridge parseia mensagem ASTM

**G) Enviar para VIDA:**
- PKL Bridge envia resultados para API VIDA
- Sistema VIDA atualiza os resultados
- Resultados ficam disponíveis no sistema

### **Passo 6: Monitorar o Processo**

#### 6.1. Logs do PKL Bridge
Observe os logs em tempo real:
```
[HH:mm:ss INF] 🔍 Buscando exames para etiqueta {TagId}
[HH:mm:ss INF] ✅ Encontrados {N} exames
[HH:mm:ss INF] 📤 Enviando mensagem ASTM
[HH:mm:ss INF] 📨 Dados recebidos via TCP
[HH:mm:ss INF] 🔬 Processando resultados
[HH:mm:ss INF] ✅ Resultados enviados para API VIDA
```

#### 6.2. Arquivo de Log
Verifique também:
```
logs/pkl-bridge-console-YYYY-MM-DD.log
```

## 🔧 Troubleshooting

### Problema 1: HLAB não conecta
**Solução:**
- Verifique se porta 8081 está livre
- Confirme IP e porta no HLAB
- Verifique firewall do Windows

### Problema 2: Etiqueta não encontrada
**Solução:**
- Verifique se etiqueta está cadastrada no sistema VIDA
- Confirme o número da etiqueta
- Teste manualmente com curl primeiro

### Problema 3: PKL 125 não recebe Order
**Solução:**
- Verifique conexão HLAB ↔ PKL 125
- Confirme protocolo ASTM no HLAB
- Verifique logs do HLAB

### Problema 4: Resultados não chegam
**Solução:**
- Verifique se PKL 125 enviou para HLAB
- Confirme que HLAB está enviando para PKL Bridge
- Verifique logs de comunicação TCP

## 📊 Checklist de Teste

### Preparação
- [ ] PKL Bridge compilado e funcionando
- [ ] HLAB configurado para TCP 127.0.0.1:8081
- [ ] PKL 125 ligado e conectado ao HLAB
- [ ] Amostra com etiqueta pronta
- [ ] Etiqueta cadastrada no sistema VIDA

### Teste Mock (Validação)
- [ ] PKL Bridge inicia sem erros
- [ ] Menu interativo funciona
- [ ] Simulação de exame completa com sucesso
- [ ] Logs mostram fluxo correto

### Teste Real (Produção)
- [ ] HLAB conecta ao PKL Bridge
- [ ] Etiqueta é encontrada na API VIDA
- [ ] Mensagem ASTM Order é gerada
- [ ] PKL 125 recebe a solicitação
- [ ] Amostra é processada
- [ ] Resultados são recebidos
- [ ] Resultados são enviados para API VIDA
- [ ] Resultados aparecem no sistema VIDA

## 🎯 Próximos Passos

### Se Teste Mock Funcionar:
1. Ativar API VIDA real (trocar linha no Program.cs)
2. Testar busca de etiqueta real
3. Validar mapeamento de códigos de exames

### Se Teste Real Funcionar:
1. Documentar configurações finais
2. Criar procedimento operacional padrão
3. Treinar equipe
4. Deploy em produção

## 📝 Notas Importantes

1. **Primeira vez**: Use modo mock para validar o fluxo
2. **Segunda vez**: Teste com API VIDA real mas sem hardware
3. **Terceira vez**: Teste completo com hardware real
4. **Sempre**: Monitore os logs para identificar problemas

## 🆘 Suporte

Se encontrar problemas:
1. Verifique os logs em `logs/pkl-bridge-console-*.log`
2. Capture screenshots dos erros
3. Anote o número da etiqueta usada
4. Documente o comportamento observado

---

**Boa sorte com os testes! O sistema está pronto para uso.** 🚀
