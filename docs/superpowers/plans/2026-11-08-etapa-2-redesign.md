# Etapa 2: redesenho do frontend, plano de implementação

> **Para agentes:** SUB-SKILL OBRIGATÓRIA: use superpowers:subagent-driven-development (recomendado) ou superpowers:executing-plans para executar tarefa a tarefa. Os passos usam caixas (`- [ ]`) para acompanhamento.

**Objetivo:** levar todas as telas de `frontend/` para a direção visual escolhida pelo Gustavo (A livro-razão, B reportagem ou C cidadão), terminando no perfil e na revelação progressiva da lista de despesas.

**Arquitetura:** os tokens semânticos de `globals.css` são a única fonte de cor, tipo, raio e espaçamento. Primeiro mudam os tokens e os componentes base (tags, botão, campo, superfície, tabela), depois cada tela consome esses componentes. Os testes do Vitest descrevem comportamento e texto, não classe CSS; só mudam quando o markup muda de propósito.

**Stack:** Next.js 16.3.6 (App Router, `next/font/google`), React 19.2.8, Tailwind v4 configurado por `@theme` em CSS, Vitest 4 com Testing Library e jsdom.

**Especificação:** plano mestre `docs/superpowers/plans/2026-11-07-ultima-versao.md` (Etapa 2, na worktree `civic-project-decision-db798c`), ADR-0013 (`docs/decisoes/ADR-0013-identidade-visual-e-taxonomia-de-risco.md`, com a emenda de 18/09), `docs/pendencias.md` §2 e a página das três direções: https://claude.ai/artifact/45RnkoRhhXLUNZtD4AGtkD

## Restrições globais

- A matriz de risco não muda de valor nem de forma: baixa `#F8FAFC`/`#334155` com `i` em círculo; média `#FEF3C7`/`#78350F` com `!` em triângulo; alta `#FEE2E2`/`#991B1B` com `×` em octógono; não avaliado `#F1F5F9`/`#475569` com `–` em círculo tracejado. No escuro, os pares da emenda de 18/09.
- Vermelho só para gravidade alta. Nenhuma cor de marca, gráfico ou poder pode ser vermelha ou âmbar.
- Sem `uppercase` em lugar nenhum (hoje: 34 ocorrências em 14 arquivos). Rótulo é caixa de frase.
- Toda tag de gravidade tem `aria-label` com nível e código da regra.
- Regra abstida nunca aparece como aprovada; zero achados com regra abstida diz quantas regras foram avaliadas.
- `GlosaConhecida` falso omite a quebra declarado × glosa; nunca `R$ 0,00`.
- Senado: "Senado não disponibiliza o comprovante" como tag inativa, no lugar do botão.
- CPF nunca aparece em tela, print ou fixture nova.
- Toda cor nova é medida antes de entrar: texto ≥ 4,5:1, elemento não textual ≥ 3:1, nos dois temas. O cálculo vai num comentário ao lado do token, como já é feito em `globals.css`.
- Tema em três estados (sistema, claro, escuro) com o script inline no `<head>`; nada pode piscar branco.
- Corpo de texto ≥ 14px no computador e ≥ 15px no celular; nenhum texto de leitura abaixo de 12px; alvo de toque ≥ 44px no celular.
- Sem rolagem horizontal da página em 375px. Tabela larga só rola dentro do próprio contêiner, e o ranking no celular não rola de lado.
- `prefers-reduced-motion` respeitado; animação só em `transform` e `opacity`.
- Não mexer em `backend/`. Se a API mudar, a sessão da Etapa 1 avisa.
- Sem push, sem deploy. Commits pequenos na branch `claude/etapa2-redesign-2944fd`.

## Foco de revisão

1. **Nome de fornecedor longo ou em caixa alta da fonte** (razão social de 70 caracteres, "COMBUSTÍVEIS E LUBRIFICANTES."): deve quebrar ou truncar com `title`, sem empurrar o valor para fora da linha no celular. Teste na Tarefa 6.
2. **Parlamentar sem foto, sem partido ou senador sem comprovante**: o retrato cai para iniciais, e o Senado mostra a tag inativa. Teste na Tarefa 8.
3. **Perfil com zero achados e regras abstidas**: deve dizer "nenhum achado nas 7 regras que puderam ser aplicadas", nunca "nenhum indicador". Teste na Tarefa 8.
4. **Despesa com vários achados** (até 3 regras numa linha): a linha fechada mostra a tag de maior gravidade e "+2", a aberta mostra todas. Teste na Tarefa 9.
5. **Navegação por teclado na lista progressiva**: `Enter` e `Espaço` abrem e fecham, o foco não pula para o topo, e o link "Ver nota fiscal" é alcançável só quando a linha está aberta. Teste na Tarefa 9.

