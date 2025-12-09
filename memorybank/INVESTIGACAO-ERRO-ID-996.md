# Investigação: Erro "O valor do ID deve ser entre 1-996"

## 🎯 Situação Atual

Mensagem ASTM sendo enviada CORRETAMENTE:
```
H|\^&|||PKL Bridge^1.0^PKL125|||||||P|1|20251029072224
P|1|||50|Paciente Cincuenta||19900520|M||||||||||||||||||||
O|2|50^^^^N|50|^^^GLICOSE`^^^CREAT`^^^COLESTEROL`^^^HDL`^^^LDH`^^^ALBUMINA`^^^GAMA_GT`^^^AST/TGO`^^^ALT/TGP`^^^FOSF_ALC|R|20251029072224|||||||||Plasma||||||||||O
L|1|N
```

## ❌ Problema

HLAB não preenche os campos na tela. Erro mencionado: **"O valor do ID deve ser entre 1-996"**

## 🔍 Análise

### Campos onde aparece "50":
1. **Patient Record campo 4**: `50` (Patient ID)
2. **Order Record campo 3**: `50^^^^N` (Specimen ID - primeiro sub-campo)
3. **Order Record campo 4**: `50` (Universal Test ID)

### Hipótese: Validação de Rack Position

O HLAB pode estar esperando um número de RACK/POSITION entre 1-996, não o ID do paciente!

No protocolo ASTM, o Specimen ID pode ter:
```
ID ^ Parent ^ Container ^ Rack ^ Position ^ Type
50 ^        ^           ^      ^          ^ N
```

**E se o HLAB espera algo assim:**
```
50 ^        ^           ^ 1    ^ 7        ^ N
    └─ Sub-campo 4: Rack Number (1-99)
        └─ Sub-campo 5: Position (1-10)
```

## 🎯 Solução Proposta

Modificar Specimen ID para incluir Rack e Position:
```
50^^^1^7^N
  |||└─ Rack 1
  ||└─ Position 7
  |└─ Container (vazio)
  └─ Parent (vazio)
```

Ou usar valores padrão como no exemplo do PKL125:
```
^1^^1^7^ (conforme protocolo PKL125)
