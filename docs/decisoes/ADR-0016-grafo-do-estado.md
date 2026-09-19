# ADR-0016 — O grafo do Estado é outro contexto, não outro ranking

**Data:** 2026-09-19
**Situação:** aceito
**Contexto anterior:** [ADR-0007](ADR-0007-postura-permissiva-do-motor.md),
[ADR-0009](ADR-0009-dois-numeros-nao-um.md),
[ADR-0013](ADR-0013-identidade-visual-e-taxonomia-de-risco.md)

---

## Contexto

O produto nasceu respondendo **"quanto se gastou"**: a cota parlamentar, os fornecedores, os
indicadores de um motor determinístico. A partir da referência
[CivLab](https://graph.civlab.org/us), entra uma pergunta diferente — **"que órgãos existem,
de quem cada um depende, sob que norma, e o que andou mudando"**.

A fonte é o [SIORG](../contratos/siorg.md), o cadastro oficial da estrutura organizacional.
Ele não conhece parlamentar, não conhece despesa e não produz juízo nenhum sobre conduta.

## Decisões

### 1. Contexto separado no mesmo repositório e no mesmo banco

Tabelas próprias (`unidades_do_estado`, `mudancas_estruturais`), namespace próprio
(`Domain.Estado`), serviço de coleta próprio, endpoints próprios em `/api/v1/estado`.
**Sem chave estrangeira para `parlamentares`**: a ligação entre o Senado como órgão e os 118
senadores da base é semântica, não referencial — são dois cadastros federais que não se
conhecem, e uma FK inventaria uma integridade que a realidade não tem.

Mesmo banco porque o custo de um segundo é real e o benefício seria teórico; separação
lógica porque misturar os dois vocabulários é como um vira métrica do outro sem ninguém
decidir.

### 2. Nenhum vocabulário de risco atravessa a fronteira

Não há gravidade, não há flag, não há índice nesta metade do produto. Um órgão criado por
ato interno **não é suspeito** — é um fato administrativo. A paleta de poderes tem cinco
cores calculadas, e **nenhuma delas é o vermelho de gravidade alta**, que o ADR-0013 reserva
estritamente aos 3,3% de achados de maior peso.

### 3. A primeira coleta é linha de base

O banco vazio significa que não há com o que comparar. Gravar cem mil linhas de "passou a
constar na estrutura oficial" no dia um seria verdade e seria inútil: afogaria a única tela
em que cada linha deveria significar algo que aconteceu no Estado.

Consequência que a interface precisa carregar: **lista de mudanças vazia tem dois
significados opostos** — "ainda não comparamos" e "comparamos e nada mudou". O mapa devolve
`temHistorico` para que a tela diga qual dos dois é. É o mesmo erro que o ADR-0009 proíbe
com os zeros, aplicado a outra tabela.

### 4. Coleta incompleta nunca conclui desaparecimento

Quando uma subárvore falha, as unidades dela somem da resposta **exatamente como sumiriam
se tivessem sido extintas**. Publicar "300 órgãos deixaram de constar" por causa de um tempo
esgotado seria uma manchete falsa produzida por nós.

Só uma coleta em que **nenhum** órgão falhou tem licença para marcar desaparecimento. É o
ADR-0007 num grafo: ausência de dado não é fato.

E mesmo então, o vocabulário é literal: `Desaparecida`, não "extinta". A fonte não distingue
extinção, incorporação e "o cadastro parou de publicar" — afirmar extinção seria inventar um
fato jurídico a partir de um silêncio de API. A unidade **permanece na base**, inativa, com
a última fotografia que tivemos dela.

### 5. Só a esfera federal, por escolha explícita

A lista de órgãos do SIORG traz o Governo do Distrito Federal e mais quatro unidades
estaduais — não é erro da fonte, outros entes também usam o sistema. Mas são nós de um ente
que ninguém decidiu mapear, e um mapa do Estado brasileiro com metade de um estado dentro
confunde mais do que informa.

O worker aceita `--todas-as-esferas` para quem quiser o contrário. O padrão é a escolha.

### 6. O mapa inicial mostra órgãos, não unidades

Medido: a estrutura tem mais de cem mil unidades, e a esmagadora maioria são unidades
administrativas. Desenhar todas produz uma nuvem cinza onde o Executivo inteiro some atrás
dos conselhos. O mapa mostra entes, órgãos e entidades — cerca de 340 nós — e o detalhe de
cada um é buscado quando alguém o abre, **um nível por vez**.

O tamanho de cada fatia é o número de unidades que pendem dela. Sem isso, um ministério e um
conselho de três pessoas ocupariam o mesmo espaço, e o mapa informaria sobre a nossa lista
em vez de sobre o Estado.

### 7. A metade das pessoas está faltando, e a página diz **o que** falta

A referência mostra cadeiras vagas e ocupantes interinos. O SIORG não publica cargo nem
titular — não há campo, em nenhum endpoint.

Medido no fim da etapa, o que falta são **três coisas distintas**, e tratá-las como uma só
foi um erro meu:

| O que | Situação |
|---|---|
| **Quem ocupa cada cargo hoje** | **Público e sem cadastro** — o Portal da Transparência publica 778.337 vínculos por mês. Trava na ponte SIAFI ↔ SIORG: por nome, casam 145 dos 354 órgãos (58,5% dos servidores), e casar o resto por aproximação é o palpite que este produto não dá |
| **Nomeações e exonerações no tempo** | DOU, via INLABS, que exige cadastro |
| **Cadeiras vagas** | Nenhuma das duas fontes publica os cargos **autorizados**, que é o denominador da conta |

A página declara essas ausências, cada uma pelo que é, em vez de deixar o mapa parecer
completo. E a ingestão de 778 mil servidores nomeados é dado pessoal em escala: a avaliação
de legítimo interesse pendente passa a valer para ela, **antes** de qualquer código.

## Consequências

- Há um segundo eixo de conteúdo indexável, com valor próprio e sem sobreposição com os
  rankings — e sem nenhuma exposição jurídica nova, porque nada aqui descreve conduta.
- A coleta é **sequencial por natureza da fonte** (uma requisição por IP), então ela é lenta
  e não há como acelerá-la em camada nenhuma. O agendamento tem de contar com isso.
- O histórico estrutural só começa a existir na segunda coleta. Quanto antes a primeira
  rodar em produção, antes o produto tem o que mostrar.
- Uma nova família de cores entra na paleta, com contraste calculado para os dois temas e
  registrada em `globals.css`, como o ADR-0013 exige.