---

## Valores por direção

A Tarefa 1 grava **só a coluna escolhida**. Os nomes dos tokens são os mesmos nas três, para que o resto do plano não dependa da escolha.

| Token (`--color-*`) | A claro / escuro | B claro / escuro | C claro / escuro |
|---|---|---|---|
| `fundo` | `#FFFFFF` / `#0E1412` | `#FFFFFF` / `#16121B` | `#F4F7F5` / `#0D1914` |
| `surface` | `#F5F7F6` / `#151D1A` | `#F6F4F8` / `#201A27` | `#FFFFFF` / `#14241D` |
| `borda-suave` | `#D5DDD9` / `#26322D` | `#DCD6E2` / `#352C40` | `#D9E3DD` / `#253A30` |
| `borda-estrutural` | `#6B7A73` (4,51) / `#5E6E67` (3,46) | `#7A4AA8` / `#A77FD3` | `#4D5F56` / `#A2B8AC` |
| `texto` | `#1C2622` (15,56) / `#E3EAE6` (15,24) | `#1E1A24` (17,09) / `#EDE8F2` (15,33) | `#13241C` (16,19) / `#E6F0EA` (15,45) |
| `texto-suave` | `#52615A` (6,53) / `#A3B2AB` (7,78) | `#5C5566` (7,13) / `#B3A9BF` (7,54) | `#4D5F56` (6,80) / `#A2B8AC` (7,68) |
| `titulo` (marca, botão) | `#0F5132` (9,36) / `#7FD1A8` (10,28) | `#5B2A86` (9,90) / `#CDB0EC` (9,69) | `#12372A` (13,08) / `#9BE3BC` (12,10) |
| `sobre-titulo` | `#FFFFFF` / `#0E1412` | `#FFFFFF` / `#16121B` | `#FFFFFF` / `#0D1914` |
| `acento` (barra) | `#2F7D5B` (4,99) / `#4FA57D` (6,22) | `#7A4AA8` (6,23) / `#A77FD3` (5,82) | `#2E9A5E` (3,56) / `#3BB273` (6,69) |
| `realce` | `#E3F1EA` / `#16302A` | `#EFE6F7` / `#2E2340` | `#DDF3E6` / `#1C3A2C` |

| | A | B | C |
|---|---|---|---|
| Fonte de corpo (`--font-sans`) | IBM Plex Sans | Public Sans | Figtree |
| Fonte de destaque (`--font-display`) | IBM Plex Sans Condensed 600 | Newsreader (serifa, `opsz`) | Bricolage Grotesque 700 |
| `--radius-cartao` / `--radius-campo` | `0.25rem` / `0.1875rem` | `0` / `0.1875rem` | `1.125rem` / `0.875rem` |
| Sombra | nenhuma | nenhuma | nenhuma |
| Densidade (altura de linha de tabela) | 34px | 48px | lista de cartões, 64px |
| Ranking no celular | lista de duas linhas | lista com frase | cartão de pessoa |

Todas as fontes vêm de `next/font/google` com `subsets: ["latin", "latin-ext"]`.

---

### Tarefa 1: tokens, fontes e limpeza do cromo global

**Arquivos:**
- Modificar: `frontend/src/app/globals.css` (bloco `@theme` e os dois blocos de tema escuro)
- Modificar: `frontend/src/app/layout.tsx` (fontes)
- Teste: `frontend/src/lib/tokens.test.ts` (novo)

**Interfaces:**
- Produz: os tokens da tabela acima com os nomes atuais (`--color-fundo`, `--color-titulo` etc.), mais `--font-sans` e `--font-display` apontando para as variáveis `--fonte-corpo` e `--fonte-destaque`.

- [ ] **Passo 1: teste que falha.** `tokens.test.ts` lê `globals.css` como texto e calcula contraste com uma função `contraste(hexA, hexB): number` (luminância relativa WCAG) definida no próprio teste.
  - `it("todo par texto/fundo passa 4,5 nos dois temas")`: para `texto`, `texto-suave`, `titulo` contra `fundo` e `surface`, e `sobre-titulo` contra `titulo`, espera `>= 4.5`.
  - `it("acento e borda estrutural passam 3")`: espera `>= 3`.
  - `it("a matriz de risco não mudou")`: os oito pares do ADR-0013 aparecem literalmente.
