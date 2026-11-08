# Etapa 2 (última versão): redesenho na direção C, "cidadão"

Branch `claude/etapa2-redesign-2944fd`, 08/11/2026. Plano em
`docs/superpowers/plans/2026-11-08-etapa-2-redesign.md`; as três direções comparadas em
https://claude.ai/artifact/45RnkoRhhXLUNZtD4AGtkD. O Gustavo escolheu a C.

## O que mudou

- **Tokens e fontes.** Fundo menta-acinzentado `#F4F7F5` com cartões brancos, marca verde-floresta
  `#12372A`, Figtree no corpo e Bricolage Grotesque em título e número grande. Sem sombra.
  Os contrastes estão calculados em comentário em `globals.css` e um teste
  (`lib/tokens.test.ts`) reprova par abaixo de 4,5 (texto) ou 3 (barra e borda) nos dois temas
  e reprova qualquer mudança na matriz de risco do ADR-0013.
- **Tags de risco em SVG**, com as quatro formas do ADR-0013 e uma prop `contagem` para o
  ranking e o resumo do perfil. O código da regra continua fora do nome acessível (decisão
  anterior, mantida e com teste).
- **Componentes base** em `componentes/base/`: `Botao` (com aviso de nova aba), `Campo`,
  `Superficie`, `TabelaDeDados`; e `CartaoDePessoa` com `Retrato` e `ContagensDeGravidade`.
- **Cabeçalho** com as seções atrás de um botão de menu de 44px no celular e as três
  ordenações num seletor segmentado; **rodapé** em três linhas.
- **Home**: um assunto por cartão, as duas casas lado a lado, os três que mais gastaram em
  cada casa, a matriz de leitura e "o que um indicador não é". Saíram o selo animado, a
  numeração 1-2-3 da metodologia e os ícones de cada cartão.
- **Ranking**: o painel virou quatro cartões (gasto, o que o motor encontrou, bancadas,
  estados) e o pódio saiu, porque a lista logo abaixo começa por ele. A tabela é uma só: abaixo
  de `md` cada linha vira cartão de pessoa pela própria classe, sem duplicar o conteúdo e sem
  rolar de lado. Zero achado não vira tag.
- **Despesa, comparar, Estado, privacidade**: título em caixa de frase (`caixaDeFrase`), os
  cartões no vocabulário novo, e a lista de órgãos do Estado agrupada por poder, recolhida
  (13.900px para 4.090px no computador).
- **Perfil**: `ResumoDoPerfil` com retrato ou iniciais, os dois números e a frase dos achados
  ("Nenhum achado nas 7 regras que puderam ser aplicadas. 5 de 12 sem dado público…").
  Ordem: resumo, composição do índice, despesas com achado, o que foi analisado (aberto,
  sempre), comparar, e quatro seções recolhidas. Mostra as 10 despesas com achado mais
  recentes em vez de 25. Altura no celular: 13.950px antes, 5.939px depois.
- **Revelação progressiva**: `LinhaDeDespesa` sobre `<details>`/`<summary>`. Fechada mostra
  tipo, data, fornecedor, valor e a tag mais grave com "+N"; aberta mostra cada achado, o
  documento, a glosa (só se conhecida e maior que zero) e a nota fiscal ou a tag do Senado.
  Enter abre, Tab vai para "Ver nota fiscal", Espaço fecha (conferido no Chrome).
- **Filtros da lista de despesas** saíram da barra lateral: um "Filtrar" nativo acima da
  lista, que abre sozinho quando há filtro aplicado.
- **CPF fora da tela.** `documentoPublicavel` mostra só CNPJ; documento de pessoa física,
  mesmo mascarado, vira "Pessoa física (documento não exibido)", na tela e no RSS.
- `DespesasComIndicadores.tsx` foi apagado: nenhum arquivo o importava, e ele imprimia o
  documento do fornecedor sem filtro.

## Medido

- 375px: largura de rolagem 375 em todas as dez rotas medidas.
- Contraste AA com `getComputedStyle` em onze rotas, claro e escuro: menor par 6,31 no claro e
  6,00 no escuro, depois de trocar dois textos que usavam o acento (3,56).
- Nenhum `uppercase`, nenhum `text-[9-11px]`, nenhuma cor crua do Tailwind (`amber-`,
  `slate-`…) em `src`.

## Build

`npm test` (344 testes) e `npm run lint` passam. `npm run build` **sem API** passa: o
`/feed.xml` quebrava com `fetch failed`, e agora responde 503 com `Retry-After` quando a API
está fora (um feed vazio ficaria cinco minutos no cache e o leitor de RSS concluiria que os
indicadores sumiram); nesse build ele sai como rota sob demanda.

**Com a API local** (`dotnet run` em Development) o build ainda não fecha: 11 workers
pré-renderizando 982 páginas estouram o limite de 60s por página (`/parlamentares/...` numa
rodada, `/estado/304` na outra). Isso não é desta branch; pede a API em Release ou menos
workers (`experimental.cpus`).

## O que ficou de fora

- Os filtros do ranking ainda ocupam quatro campos empilhados no celular.
- No computador, os filtros das despesas também abrem por clique: `<details>` não abre por
  largura de tela sem JavaScript.
- O código das regras no `aria-label` da tag, que o plano pedia, não entrou: contraria a
  decisão anterior registrada em `TagDeGravidade` e no teste.
