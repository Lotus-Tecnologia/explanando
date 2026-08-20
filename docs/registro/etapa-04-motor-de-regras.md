# Etapa 04 — Motor de regras determinísticas

**Início:** 2026-08-19 · **Conclusão:** 2026-08-19 · **Situação:** ✅ Concluída

---

## 1. Objetivo

Encadear as perguntas que o domínio já sabe responder — `PossuiCnae`, `EstavaIrregularEm`,
`DiasDeAtividadeAte`, `EhMei` — num pipeline de regras, uma classe por `REG_*`, e produzir
as `FlagAnomalia` que alimentam o score e o ranking público.

## 2. Definido

### Postura permissiva ([ADR-0007](../decisoes/ADR-0007-postura-permissiva-do-motor.md))

**Na dúvida, silêncio.** Falta de dado nunca vira suspeita. O resultado de compatibilidade
tem três estados — `Compativel`, `Incompativel`, `SemBaseParaAvaliar` — porque booleano
forçaria a escolher entre acusar e inocentar quando a resposta honesta é "não sei".

O motor vai achar menos do que acharia com postura restritiva. Isso é o desenho: cada falso
positivo é uma imputação indevida numa página pública indexada, com nome e foto.

### Repositório permanece privado ([ADR-0008](../decisoes/ADR-0008-repositorio-privado.md))

Com trava no CI que falha o pipeline se a visibilidade mudar.

## 3. Executado

### As duas tabelas curadas

Eram a pendência que bloqueava `REG_KYB_001` e `REG_TX_002`. Ambas resolvidas.

**CNAE × tipo de despesa** (`Domain/Cotas/CompatibilidadeCnae.cs`) — cobre os 24 tipos que a
API publica. Compara por **prefixo** de CNAE (2 a 5 dígitos), não por código exato de 7: casar
a família inteira dispensa acertar a subclasse, que muda com o tempo. Basta **um** CNAE bater,
principal ou secundário — exigir o principal marcaria empresas de atividade mista, como o
posto da amostra real que tem "bares" entre os secundários.

**Limites da CEAP** (`Infrastructure/Persistencia/Semente/SementeDeLimitesCota.cs`) — os 27
valores por UF, semeados de forma idempotente.

### CI no GitHub Actions

Build, suíte completa com Testcontainers, verificação de modelo × migrations, e a trava de
visibilidade. Mudança só em `docs/` não dispara o pipeline, para não consumir cota de Actions
à toa.

### As 11 regras e o motor

```
Domain/Regras/
  ParametrosDoMotor.cs        todos os limiares, num lugar só
  CalendarioLegislativo.cs    recesso pelo art. 57 da Constituição
  ContextoDeAvaliacao.cs      + IRegraAnomalia
  RegrasDeFornecedor.cs       REG_KYB_001 a 005
  RegrasDeTransacao.cs        REG_TX_001 a 004
  RegrasGeograficas.cs        REG_GEO_002 e 003
  MotorDeRegras.cs            orquestra e isola falhas
```

**Os limiares ficam todos em `ParametrosDoMotor`**, num arquivo só, porque são a parte mais
arbitrária do motor: nenhum vem de lei ou de dado publicado. Espalhados pelas regras, ninguém
conseguiria auditar o conjunto nem responder por que noventa dias e não sessenta. A única
exceção é o teto do MEI, que vem de lei — e está marcado como tal.

**`CalendarioLegislativo` não é calibragem.** O art. 57 da Constituição fixa a sessão
legislativa de 2 de fevereiro a 17 de julho e de 1º de agosto a 22 de dezembro; recesso é o
complemento. Por isso `REG_GEO_003` saiu da lista de pendências sem precisar de curadoria.

**`REG_GEO_002` só olha despesa presencial.** Contratar consultoria de empresa de outro estado
é rotina; abastecer um veículo lá, não. Sem esse recorte, a regra marcaria praticamente todo
parlamentar que contrata serviço digital fora do estado.

**`REG_KYB_005` só considera documento fiscal**, resolvendo a pendência que a Etapa 03
levantou: bilhete aéreo tem numeração de 13 dígitos e recibo não segue série sequencial do
emitente.

### Varredura e persistência

`ServicoDeVarredura` monta o contexto inteiro em memória antes de avaliar — o motor não toca
banco nem rede, o que garante que uma regra nova não introduza consulta N+1 sem ninguém
perceber.

A varredura **recalcula do zero**: apaga as flags da passada anterior antes de gravar. Somar
dobraria o score de um parlamentar só por rodar a varredura duas vezes, inflando o ranking
público sem nenhum gasto novo ter existido. E uma regra afrouxada precisa conseguir *remover*
um achado anterior, não só adicionar.

## 4. Testes

**318 testes verdes**, e o pipeline passa no CI — inclusive os testes de integração, que
sobem Postgres em contêiner no runner.

Cada regra tem dois tipos de teste: um que prova que ela **fala** no caso que existe para
pegar, e vários que provam que ela **se cala** onde não tem base — que é a parte que importa
sob a postura permissiva. O caso mais importante do motor é o silêncio: há um teste dedicado a
garantir que um parlamentar sem nada de errado não recebe flag nenhuma das 11 regras.

Dois testes estruturais valem menção: um garante que **toda flag emitida** passa pelo
`VocabularioDeCompliance` (uma regra que escrevesse vocabulário acusatório explodiria no teste
em vez de chegar à tela), e outro prova que **uma regra com defeito não derruba as outras
dez**.

A tabela de limites é testada **como dado**, não só como mecanismo: faixa plausível de valores
(guarda contra erro de ordem de grandeza), cobertura das 27 UFs sem repetição, e coerência
geográfica — RR maior que SP, AC maior que MG, DF o menor de todos. Se alguém editar a tabela
e inverter essa lógica, o teste reprova.

