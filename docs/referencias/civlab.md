# CivLab — o mapa da referência

Explorado em 2026-09-19 contra `https://graph.civlab.org/us`, interagindo com cada
controle. Este documento é **o que eles fazem**, medido, e **o que disso cabe no nosso
projeto**. Não é aspiração: cada item tem uma coluna dizendo se temos dado para ele.

---

## 1. A ideia central do desenho

O grafo é uma **engrenagem radial** com duas dimensões independentes, e é isso que o faz
funcionar:

| Dimensão | O que codifica |
|---|---|
| **Ângulo (setor)** | O **poder**: Judicial, Legislativo, Executivo — cada um um setor colorido do disco, com o rótulo curvado na borda |
| **Raio (anel)** | O **nível de autoridade**: `HIGHEST AUTHORITY` → `OVERSIGHT` → `ADMINISTRATION` → subagências na borda |

> **O raio não é profundidade na árvore — é poder.** Essa é a diferença mais importante
> para o nosso desenho atual, em que o raio é só "quantos níveis abaixo da raiz". Um
> conselho de assessoramento ligado direto ao presidente está a um salto da raiz, mas não
> tem a autoridade de um ministério; o anel diz isso, a profundidade não.

No centro, um nó em forma de engrenagem laranja: **"People of the United States"**. A
fonte da legitimidade, e não um órgão. Todas as arestas de eleição partem dele.

Bandas secundárias aparecem como arcos rotulados dentro de um setor — `CABINET` dentro do
Executivo, `CONGRESS` dentro do Legislativo (este como uma cápsula única contendo as duas
câmaras).

## 2. A legenda — forma e traço, nunca só cor

Duas famílias, e é exatamente o princípio do ADR-0013 aplicado a um grafo.

**ENTIDADES (9 formas de nó)**
Elected offices · Department heads · Agencies & departments · Advisory bodies ·
Commissions · Corporations · Courts · Quasi-official · Sub-agencies

**RELAÇÕES (9 estilos de aresta)**
Elects · Appoints · Confirms · Oversees · Administers · Advises · Heads · Offices ·
Ex officio

Cada relação tem traço próprio (cheio, tracejado, pontilhado) **e seta de direção**. A cor
da aresta segue o poder de origem — a aresta do Congresso para um ministério é vermelha,
a do Presidente é azul.

## 3. As interações, uma a uma

| Gesto | O que acontece |
|---|---|
| **Passar o mouse** | O nó acende na cor cheia do setor e aparece um rótulo flutuante com o nome ("United States House of Representatives") |
| **Clicar** | A roda **gira** para pôr o nó selecionado embaixo, no centro; **todas as outras arestas somem** e ficam só as dele; o resto do disco vira contorno; o nome aparece num selo no rodapé; a URL e o `<title>` mudam |
| **Painel** | Abre abaixo (telas estreitas) ou ao lado (telas largas) |
| **Setas ‹ ›** | Navegam entre nós / voltam para o grafo inteiro |
| **Busca (lupa)** | Sobreposição com fundo desfocado, resultados agrupados por "Entities", cada um com o quadradinho da cor do poder |
| **Legend ⌄** | Abre a legenda completa, com rolagem |
| **Graph ⌄** | Alterna entre as duas vistas: **Graph** e **Power map** |
| **Sol/lua** | Tema claro e escuro — no claro os setores viram tintas pálidas e as arestas ficam índigo saturado |
| **CivLab / US Gov ⌄** | Troca de governo: *SF Gov*, *US Gov* (novo), e **"Request a graph — vote for the next country, state, or city"** |

## 4. O painel de uma unidade

Ordem exata do conteúdo:

1. **Título** — o nome da entidade
2. **Descrição em prosa** — um parágrafo de verdade, explicando a função e citando a base
   legal ("…estabelecido pelo National Security Act Amendments of 1949… (10 U.S.C. 111)")
3. **Dois links** — `Legal Source` e `Official Website`
4. **O ocupante** — rótulo do cargo ("Secretary of Defense"), **retrato**, nome
   ("Pete Hegseth"), e **"Appointed 2025"** (ou "Elected 2025" num cargo eletivo). Sem
   retrato, um monograma com as iniciais
