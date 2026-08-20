# Etapa 04b — Calibragem contra dado real

**Data:** 2026-08-20 · **Situação:** ✅ Concluída

---

## 1. Objetivo

Rodar a coleta e o motor contra dado de verdade e medir se os limiares de
`ParametrosDoMotor` produzem sinal ou ruído. Eles eram estimativas — calibrar contra dado
inventado não vale nada.

## 2. Escopo executado, e por que não foi a base inteira

**20 deputados, 28.540 despesas, 3.343 fornecedores, 8.315 sócios.**

A estimativa que eu havia dado — "~6.000 requisições, ~20 minutos" — **estava errada**.
Ela contava só a paginação da Câmara e ignorava o enriquecimento de CNPJ. Medindo antes de
disparar: são **65 a 237 CNPJs únicos por deputado**, o que extrapola para **30 a 60 mil
consultas** à BrasilAPI numa coleta completa — 1,5 a 3 horas, e uma carga desproporcional
num serviço comunitário gratuito.

Uma amostra de 20 deputados responde as mesmas perguntas de calibragem com uma fração da
carga. A coleta ganhou parâmetro `--limite` para isso.

## 3. Descobertas

### 3.1 A medição de rate-limit da Etapa 01 estava errada

A Etapa 01 registrou "nenhum `429` observado" na BrasilAPI. A primeira coleta real, de
**dois** deputados, acumulou **166 respostas `429`**.

A causa da conclusão errada: aquela medição usou **oito requisições**. Oito não revelam um
limite medido por janela de tempo. Foi uma generalização apressada, e o registro da Etapa 01
agora aponta para cá.

`LimitadorDeTaxaHandler` espaça as chamadas **antes** de saírem (600ms) e obedece ao
`Retry-After`. Retry sozinho não resolvia: ele reage depois de já ter incomodado o servidor
e, sob carga, vira uma fila de tentativas que piora o congestionamento. Depois da correção,
a coleta de 20 deputados produziu **3 retries** no total.

### 3.2 Uma falha ao enriquecer um CNPJ derrubava o deputado inteiro

O sintoma era enganoso: **1.800 despesas gravadas e zero checkpoints**.

Uma consulta de CNPJ que esgotava as tentativas propagava a exceção e abortava a ingestão
inteira daquele parlamentar. As despesas já gravadas permaneciam — o hash garante isso — mas
sem checkpoint, então a passada seguinte recomeçaria do zero. Uma falha transitória ao
enriquecer **um** fornecedor custava as outras 1.199 despesas.

Agora degrada como "CNPJ desconhecido pela fonte", com duas diferenças: é contado à parte em
`FornecedoresNaoEnriquecidos` — *"não deu para olhar"* não pode se confundir com *"olhei e
não achei nada"* — e a falha **não é cacheada**, para que a próxima passada tente de novo.

Na coleta final: **1 CNPJ sem consulta** em 3.343.

### 3.3 O domínio rejeitava 4,3% dos registros — e a rejeição inflava o gasto

**1.223 despesas recusadas por "valor negativo".** São **estornos**: cancelamentos de
passagem aérea (SIGEPA), com o mesmo formato de uma despesa comum e sinal invertido.

O prejuízo não era perder registro. Era o oposto do que o produto existe para fazer:
**ignorar estorno infla o gasto.** Um parlamentar que comprou passagem de R$ 1.846 e cancelou
apareceria tendo gasto os R$ 1.846, e o ranking público o colocaria acima de quem realmente
gastou aquilo.

`Despesa` passou a aceitar valor negativo e expõe `EhEstorno`. As regras que **somam** não
mudaram — o negativo abate sozinho, que é o correto. As que **contam notas**
(`REG_TX_001` e `REG_KYB_005`) passaram a excluir estornos: uma devolução não é uma compra
fatiada.

Nota lateral sobre a arquitetura: a recoleta para capturar os estornos levou **47 segundos**,
contra 36 minutos da primeira. Os fornecedores já estavam no banco e o hash pulou as despesas
conhecidas — a idempotência pagou o próprio custo de projeto na primeira vez que foi exigida.