## 5. Descobertas

### 5.1 A tabela da CEAP não existe em nenhuma API

Não está nos dados abertos, e o mapa do portal de transparência a renderiza por JavaScript
sem expor os valores no HTML nem em chamada de rede alguma. Foi preciso encontrá-la no Guia
para Jornalistas do próprio portal da Câmara.

**Conferência:** os 27 valores foram extraídos duas vezes de forma independente, com
resultados idênticos, e os extremos batem com intervalo publicado por fonte jornalística
separada. A ordem também faz sentido físico — DF tem o menor valor por dispensar voo, RR o
maior por ser o mais distante.

### 5.2 A vigência dos valores não é publicada — e a suposição erra para o lado certo

A fonte traz os valores vigentes, mas não desde quando, nem o histórico. Assumimos
**2026-01-01**.

A consequência é assimétrica, e é por isso que a suposição é aceitável: se o reajuste ocorreu
depois de janeiro, as despesas anteriores serão comparadas a um teto **mais alto** que o real,
e `REG_TX_002` deixará de sinalizar alguns casos. Erra para menos, nunca para mais.

Despesas de 2023 a 2025 ficam **sem teto configurado**, e a regra se abstém sobre elas.

### 5.3 O CI acusou o erro errado

A primeira versão do job de migrations não rodava `restore`, então o comando falhava por
assets ausentes. Pior que a falha: a mensagem culpava o modelo em qualquer saída diferente de
zero — o log dizia "o modelo mudou sem migration" quando a causa era outra, mandando
investigar a coisa errada.

Corrigido inspecionando a saída do comando para distinguir os dois casos. A verificação também
mudou de job próprio para dentro do job de build, reaproveitando restore e build: em
repositório privado os minutos de Actions são cota.

### 5.4 O vocabulário obrigatório continha um termo proibido

"Desvio Padrão" está entre os termos **obrigatórios** do documento de Negócios. "Desvio" está
entre os **proibidos**, por imputar conduta. A checagem por palavra inteira bloqueava o termo
que o próprio documento exige.

Resolvido com uma lista de expressões técnicas permitidas, retiradas do texto antes da busca
pelos termos proibidos — e com teste garantindo que a exceção não libera "desvio" sozinho.

### 5.5 Documentei um comportamento que não tinha implementado

O cabeçalho do `MotorDeRegras` dizia que "uma regra com defeito não derruba as outras", mas o
código não tinha `try/catch` nenhum. Percebido ao escrever o teste correspondente.

Implementado de verdade, com um detalhe que só aparece na prática: as regras usam iteradores
preguiçosos, então o `ToList()` precisa ficar **dentro** do `try` — fora dele, a exceção só
apareceria mais adiante, já sem alcance do `catch`.

### 5.6 Um teste se contaminou pelo banco compartilhado

`VarreduraPontaAPontaTests` semeava um fornecedor com CNPJ fixo e situação "Baixada". Só que
outros testes da mesma coleção já haviam gravado esse mesmo CNPJ com situação "Ativa" — e o
cenário acabava reutilizando o fornecedor errado, fazendo `REG_KYB_002` não disparar.

O banco é compartilhado pela coleção de propósito (subir um contêiner por teste seria lento
demais), então o dado de cada teste precisa ser único. Passou a gerar CNPJ válido por execução.

### 5.7 As descrições saíram sem acento

As descrições das flags são **texto público** — aparecem na página do parlamentar. Escrevi-as
sem acentuação por hábito de código, e um teste que procurava "média" pegou. Corrigidas para
português correto; a checagem de vocabulário normaliza acentos, então continuam funcionando.

### 5.8 A chave da tabela CNAE tinha ponto final e nunca seria encontrada

`Normalizar` remove ponto final do tipo de despesa, mas uma das 24 chaves foi escrita com ele.
A entrada existia e era inalcançável — `REG_KYB_001` se absteria em silêncio para
"aquisição ou loc. de software". Pego pelo teste que exige prefixo não vazio para todo tipo
coberto.

## 6. Pendências

| Pendência | Impacto | Observação |
|---|---|---|
| `REG_NET_004` (concentração partidária) não implementada | Médio | Única das 12 que cruza parlamentares; o contexto atual é por parlamentar e ela precisa de uma passada global |
| Calibragem dos limiares nunca foi confrontada com dado real | Alto | Os valores de `ParametrosDoMotor` são estimativas; só uma coleta real dirá se produzem sinal ou ruído |
| Limites da CEAP de 2023 a 2025 | Médio | `REG_TX_002` só alcança 2026 até que sejam levantados nos Atos da Mesa |
| Confirmar a vigência assumida (2026-01-01) | Baixo | A suposição erra para menos; ver 5.2 |
| Medir e exibir a taxa de abstenção do motor | Médio | Se `REG_KYB_001` se abstiver em 80% das despesas, a tabela está pobre e isso tem de ser visível |
| Nenhuma coleta real executada contra os 513 deputados | Médio | Depois do motor, para não popular o banco duas vezes |

## 7. Próxima etapa

**Etapa 05 — API pública.**

Com o motor de pé, o próximo passo é expor o resultado: ranking por score, perfil do
parlamentar e listagem de despesas com as inconsistências. CQRS, cache no Redis para as
consultas quentes, e a ofuscação de dado pessoal na borda de saída.

Antes disso, porém, vale **rodar a coleta real** contra os 513 deputados e passar o motor
sobre a base. É a única forma de saber se os limiares de `ParametrosDoMotor` produzem sinal ou
ruído — e a taxa de abstenção por regra, que o motor já reporta, é a métrica que responde isso.
Calibrar contra dado inventado não vale nada.
