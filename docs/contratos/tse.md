# TSE — a investigação da Fase 6

*Feita em 2026-09-25, dentro do prazo fechado de um dia do
[plano de execução](../plano-de-execucao.md).* O objetivo não era ingerir nada: era responder três
perguntas e dar um **sim ou não** para cada ideia que depende do TSE.

---

## Como o TSE responde

**O portal mudou desde a última medição.** Em 2026-09-24, `dadosabertos.tse.jus.br` respondia
`200` a um `curl` com o conjunto completo de cabeçalhos de navegador. Hoje, o mesmo pedido volta
**`403 Access Denied`**, com a página de erro da Akamai — e o CDN dos arquivos
(`cdn.tse.jus.br`) também. O bloqueio não é por cabeçalho: é por impressão digital do cliente.

| Caminho | Resposta |
|---|---|
| `curl` com cabeçalhos de navegador, portal e CDN | `403` (Akamai) |
| Navegador real, API CKAN do portal (`/api/3/action/...`) | `200`, JSON |
| Navegador real, API do DivulgaCandContas (`/divulga/rest/v1/...`) | `200`, JSON |
| Navegador real, `fetch` para o CDN a partir do portal | bloqueado por CORS |

**Consequência para a ingestão:** um coletor em .NET com `HttpClient`, como os outros do
projeto, vai tomar `403` do mesmo jeito que o `curl`. Os caminhos possíveis são baixar os
arquivos anuais à mão (são publicados uma vez por eleição, não mudam todo mês) ou negociar
acesso. **Não é para insistir** — mesma postura das fontes que bloqueiam por volume.

---

## Pergunta 1 — Formato e chaves das doações

**Em lote, por ano.** O catálogo lista, por eleição, um `.zip` de prestação de contas de
candidatos e outro de órgãos partidários:

| Eleição | Conjunto no catálogo | Arquivo de candidatos |
|---|---|---|
| 2022 (federal) | `dadosabertos-tse-jus-br-dataset-prestacao-de-contas-eleitorais-2022` | `prestacao_de_contas_eleitorais_candidatos_2022.zip` |
| 2024 (municipal) | `prestacao-de-contas-eleitorais-2024` | `prestacao_de_contas_eleitorais_candidatos_2024.zip` |

**O layout interno dos `.zip` não foi verificado**, e é o único ponto aberto: o CDN recusa
até o `HEAD`, então nem o tamanho se mede sem baixar. Baixar é decisão do Gustavo (ver o fim).

**Por candidato, em JSON**, a API do DivulgaCandContas devolve a prestação consolidada. Medido
num deputado federal eleito em 2022 (`/prestador/consulta/{eleicao}/{ano}/{uf}/{cargo}/{partido}/{numero}/{id}`):

- `dadosConsolidados` — totais por origem: pessoa física, partido, recursos próprios, FEFC,
  internet, e assim por diante;
- `rankingDoadores` — **uma linha por doador**, com `cpfCnpj`, `nome`, `qntd` e `valor`;
- `cnpj` da campanha, número do processo no PJe, histórico de entregas.

A chave do candidato no TSE (`idCandidato`) **não é** a nossa. A ligação candidato ↔
parlamentar sai de **UF + nome de urna + partido**, conferidos contra a lista de eleitos. **Não
pelo CPF do parlamentar:** a política de privacidade promete que ele não é coletado, mesmo a
Câmara entregando completo — e a promessa vale para o TSE também.

## Pergunta 2 — O CPF do doador vem mascarado como o nosso?

**Não — e é por isso que a chave existe.**

- **O TSE publica o CPF do doador inteiro, com os onze dígitos.** Visto no `rankingDoadores`.
- **O quadro societário da Receita**, que é de onde vêm os nossos 86.810 sócios, publica o CPF
  **mascarado em posição fixa**: `***NNNNNN**`, só os seis dígitos do meio.

Como a máscara da Receita é fixa, aplicar a mesma máscara ao CPF do TSE dá uma string
comparável: **nome + CPF mascarado na mesma posição**. É casamento literal — exatamente a
chave que o `nome_socio` + `cpf_mascarado` já usa na Fase 5 —, não aproximação.

Doação de empresa é proibida desde 2015, então todo doador é pessoa física: a ligação
**fornecedor → doador** passa obrigatoriamente pelo sócio. É o caminho
fornecedor → sócio → doador.

