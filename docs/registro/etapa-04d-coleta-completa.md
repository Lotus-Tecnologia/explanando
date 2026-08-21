# Etapa 04d — Coleta completa da Câmara e primeira varredura da base inteira

**Data:** 2026-08-21 · **Situação:** ✅ Concluída

---

## 1. O raio-X

| | |
|---|---|
| Parlamentares | **513 de 513** (100% da esteira, zero pendências) |
| Despesas | **711.796** |
| Fornecedores | **40.774** |
| Volume total | **R$ 802.407.895,78** |
| Flags | **21.067** (2,96% das despesas) |
| Consolidados mensais | 20.097 |
| Banco | **317 MB** |
| Falhas de regra | **0** |
| Suíte | 641 testes |

**503 dos 513** receberam ao menos uma flag. **15 ficam fora do ranking de risco** por amostra
insuficiente, exatamente como o [ADR-0009](../decisoes/ADR-0009-dois-numeros-nao-um.md) previu.

### Os dois números do ADR-0009, agora com base real

| | Volume (B2C) | Índice de risco (B2B) |
|---|---|---|
| Máximo | R$ 2.287.735,08 | 38,28 |
| Média | R$ 1.564.147,94 | 5,04 |
| Publicáveis | 513 | 498 |

### Gravidade das flags

| Gravidade | Flags | % |
|---|---|---|
| Alta | 695 | 3,3% |
| Média | 11.258 | 53,4% |
| Baixa | 9.114 | 43,3% |

Só 3,3% são de gravidade alta. É a postura permissiva do
[ADR-0007](../decisoes/ADR-0007-postura-permissiva-do-motor.md) aparecendo no agregado.

## 2. A calibragem sobreviveu a um aumento de 25× no volume

| Amostra | Despesas | Taxa sinalizada |
|---|---|---|
| 5 deputados | 5.843 | 3,3% |
| 20 deputados (Etapa 04b) | 28.540 | 2,7% |
| **513 deputados** | **711.796** | **2,96%** |

Os limiares de `ParametrosDoMotor` foram escolhidos contra 28.540 despesas e atravessaram
711.796 sem derrapar. É a validação mais forte que o motor recebeu — e o contrário teria sido
igualmente informativo: taxa muito acima significaria limiar frouxo, muito abaixo significaria
tabela curada pobre.

## 3. As abstenções, declaradas

Duas regras ficam praticamente mudas, e **por falta de dado, não por ausência de achado**. A
`MetadadosDeCobertura` reporta as duas como `SemDado`, não como "nenhum indicador encontrado".

| Regra | Flags | Por quê |
|---|---|---|
| `REG_TX_002` teto da cota | **0** | A tabela da CEAP só cobre 2026. Os valores de 2023–2025 estão nos Atos da Mesa e ainda não foram levantados |
| `REG_KYB_004` MEI acima do teto | **14** | 65% dos fornecedores não têm `opcao_pelo_mei` informada pela BrasilAPI |

Publicar essas duas como silêncio conclusivo seria o produto afirmando mais do que sabe. É a
mesma disciplina do [ADR-0004](../decisoes/ADR-0004-nomenclatura-tecnica.md), aplicada à
cobertura em vez de ao vocabulário.

### Flags por regra

| Regra | Flags | % das despesas |
|---|---|---|
| `REG_KYB_001` CNAE divergente | 6.531 | 0,918% |
| `REG_TX_003` padrão mensal idêntico | 5.973 | 0,839% |
| `REG_GEO_002` fora da base eleitoral | 2.941 | 0,413% |
| `REG_TX_004` pico de consumo | 1.887 | 0,265% |
| `REG_KYB_005` nota inicial | 1.751 | 0,246% |
| `REG_KYB_002` situação cadastral | 681 | 0,096% |
| `REG_KYB_003` empresa recém-nascida | 535 | 0,075% |
| `REG_TX_001` fracionamento | 535 | 0,075% |
| `REG_GEO_003` recesso | 200 | 0,028% |
| `REG_KYB_004` MEI | 14 | 0,002% |
| `REG_TX_002` teto | 0 | — |
| `REG_NET_004` concentração partidária | **19** | passada de rede |

## 4. `REG_NET_004` rodou com significado pela primeira vez