### 3.4 A tabela CNAE tinha 80% de falso positivo

Foi o achado central da calibragem. `REG_KYB_001` marcava **1.477 despesas — 5,2% de toda a
base**, e sozinha respondia por 76% de todas as flags. Alto demais para uma regra que deveria
falar só no desencontro evidente.

Olhando **quais** CNAEs estavam sendo marcados, a tabela é que estava errada:

| CNAE | Quem é | Flags | Por quê era falso positivo |
|---|---|---|---|
| `7490104` | **Uber do Brasil** | 439 | Uber é *intermediação e agenciamento*, não transporte. A tabela de táxi só tinha `492` e `522` |
| `8411600` | **Município de Belo Horizonte** | 386 | IPTU do escritório. Pagar prefeitura é imposto, não contratação de fornecedor |
| `6311900` | Brasil Cloud | 82 | Hospedagem em nuvem é manutenção de escritório |
| `7733100` | EMC Tecnologia | 64 | Locação de equipamento para o escritório |
| `6204000` | **Adobe** | 59 | Software é manutenção de escritório |
| `6541300` | Fundação Banrisul | 58 | Seguro do escritório |

Correções: uma lista de **ramos sempre aceitos** (`84`, administração pública — vale para
qualquer tipo de despesa), `7490`/`94`/`5229` acrescentados a táxi, e manutenção de escritório
alargada para TI, seguro, locação de equipamento, serviços administrativos e varejo.

**Resultado: 1.477 → 297 flags (5,2% → 1,04%).** Cada CNAE corrigido virou um caso de teste
com a contagem que produziu na base real.

E a regra **não emudeceu**: continuam marcadas imobiliária e churrascaria vendendo
combustível, que é exatamente o que ela existe para achar. Há teste garantindo isso.

## 4. Calibragem final

**766 flags sobre 28.540 despesas — 2,7%.** Nenhuma falha de regra.

| Regra | Flags | % das despesas | Leitura |
|---|---|---|---|
| `REG_KYB_001` | 297 | 1,04% | Calibrada nesta etapa |
| `REG_TX_003` | 203 | 0,71% | Plausível — valor idêntico por meses seguidos |
| `REG_TX_004` | 80 | 0,28% | Plausível |
| `REG_GEO_002` | 62 | 0,22% | Plausível |
| `REG_KYB_005` | 49 | 0,17% | Plausível |
| `REG_KYB_002` | 46 | 0,16% | Plausível |
| `REG_KYB_003` | 14 | 0,05% | Plausível |
| `REG_TX_001` | 13 | 0,05% | Baixo, mas o limiar é conservador |
| `REG_GEO_003` | 2 | 0,01% | Muito baixo |
| `REG_KYB_004` | 0 | — | Ver abaixo |
| `REG_TX_002` | 0 | — | Ver abaixo |

### As duas regras silenciosas têm explicação legítima

**`REG_KYB_004` (MEI acima do teto)** — há 43 fornecedores MEI na base, mas nenhum recebeu
acima de R$ 81.000 de um único parlamentar. Plausível. **Mas 2.190 dos 3.343 fornecedores
(65%) têm `optante_pelo_mei` nulo na fonte** — a regra está cega para dois terços da base, e
isso é limitação de dado, não de calibragem.

**`REG_TX_002` (teto da cota)** — só 4.809 das 28.540 despesas são de 2026, o único ano com
limites configurados. E os meses de 2026 ficam em 60–80% do teto, não nos 99% exigidos.
Levantar os limites de 2023–2025 mudaria isso mais que mexer no limiar.

### Amostra do que iria à página pública

