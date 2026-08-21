# Etapa 04c — Armazenamento antes da coleta completa

**Data:** 2026-08-21 · **Situação:** ✅ Concluída

---

## 1. Por que esta etapa existe

A coleta completa dos 513 deputados é o passo caro e não repetível de graça do projeto. Três
mudanças de esquema precisavam acontecer **antes** dela, e uma delas é irreversível se
esquecida.

## 2. O que foi medido, e onde eu tinha errado

A ordem de grandeza que motivou a etapa veio de uma extrapolação minha que estava errada por
escalar **tudo** linearmente com o número de deputados, inclusive fornecedores — que se
repetem entre gabinetes.

| Afirmação | Medição real |
|---|---|
| "744 MB para uma legislatura" | ~526 MB antes das otimizações; a extrapolação linear inflava |
| "as otimizações economizam ~43%" | **28%** na tabela `despesas` (624 → 450 bytes por despesa) |
| "não cabe nos 512 MB do tier gratuito" | **Cabe:** ~435 MB projetados, ~85% de utilização |
| "a coleta leva 1,5 a 3 horas" | **~12,6 horas.** 88,6 s por deputado, medido em 5 |

A última é a mais consequente na prática: a coleta é uma tarefa noturna, não de uma tarde. E
o tempo é quase todo espera — 132 fornecedores por deputado × 600 ms de espaçamento na
BrasilAPI dá 79 dos 88 segundos.

### Sobreposição de fornecedores

87,8% dos fornecedores atendem **um único** deputado; a média é 1,22 deputados por
fornecedor. É o número que desmonta a extrapolação linear e, de quebra, o que explica por que
`REG_NET_004` precisa de base grande para dizer alguma coisa.

## 3. O que mudou

### 3.1 `hash_unico`: `char(64)` → `bytea(16)`

O índice único sobre essa coluna era o maior da base inteira — 3.560 kB, mais da metade de
todos os índices de `despesas`. Hexadecimal gastava o dobro do espaço para guardar a mesma
informação duas vezes. Truncar o SHA-256 para 128 bits leva a probabilidade de colisão em
732.000 registros para a ordem de 10⁻²⁷.

Medido depois: o índice caiu de 128 para 55 bytes por linha, **57% menor**.

Efeito colateral que quase passou: `byte[]` compara por **referência** em C#. Um
`HashSet<byte[]>` sem comparador próprio aceitaria a mesma nota duas vezes, e a idempotência
da ingestão — a única defesa contra duplicar um mês inteiro ao reprocessá-lo — simplesmente
não existiria. Daí o `ComparadorDeHash`.

### 3.2 `url_documento` descartada, e o passo irreversível

Dois padrões, verificados contra 20.595 URLs reais:

| Tipo | Padrão | Reconstrução exata |
|---|---|---|
| 4 (NF-e) | `.../nota-fiscal-eletronica?ideDocumentoFiscal={código}` | **100%** |
| 0, 1, 2, 3 | `.../publ/{carteira}/{ano}/{código}.pdf` | 97,7% |

As 241 divergências são **todas no ano da pasta**: nota de novembro ou dezembro é arquivada
no ano seguinte, com 5 exceções que não seguem regra alguma. Havia a tentação de derivar o
ano por heurística — acertaria 99,95%. Numa página pública com nome e foto, 0,05% de links
quebrados não vale os dois bytes: o leitor clicaria e concluiria que o documento foi
retirado. O ano vai para um `smallint`, nulo nos 98,5% em que coincide com o ano da nota.

**O passo irreversível é o número da carteira parlamentar.** Ele não vem de endpoint nenhum —
só existe dentro da URL que está sendo descartada. Verificado antes de agir:

- constante por deputado (zero divergências em 10.676 URLs);
- todo deputado tem ao menos 153 notas com link, então uma basta.

A ingestão passou a extraí-lo da primeira nota com link. Descartar a coluna sem esse passo
perderia a capacidade de linkar documento **para sempre**.

### 3.3 A válvula de retenção, desligada

`ConsolidadoMensal` guarda o gasto somado por mês e sobrevive à poda — é o que permite o
ranking de volume continuar cobrindo o mandato inteiro sem o detalhe nota a nota.

Consolidação **mensal**, e não um total único: uma linha por mês custa quase nada (215 linhas
para 5 deputados, ~22 mil para 513) e preserva a curva de gasto que um total jogaria fora.

`DetalheDisponivel` é a parte que não pode faltar. Sem ela a API mostraria um mês consolidado
como se as notas ainda pudessem ser abertas.

Consolidar e podar são passos separados. O primeiro roda sempre e é reversível — recalcula do
zero. O segundo é destrutivo e só acontece quando alguém passa `--meses`; apagar gasto público
tem de ser um ato explícito. A ordem também importa e tem teste: consolida primeiro, apaga
depois. Invertida, o total do mês podado seria calculado sobre zero despesas e o gasto sumiria
de vez.

**Fica desligada no V1.** A legislatura inteira cabe. Mas a 57 termina em janeiro de 2027
(+~37 MB) e a 58 começa em seguida — a válvula existe para quando isso apertar, e existir
agora evita descobrir que ela falta com o banco cheio.

## 4. O que ficou de fora, e por quê

`tipo_despesa` → tabela de referência rende ~23 MB de uma folga de ~77 MB, atravessa o motor
de regras e 20 arquivos de teste. E, ao contrário da URL, **pode ser normalizado por migration
a qualquer momento sem perder nada** — é essa assimetria que torna adiá-lo seguro.

## 5. Verificação de ponta a ponta

Banco recriado do zero, 5 deputados coletados no esquema novo:

| | |
|---|---|
| Despesas | 5.843, zero rejeitadas |
| Bytes por despesa | 450 (era 624) |
| Carteira extraída | 5 de 5 deputados |
| Notas precisando de `ano_do_arquivo` | 87 (1,5%) |
| Flags | 193 (3,3%), zero falhas de regra |
| Consolidados mensais | 215 |
| Aviso de amostra na passada de rede | disparou corretamente (5 < piso de 100) |

Suíte em **516 testes**.

## 6. Pendências

| Pendência | Impacto | Observação |
|---|---|---|
| A coleta completa leva ~12,6 h | Operacional | Tarefa noturna. Quase toda espera pelo limitador da BrasilAPI |
| Folga de ~77 MB e encolhendo | Médio | A legislatura 57 vai até jan/2027. A poda resolve, mas custa detalhe |
| `tipo_despesa` normalizado | Baixo | ~23 MB, sem urgência e sem risco de perda |
| `REG_NET_004` nunca rodou em base acima do piso | Alto | Só a coleta completa resolve |

## 7. Próxima etapa

**Bloco B — coleta completa dos 513 deputados**, e em paralelo o **spike de contratos do
Senado**, que o [ADR-0011](../decisoes/ADR-0011-v1-e-o-plano-inteiro.md) trouxe para o V1 sem
nenhum contrato verificado.