A regra declara piso de **100 parlamentares**. Até aqui só tinha rodado sobre 5 e 20 — sempre
abaixo do piso, sempre com o aviso de que os achados eram de amostra. Com 513, ela fala.

### Os cinco fornecedores

| Fornecedor | Abertura | CNAE | UF | Deputados | Partido | Total |
|---|---|---|---|---|---|---|
| ESTRATEGIA COMUNICACOES | 2023-01-13 | 7319004 publicidade | MG | **6 de 6** | PL | R$ 802.166 |
| D. M. M. SILVA | 2024-05-27 | 7319003 marketing | DF | 4 de 4 | PL | R$ 411.000 |
| 53.945.953 LUCAS MACHADO | 2024-02-16 | 7020400 consultoria | DF | 3 de 4 | PT | R$ 506.470 |
| GRAFICA E EDITORA EMBALAGEM | 2023-06-29 | 1811302 impressão | SP | 3 de 3 | PSOL | R$ 334.923 |
| AB COMUNICACAO DIGITAL | 2024-05-27 | 7311400 publicidade | DF | 3 de 3 | PL | R$ 279.000 |

### A revisão manual, e o que ela descartou

A preocupação levantada era falso positivo por **dominância de mercado** — uma companhia aérea
ou telecom atendendo muitos deputados do mesmo partido por puro market share.

**Não ocorre, e há razão estrutural:** a regra exige fornecedor recém-aberto (≤365 dias entre a
abertura na Receita e a primeira nota). Empresa de décadas é filtrada *antes* de a exclusividade
partidária ser avaliada. Os cinco confirmam: todos abertos em 2023–2024, com 3 a 6 clientes no
total — nenhum com dezenas.

### A medição que valida a regra

| | Fornecedores com 3+ deputados | Partidariamente exclusivos | % |
|---|---|---|---|
| Recém-abertos (desde 2022) | 116 | 6 | **5,2%** |
| Todos, qualquer idade | 1.290 | 23 | **1,8%** |

A exclusividade partidária **não é comum**. Ela isola 5,2% de um conjunto que já era estreito.
Se fosse artefato do desenho, a maioria dos fornecedores novos seria exclusiva — e não é.

*(O recorte "aberto desde 2022" é mais largo que os 365 dias da regra, o que explica 6 aqui
contra 5 marcados.)*

### O padrão

**Quatro dos cinco são do domínio de comunicação** — publicidade e gráfica. É o setor de maior
discricionariedade e contratação por indicação.

**`ESTRATEGIA COMUNICACOES` é o sinal mais forte**: 6 de 6 clientes, todos PL. O PL tem 98 dos
513 assentos; seis de seis por acaso fica na ordem de 0,005%.

**A regra não pega um lado só**: PL (3 casos), PT (1), PSOL (1). O PL aparecer mais é coerente
com ser a maior bancada.

### Limitação conhecida

**O partido é o do momento da coleta.** Uma janela partidária pode criar ou destruir
exclusividade artificialmente: um fornecedor que atendia três deputados de legendas distintas
vira "exclusivo" se dois trocarem de partido. A regra não sabe disso, e o grau de confiança de
0,7 — em vez de 1,0 — existe em parte por causa disso.

## 5. Armazenamento: o [ADR-0010](../decisoes/ADR-0010-resolucoes-produto-infraestrutura.md) segurou

**317 MB** para a legislatura inteira, contra os 512 MB do tier gratuito do Neon. Folga de 38%.

| Esquema | Bytes por despesa |
|---|---|
| Original | 624 |
| Depois de `hash_unico` e da URL | 450 |
| Depois do catálogo de tipos | **376** |

Sem as três otimizações da [Etapa 04c](etapa-04c-armazenamento.md), a base fecharia em torno de
520 MB — **acima do limite**. A janela de retenção continua desligada e não foi necessária.

### O que as otimizações entregaram na prática

| | |
|---|---|
| Carteira parlamentar extraída | **511 de 513** deputados |
| Despesas com documento publicado | 70,5% |
| Notas precisando de `ano_do_arquivo` | 5.278 (0,7%) |
| Tipos fora do catálogo | 1.820 despesas, com o nome preservado |
| Estornos aceitos | **35.398** (5,0%) |