```
REG_KYB_003  'LEVVA MOBILIDADE LTDA' tinha 43 dias de atividade quando emitiu
             nota de R$ 5.500,00.

REG_TX_003   Pagamentos de exatamente R$ 9.000,00 a 'R. M. R. DE ALMEIDA - ME'
             em 11 meses consecutivos, sem variação de centavos.

REG_TX_004   O gasto com 'DIVULGAÇÃO DA ATIVIDADE PARLAMENTAR.' em 2024-12 foi
             R$ 104.850,00, contra média de R$ 2x.xxx nos meses anteriores.

REG_KYB_005  3 notas fiscais de 'CRIVEL COMUNICACAO LTDA' para este parlamentar
             têm numeração inicial (18, 19, 20), compatível com um dos primeiros
             clientes da empresa.
```

Nenhuma imputa conduta, todas atribuem o achado a uma regra nomeada.

## 5. Pendências

| Pendência | Impacto | Observação |
|---|---|---|
| ~~Score favorece quem tem mais despesas~~ | ✅ Resolvido | Ver seção 7 e [ADR-0009](../decisoes/ADR-0009-dois-numeros-nao-um.md) |
| `REG_TX_002` só alcança 2026 | Médio | Levantar limites de 2023–2025 nos Atos da Mesa |
| `optante_pelo_mei` nulo em 65% dos fornecedores | Médio | Limitação da fonte; `REG_KYB_004` fica cega nesses casos |
| `REG_GEO_003` com 2 flags em 28 mil despesas | Baixo | Limiar de R$ 1.000 pode estar alto para alimentação |
| 3 despesas ainda rejeitadas por glosa inconsistente | Baixo | Investigar se é dado ruim da origem ou rigidez do domínio |
| Coleta completa dos 513 nunca rodada | Médio | ~1,5 a 3 horas e dezenas de milhares de consultas à BrasilAPI |

## 6. Desdobramento: a distorção do ranking virou o ADR-0009

A pendência mais grave que esta calibragem levantou não era de código, era de **modelo de
negócio**: havia um número só tentando servir a dois compradores com necessidades opostas.

O dado mostrou o problema sem ambiguidade:

| | Score antigo | Vermelhas | Despesas |
|---|---|---|---|
| Afonso Hamm | 107 | **1** | 3.575 |
| Albuquerque | 97 | **14** | 752 |

Hamm aparecia acima de Albuquerque tendo uma flag vermelha contra quatorze, só por ter
emitido quase cinco vezes mais notas. O score media atividade administrativa, não risco.

A correção está no [ADR-0009](../decisoes/ADR-0009-dois-numeros-nao-um.md): dois números
independentes, com piso amostral para a armadilha inversa da normalização. Depois da mudança,
os dois rankings dizem coisas diferentes — e ambos fazem sentido:

**Volume (B2C, público)**

| # | | Volume | Notas |
|---|---|---|---|
| 1 | Albuquerque | R$ 2.192.862 | 752 |
| 2 | Acácio Favacho | R$ 2.041.497 | 1.179 |
| 3 | Adail Filho | R$ 2.021.647 | 792 |

**Índice de risco (B2B, por 100 despesas)**

| # | | Índice | Vermelhas | Notas |
|---|---|---|---|---|
| 1 | Alexandre Leite | 13,28 | 0 | 738 |
| 2 | Albuquerque | 12,90 | 14 | 752 |
| 3 | Adail Filho | 10,35 | 0 | 792 |
| 6 | Alceu Moreira | 6,29 | 19 | 3.736 |

Alceu Moreira, que liderava o score antigo, caiu para sexto — suas 19 flags vermelhas estão
diluídas em 3.736 notas.

**Observação que merece atenção:** o primeiro colocado em risco tem **zero flags vermelhas**.
O índice pondera gravidade *e* confiança, então muitas flags médias superam poucas altas.
É honesto, mas vale decidir se a UI do B2B deve destacar a composição do índice para não
sugerir gravidade que não existe.

## 7. O que esta etapa provou

Nada aqui teria aparecido em teste com dado inventado. Os quatro defeitos — a medição errada
de rate-limit, a falha em cascata, os estornos rejeitados e a tabela CNAE com 80% de falso
positivo — só existiam no encontro com o dado real.

O mais grave era o mais silencioso: **a rejeição dos estornos inflava o gasto**, e o sistema
teria publicado números maiores que a realidade, com nome e foto, sem nenhum teste falhando.