- [ ] **Passo 2:** `npm test -- tokens` falha nos valores novos.
- [ ] **Passo 3:** gravar a coluna da direção escolhida em `globals.css`, com o cálculo em comentário; trocar as fontes em `layout.tsx`; apagar `--shadow-cartao` e `--shadow-flutuante` se a direção não usa sombra (A, B e C não usam).
- [ ] **Passo 4:** `npm test`, `npm run lint` e `npm run build` verdes.
- [ ] **Passo 5:** commit `Etapa 2: tokens e fontes da direção X`.

### Tarefa 2: componentes de risco e de estado

**Arquivos:**
- Modificar: `frontend/src/componentes/TagDeGravidade.tsx`, `Abstencao.tsx` (`TagDeAbstencao`, `BlocoDeCobertura`), `Comprovante.tsx`
- Teste: `frontend/src/componentes/componentes.test.tsx`

**Interfaces:**
- Consome: tokens da Tarefa 1.
- Produz: `TagDeGravidade({ gravidade, regra?, contagem? })`, em que `contagem` mostra o número dentro da tag (usada no ranking) e `regra` entra no `aria-label`.

- [ ] **Passo 1: testes que falham.** `it("tag mostra a contagem e nomeia nível e regra")`: `render(<TagDeGravidade gravidade="Media" regra="REG_FN_001" contagem={49} />)` → `getByLabelText("Gravidade média, regra REG_FN_001, 49 achados")` e texto `49`. `it("nenhuma tag usa caixa alta")`: nenhum elemento renderizado tem classe `uppercase`.
- [ ] **Passo 2:** rodar e ver falhar.
- [ ] **Passo 3:** implementar a prop `contagem`; forma em SVG, com o mesmo desenho dos quatro ícones da página de direções.
- [ ] **Passo 4:** testes verdes.
- [ ] **Passo 5:** commit.

### Tarefa 3: componentes base de interface

**Arquivos:**
- Criar: `frontend/src/componentes/base/Botao.tsx`, `Campo.tsx`, `Superficie.tsx`, `TabelaDeDados.tsx`
- Modificar: `Aviso.tsx`, `Paginacao.tsx`, `AlternadorDeTema.tsx`, `BarraDeFiltros.tsx`
- Teste: `frontend/src/componentes/base/base.test.tsx`

**Interfaces:**
- Produz:
  - `Botao({ variante: "primario" | "secundario" | "texto", href?, externo?, children })`. Com `href` vira `<a>`; `externo` acrescenta `target="_blank"`, `rel="noopener noreferrer"` e o texto oculto "abre em nova aba".
  - `Campo({ id, rotulo, tipo?: "busca" | "texto", ...input })`, sempre com `<label>` visível ou `sr-only`; só `tipo="busca"` leva ícone.
  - `Superficie({ como?: "section" | "div", titulo?, children })`, que aplica `--radius-cartao`, `surface` e borda suave.
  - `TabelaDeDados({ legenda, colunas, children })`, com cabeçalho fixo, régua estrutural e contêiner `overflow-x-auto`.

- [ ] **Passo 1: testes que falham.** `it("botão externo avisa que abre nova aba")`; `it("campo de busca tem rótulo acessível")`; `it("tabela rola no próprio contêiner")` (o contêiner tem `overflow-x-auto` e `role="region"` com `aria-label` igual à legenda).
- [ ] **Passo 2:** ver falhar.
- [ ] **Passo 3:** implementar; trocar os botões e campos repetidos nos quatro componentes listados.
- [ ] **Passo 4:** `npm test`, lint e build verdes.
- [ ] **Passo 5:** commit.

### Tarefa 4: cabeçalho, rodapé e página de erro

**Arquivos:** `Cabecalho.tsx`, `Rodape.tsx`, `app/error.tsx`, `app/not-found.tsx`; teste em `componentes.test.tsx`.

- [ ] **Passo 1:** `it("cabeçalho marca a seção atual com aria-current")` e `it("menu do celular abre com botão de 44px rotulado")`.
- [ ] **Passo 2:** ver falhar.
- [ ] **Passo 3:** implementar na direção escolhida; o rodapé perde o parágrafo duplicado com o aviso da home e fica com três linhas.
- [ ] **Passo 4:** verde; conferir as quatro combinações no navegador embutido (claro/escuro × 1440/390).
- [ ] **Passo 5:** commit.