Os 35.398 estornos merecem destaque: a primeira coleta de todas os rejeitava, e rejeitá-los
**inflava** o gasto — passagem comprada e cancelada apareceria como gasta. Aqui eles abatem
R$ 35 milhões do volume, corretamente.

## 6. A coleta não foi limpa, e isso rendeu três correções

Onze horas de coleta expuseram três defeitos que nenhum teste com dado inventado teria achado.
Cada um está registrado em detalhe no
[incidente de 2026-08-21](incidente-2026-08-21-despesas-vazia.md) e nos commits.

### 6.1 A fonte respondeu `200` e mentiu

A rota `/despesas` passou a devolver lista vazia para todo deputado. A primeira coleta terminou
em 26 minutos com `Falhas: 0` — tecnicamente correto e completamente enganoso.

**Seguraram:** o alerta de sanidade (497 disparos) e `AindaVale`, que impede checkpoint com
alerta de blindar o deputado. Zero perda.

**Faltava:** parar. Dez alertas seguidos agora derrubam a passada.

### 6.2 Truncamento silencioso na paginação

Página vazia no meio encerrava o laço em silêncio. Um deputado ficaria com um terço das
despesas e **nenhum alerta dispararia** — ele *tem* dado, só não todo.

O cliente passou a conferir contra o link `last`. E a medição desmentiu uma premissa minha: a
API manda `last` **sempre**, inclusive em página única (`last=1`), então a ausência dele é ela
mesma sinal de degradação.

### 6.3 A listagem caindo derrubava tudo

Uma queda de internet de uma hora abriu o circuit breaker do Polly durante a paginação da
**lista de deputados** — o único ponto do laço sem proteção. A coleta morreu em 482 de 513 e
levou junto o resumo final.

A enumeração virou manual, porque a falha precisa ser capturada separadamente do corpo do laço:
"a fonte parou de listar" e "este deputado deu problema" exigem desfechos diferentes.

### 6.4 E um quarto, que só a escala revelou

Três fornecedores — **"MOREIRA LAVAGEM E POLIMENTOS"**, **"ESQUILOS LAVAGEM AUTOMOTIVA"** e
**"RODO SERVICE LAVAGEM E CONSERVACAO"** — faziam `REG_KYB_001` estourar. A descrição interpola
a razão social, e **"lavagem"** está na lista de proibidos do ADR-0004 por causa de *lavagem de
dinheiro*.

**Lava-jatos derrubavam o filtro anti-acusação.**

O prejuízo era maior que os 6 erros no log: quando uma regra estoura, o `ToList` do motor
descarta **todas** as flags daquele parlamentar. `REG_KYB_001` foi de 6.350 para 6.531 — **181
flags legítimas perdidas** por causa do ramo de três empresas.

A correção é de princípio, não de lista: trecho entre aspas deixa de ser examinado. O filtro
existe para conter o que o *produto* afirma; o nome de uma empresa que existe no mundo não é
escolha de palavra nossa.

Para achar isso foi preciso **alterar o código** — o worker imprimia só a contagem de falhas,
nunca quais. Essa lacuna de observabilidade foi corrigida junto.

## 7. Pendências

| Pendência | Impacto | Observação |
|---|---|---|
| Limites da CEAP de 2023–2025 | **Alto** | `REG_TX_002` em zero até que sejam levantados nos Atos da Mesa |
| Catalogar os 4 tipos que apareceram | Baixo | Grafia exata em mãos; migration como a dos dezesseis primeiros |
| Janela partidária distorce `REG_NET_004` | Médio | O partido é o do momento da coleta. Sem histórico de filiação, não há como corrigir |
| Cliente do Senado | Médio | O conversor e os testes existem ([Etapa 01b](etapa-01b-contratos-senado.md)); falta decidir a interface, já que a CEAPS é por ano e não por parlamentar |
| Tabela de limites da CEAPS | Alto para o Senado | Não vem em API |

## 8. Próxima etapa

**Etapa 05 — API pública**, agora sobre 513 parlamentares e 711.796 despesas reais. Ranking por
volume, perfil, listagem de despesas com os indicadores, e a `MetadadosDeCobertura` exposta ao
lado dos achados — porque publicar as flags sem ela deixaria o leitor supor que o silêncio de
`REG_TX_002` significa aprovação.