> **Cuidado de LGPD que vem junto.** O TSE publica o CPF inteiro; nós **não**. Na ingestão, o
> CPF é mascarado **antes de gravar**, na mesma posição da Receita, e o inteiro nunca toca o
> banco. Nenhum CPF foi copiado para este documento.

## Pergunta 3 — Votação por município

**Sim, e mais fina que município.** O conjunto `resultados-2022` traz:

| Arquivo | Granularidade |
|---|---|
| `votacao_candidato_munzona_2022.zip` | candidato × município × zona, todas as UFs, 1º e 2º turnos |
| `votacao_partido_munzona_2022.zip` | partido × município × zona |
| `votacao_secao_2022_{UF}.zip` | seção eleitoral, por UF |

**Um risco a medir na ingestão:** o TSE identifica município pelo **código dele**, que não é o
do IBGE. O endereço do fornecedor, que é o outro lado da comparação, vem com o código IBGE.
Sem uma tabela de correspondência TSE↔IBGE, a saída seria casar pelo nome do município com a
UF — normalizando caixa e acento, mas sem distância de edição.

---

## O sim ou não de cada ideia

| Ideia que depende do TSE | Resposta | Condição |
|---|---|---|
| **Timeline completa** — o degrau "torna-se doador" (Fase 7) | ✅ **Sim** | Chave nome + CPF mascarado, via sócio. Depende de baixar o lote de 2022 |
| **Quem beneficia quem** — o salto "doação para partido" | ✅ **Sim** | Mesma chave, no lote de órgãos partidários |
| **Mapa de poder expandido** — doadores como nó | ✅ **Sim** | Mesma chave. Associações, *think tanks* e ONGs seguem **sem fonte** |
| **Alerta de reduto eleitoral** (Fase 3) | 🟡 **Sim, com um risco** | A votação existe por município e zona; falta a correspondência de código de município TSE↔IBGE |

**Resultado da investigação: a Fase 7 pode começar**, com um passo antes que não é meu —
baixar os arquivos.

## O que ficou aberto — e como se fechou em 2026-09-25

1. **Os lotes foram baixados**, com autorização do Gustavo. O CDN recusa curl, PowerShell,
   Python e o `HttpClient` do .NET; **o `fetch` do Node passa**, e é por ele que
   `ferramentas/baixar-tse.mjs` baixa. Tamanhos: candidatos 367 MB, órgãos partidários 18,5 MB,
   votação por município e zona 578 MB (8,6 GB descompactada). Moram em `dados/tse/`, fora do git.
2. **O lote confirma a API:** `NR_CPF_CNPJ_DOADOR` vem com os onze dígitos em 544.538 das 674.944
   receitas de candidatos; as outras são CNPJ (partido, campanha) e marcadores de "sem doador".
3. **Município:** o cruzamento com a sede do fornecedor é por **nome normalizado + UF**, e não
   por código — o do TSE não é o do IBGE.

## A carga — `importar-tse`

| | |
|---|---|
| Parlamentares ligados à candidatura de 2022 | **510 de 513** |
| Ambíguos (homônimo na mesma UF) | 0 |
| Votos por município gravados | 152.859 |
| Receitas de pessoa física lidas | 533.254 |
| Doações a parlamentares da base | 12.845 |
| Doações de quem é sócio de fornecedor | 2.767 |
| Tempo | 1,3 minuto |

**A chave de ligação é o nome civil**, e ele não estava na base: a lista da Câmara traz o nome
parlamentar nos dois campos, e o de urna raramente é igual a ele ("GLEISI", "FRAGA",
"CAPITÃO DERRITE"). A carga busca o civil no detalhe de cada deputado na API da Câmara — 499
completados — e liga por nome civil + UF + cargo, normalizados e comparados por igualdade. Os três
que não ligaram (Emanuel Pinheiro Neto, Guilherme Uchoa, Paulo Folletto) têm nome civil diferente
no TSE, e ficam de fora em vez de ligados no palpite.

**Duas medições que mudaram o modelo:**
- `SQ_RECEITA` **não é único**: 189 receitas de pessoa física repetem o sequencial com valores
  diferentes — a mesma receita declarada em partes. O índice não é único, e a carga é
  idempotente por apagar o ano antes de gravar.
- Com `InvariantGlobalization`, `string.Normalize` não tira acento. "JOSÉ" continuava "JOSÉ", e
  o nome acentuado nunca casava. `NomeNormalizado` usa uma tabela própria.