### Tarefa 5: home

**Arquivos:** `app/page.tsx`, `BuscaRapidaHome.tsx`, `PainelDaCasa.tsx`; testes `BuscaRapidaHome.test.tsx`, `componentes.test.tsx`.

- [ ] **Passo 1:** ajustar os testes de texto que mudam de propósito (título, selo "Auditoria Cívica Contínua" removido, números 1-2-3 removidos da metodologia). Novo: `it("a home não usa caixa alta nem selo decorativo")`.
- [ ] **Passo 2:** ver falhar.
- [ ] **Passo 3:** aplicar o desenho da home da direção (página de direções, seção da letra escolhida).
- [ ] **Passo 4:** testes, lint e build; prints nos quatro modos; contraste medido com o script de prints.
- [ ] **Passo 5:** commit.

### Tarefa 6: ranking (Câmara e Senado, três ordenações)

**Arquivos:** `PaginaDoRanking.tsx`, `TabelaDoRanking.tsx`, `DashboardDaCasa.tsx`, `ExploradorDaCasa.tsx`; testes `DashboardDaCasa.test.tsx`, `Paginacao.test.tsx`, `componentes.test.tsx`.

**Interfaces:** consome `TabelaDeDados`, `Campo`, `TagDeGravidade` com `contagem`.

- [ ] **Passo 1: testes que falham.** `it("o ranking tem lista para o celular e tabela para o computador")`: o jsdom não aplica media query, então o teste confere a estrutura: um `<ol aria-label="Ranking">` dentro de contêiner `md:hidden`, com nome, valor e tags em cada item, e a `<table>` dentro de `hidden md:block`. A largura real em 375px é conferida no navegador (Passo 4). `it("zero achados não vira tag")`: linha com 0/0/0 não renderiza tag. `it("nome longo não empurra o valor")`: razão social de 70 caracteres recebe `title` e classe de truncamento.
- [ ] **Passo 2:** ver falhar.
- [ ] **Passo 3:** implementar; o painel de distribuição usa as cores da matriz, e as barras de partido e UF usam `acento`.
- [ ] **Passo 4:** verde; prints das seis rotas (`/camara`, `/camara/indice`, `/camara/gravidade` e as do Senado) nos quatro modos.
- [ ] **Passo 5:** commit.

### Tarefa 7: telas secundárias (despesa, comparar, Estado, privacidade)

**Arquivos:** `app/despesas/[id]/page.tsx`, `app/comparar/[a]/[b]/page.tsx`, `CompararCom.tsx`, `app/estado/**`, `GrafoDoEstado.tsx`, `PainelDoEstado.tsx`, `PainelDaUnidade.tsx`, `SerieDePessoal.tsx`, `MudancasDaUnidade.tsx`, `VistasDoEstado.tsx`, `PoliticaDePrivacidade.tsx`; testes já existentes desses componentes.

- [ ] **Passo 1:** ajustar só os testes cujo markup muda de propósito; novo `it("título de despesa sai em caixa de frase")` (a fonte manda "COMBUSTÍVEIS E LUBRIFICANTES."; a tela mostra "Combustíveis e lubrificantes", com uma função `caixaDeFrase(texto: string): string` em `lib/frases.ts`, testada em `frases.test.ts` com siglas preservadas: "LOCAÇÃO OU FRETAMENTO DE VEÍCULOS" → "Locação ou fretamento de veículos", "CEAP" fica "CEAP").
- [ ] **Passo 2:** ver falhar.
- [ ] **Passo 3:** implementar. O mapa do Estado no celular (hoje 24.800px de altura) passa a abrir recolhido por poder.
- [ ] **Passo 4:** verde; prints nos quatro modos.
- [ ] **Passo 5:** commit.

### Tarefa 8: perfil do parlamentar

**Arquivos:** `app/parlamentares/[id]/page.tsx`, `ComposicaoDoIndice.tsx`, `EscadaDeFornecedores.tsx`, `SociosEmComum.tsx`, `SerieMensal.tsx`, `VotacaoDoParlamentar.tsx`, `RespostasDoGabinete.tsx`, `opengraph-image.tsx`; testes `ComposicaoDoIndice.test.tsx`, `EscadaDeFornecedores.test.tsx`, `SociosEmComum.test.tsx`, `componentes.test.tsx`.