5. **Duas abas**:
   - **News** — feed com manchete, data, trecho, *chip* do domínio da fonte com seta de
     link externo, e miniatura
   - **Who's connected?** — as relações **agrupadas por tipo**: `Elected by`, `Appoints`,
     `Advised by`, `Sub-agencies`… Cada grupo com contagem no canto (`36`), itens em duas
     colunas com bolinha de cor, e `See all (36)`

> **O detalhe que mais vale copiar:** dentro de `Appoints`, cada item traz **"1 of 1 seats"**
> ou **"7 of 7 seats"**, e o grupo resume **"Appoints 881 seats"**. A relação é
> *quantificada* — não "nomeia esse conselho", mas "nomeia 7 das 7 cadeiras dele". É essa
> contabilidade de cadeiras que permite existir "6 vagas, 86 interinos".

## 5. A tela inicial (sem nó selecionado)

**Latest News** — um parágrafo sintetizado com **chips de entidade embutidos no texto**
(bolinha colorida + nome sublinhado que leva ao nó: CISA, The Senate, Labor Department,
FDA) e notas de rodapé numeradas apontando as fontes. Abaixo, uma fileira de retratos
sobrepostos com "Who's in the news" e o link **"Power map ›"**.

**Latest Changes** — com seletor **7D / 30D / 90D**:

| | |
|---|---|
| `SEATS VACANT` | **6** — *no current officeholder* |
| `ACTING OFFICIALS` | **86** — *serving unconfirmed* |
| `LAST CHANGE` | **3d** — *ago, on Sep 16* |

E uma **linha do tempo** horizontal de 30 dias com bolhas contendo a quantidade de
mudanças de cada dia, e "15 changes in window".

Cada mudança é um cartão: bolinha do poder na sarjeta, **selo de tipo** (`APPOINTED`,
`DEPARTURE`), o nome do cargo, e um bloco **OUT / IN**:

```
OUT   Loretta Greene        ← riscado
IN    Seat now vacant       ← itálico, quando fica vaga
```

> **O OUT/IN riscado é a melhor ideia de apresentação do site inteiro.** Diz "saiu fulano,
> entrou beltrano" sem uma frase, e o riscado marca a saída sem ambiguidade.

**Overview** — "Top level counts", em duas colunas:
*By Type*: Elected offices 4 · Members of Congress 535 · Departments & agencies 329 ·
Military services 6 · Commissions 46 · Advisory bodies 14 · Courts 19 · Government
corporations 18 · Quasi-official 45
*By Branch*: Legislative 19 · Executive 343 · Judicial 17 · **Independent 103**

## 6. O Power map

Vista alternativa, de **pessoas e não de instituições**:

> *"The 20 people named most across 1,005 articles from 3 sources in the last 90 days"*

Retratos circulares com anel colorido pelo partido, **tamanho proporcional ao número de
artigos**, nome, cargo, contagem ("228 articles") e "In the news 9h ago" em vermelho para
os recentes. Dispostos em camadas verticais, ligados por linhas finas.

> **O que fizemos com isso.** A aresta deles é "citados nos mesmos artigos", que mede a
> imprensa; não temos fonte de notícias, e comprar uma não resolveria o problema de fundo.
> A **ideia** — uma vista de pessoas, com retrato, ao lado da vista de instituições — é boa,
> e sobrevive com outra aresta: **fornecedor em comum**, que é um fato da nossa base, com
> CNPJ e nota fiscal. Virou `/rede`, e o [ADR-0017](../decisoes/ADR-0017-mapa-de-fornecedores.md)
> traz a medição que define o corte.

## 7. Como é construído

Medido na aba de rede: **Next.js na Vercel, páginas estáticas com `_next/data/*.json`
pré-buscado** — a mesma aposta do nosso [ADR-0001](../decisoes/ADR-0001-frontend-nextjs.md).
Ao abrir um painel, eles **pré-buscam o JSON de todos os nós ligados**, e é por isso que
navegar parece instantâneo.

A taxonomia das URLs é por tipo de entidade, com slug `{gov}-{nome-em-kebab}`:

```
/us/elected/us-president-of-the-united-states
/us/departments/us-department-of-labor
/us/dept-heads/us-administrator-of-general-services
/us/commissions/us-federal-reserve-system
/us/advisories/us-national-security-council
```

Retratos vêm do **Wikimedia Commons**, servidos pelo otimizador de imagem do Next.

---

## 8. O que disso cabe em nós — e o que não cabe

| Recurso do CivLab | Situação | Observação |
|---|---|---|
| Setor por poder | ✅ **feito** | Com piso de 30° por poder, declarado na tela |
| **Anel por nível de autoridade** | ✅ **feito** | Virou **natureza jurídica** — ente, administração direta, administração indireta. Melhor que a referência: lá é editorial, aqui é constitucional |
| **Forma por tipo de entidade** | ✅ **feito** | Círculo, quadrado, hexágono, losango |
| **Estilo de traço por tipo de relação** | ❌ | **Só temos subordinação.** Nomeação, confirmação e supervisão não existem no SIORG |
| Hover com rótulo | ✅ **feito** | |
| Clicar → girar e isolar arestas | ✅ **feito** | |
| Painel lateral | ✅ **feito** | Tipo, poder, natureza, base legal, subordinadas, servidores, a quem responde e quem responde a ela |
| Busca com sobreposição | ✅ **feito** | No cliente, sobre os 336 nós; insensível a acento, atalho `/`, navegação por teclado |
| **Cartão OUT/IN com o antigo riscado** | ✅ **feito** | Serve à estrutura: renomeação, mudança de sigla, mudança de pai |
| **Descrição em prosa** | ❌ | O SIORG não publica descrição. Escrever à mão para 96 mil unidades é inviável; para os ~330 órgãos, é possível |
| Link para a fonte legal / site oficial | ❌ | O SIORG não traz nenhum dos dois |
| **Ocupante com retrato** | ❌ | Precisa do DOU (INLABS, seu cadastro) ou de outra fonte |
| Feed de notícias | ❌ | Fonte nova, e um custo recorrente |
| **"Who's connected?" agrupado** | ⚠️ | Temos o pai e os filhos. Um grupo só, não nove |
| **Contabilidade de cadeiras ("7 of 7 seats")** | ❌ | Ninguém publica cargos autorizados no Brasil — já procurei |
| Cadeiras vagas / interinos | ❌ | Depende do item acima |
| **Linha do tempo de mudanças** | ✅ | **Já temos `MudancaEstrutural`** — falta só a tela |
| **Cartão OUT/IN riscado** | ⚠️ | Serve tal e qual para renomeação e mudança de pai; o de pessoas depende do DOU |
| Overview por tipo e por poder | ✅ | Já calculamos os dois |
| Busca | ✅ | Trivial sobre os 336 nós |
| **Power map** | ✅ **feito, com outra aresta** | Ver [ADR-0017](../decisoes/ADR-0017-mapa-de-fornecedores.md). Notícias não temos; fornecedor em comum temos. Virou `/rede` |
| Tema claro/escuro | ✅ | Já temos |
| Troca de governo | — | Não se aplica: um país só |

### O que isso quer dizer

**Metade do CivLab é desenho e interação, e essa metade é nossa agora** — o grafo de nós e
arestas, o hover, o clique que gira e isola, o painel, a busca, a linha do tempo, os
totais. Nada disso depende de dado novo.

**A outra metade depende de fontes que o Brasil não publica do mesmo jeito.** As relações
tipadas (nomeia, confirma, supervisiona) e a contabilidade de cadeiras são o coração do
CivLab, e elas vêm de um trabalho editorial que a lei americana facilita: os cargos
federais são criados por norma que diz quantas cadeiras existem e quem nomeia. No Brasil
isso está espalhado em decretos de estrutura regimental, não em cadastro consultável.

> **A tentação a evitar:** desenhar nove tipos de aresta e preencher todos com
> subordinação, para o desenho ficar parecido. Isso seria inventar relação — o mesmo erro
> que a ponte SIAPE↔SIORG se recusa a cometer. Enquanto a fonte der um tipo de aresta só,
> o grafo mostra um tipo de aresta só, e a legenda diz isso.