**Interfaces:** produz `ResumoDoPerfil({ parlamentar, cobertura })` em `componentes/ResumoDoPerfil.tsx`: retrato (ou iniciais), nome no display, a frase-resumo de `lib/frases.ts` e os números da direção.

- [ ] **Passo 1: testes que falham.**
  - `it("sem foto, o retrato mostra iniciais")`.
  - `it("zero achados com regra abstida diz quantas regras foram aplicadas")`: espera "nenhum achado nas 7 regras que puderam ser aplicadas" e a contagem "5 de 12 sem dado público".
  - `it("senador mostra que o Senado não disponibiliza comprovante")`.
  - `it("o resumo cabe na primeira tela")`: a ordem do DOM é retrato, nome, frase, números, e só depois a composição do índice.
- [ ] **Passo 2:** ver falhar.
- [ ] **Passo 3:** implementar. Ordem da página: resumo, composição do índice, despesas com achado (Tarefa 9), o que foi analisado, gasto mês a mês, fornecedores, votos, sócios em comum. As seções depois das despesas abrem recolhidas no celular.
- [ ] **Passo 4:** verde; a altura do perfil no celular cai de 13.950px para menos de 6.000px com tudo recolhido (medir com o script de prints); prints nos quatro modos, deputado e senador.
- [ ] **Passo 5:** commit.

### Tarefa 9: revelação progressiva na lista de despesas

**Arquivos:** `ListaDeDespesas.tsx`, `DespesasComIndicadores.tsx`, `app/parlamentares/[id]/despesas/page.tsx`, `BarraDeFiltros.tsx`; teste novo `ListaDeDespesas.test.tsx`.

**Interfaces:**
- Consome: `TagDeGravidade`, `Botao` (externo), `Comprovante`, `caixaDeFrase`.
- Produz: `LinhaDeDespesa({ despesa, aberta? })`, construída sobre `<details>`/`<summary>` nativos (funciona sem JavaScript e no teclado). Fechada: data, tipo em caixa de frase, fornecedor, tag de maior gravidade com "+N" e valor. Aberta: a descrição de cada achado, CNPJ, competência, glosa (se `GlosaConhecida`) e "Ver nota fiscal" ou a tag do Senado.

- [ ] **Passo 1: testes que falham.**
  - `it("linha fechada mostra só o essencial")`: sem link de nota fiscal no DOM visível; `summary` tem tipo, fornecedor, valor e uma tag.
  - `it("várias regras viram maior gravidade + contagem")`: alta + média + baixa → tag "Alta" e texto "+2".
  - `it("Enter abre e o link fica alcançável")`: `userEvent.keyboard("{Enter}")` no `summary` → `details` com `open` e `getByRole("link", { name: /ver nota fiscal/i })`.
  - `it("glosa desconhecida não mostra R$ 0,00")`.
- [ ] **Passo 2:** ver falhar.
- [ ] **Passo 3:** implementar; na página de despesas o filtro sai da barra lateral e vira uma linha de filtros acima da lista (no celular, um botão "Filtrar" que abre a mesma linha). Abertura animada só com `opacity`/`transform` e desligada em movimento reduzido.
- [ ] **Passo 4:** verde; prints nos quatro modos com uma linha aberta.
- [ ] **Passo 5:** commit.

### Tarefa 10: verificação final

- [ ] **Passo 1:** `npm test`, `npm run lint` e `npm run build` sem erro nem aviso.
- [ ] **Passo 2:** rodar o script de prints (12 telas × 4 modos) e o de medição (largura de rolagem em 375px, alvos abaixo de 44px, texto abaixo de 12px). Esperado: largura 375 em todas, zero texto de leitura abaixo de 12px.
- [ ] **Passo 3:** contraste AA medido no navegador para os pares usados em cada tela (script com `getComputedStyle`), salvo no registro.
- [ ] **Passo 4:** `grep -rn "uppercase" frontend/src` sem resultado; nenhum CPF nos prints (conferir fornecedores pessoa física nas telas de despesa).
- [ ] **Passo 5:** atualizar `docs/pendencias.md` §2 (fechado) e registrar a etapa em `docs/registro/`; commit.

## Fora deste plano

- Mudança de API ou de regra (Etapa 1).
- Logo novo: a marca segue tipográfica, só muda de fonte e cor.
- Página de método/metodologia própria: a direção B sugere uma; fica para depois se o Gustavo quiser.
