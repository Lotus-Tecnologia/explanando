# Contratos reais das APIs externas

Respostas **reais**, capturadas em 2026-08-18, das APIs que alimentam o V1. Servem para dois
fins: derivar os DTOs do JSON que existe (não do que a documentação promete) e virar fixture
dos testes de integração, conforme o [ADR-0005](../decisoes/ADR-0005-tdd-e-niveis-de-teste.md).

Quando um teste que usa estes arquivos quebrar contra a API ao vivo, é sinal de que o contrato
mudou — que é exatamente o risco de "Single Point of Failure governamental" levantado no
documento de Análise de Lacunas.

| Arquivo | Origem |
|---|---|
| `camara-deputados-lista.json` | `GET /api/v2/deputados?itens=3` |
| `camara-deputado-detalhe.json` | `GET /api/v2/deputados/204379` |
| `camara-despesas.json` | `GET /api/v2/deputados/204379/despesas?idLegislatura=57&itens=3` |
| `camara-despesas-pagina-100.json` | idem, `itens=100` — amostra para análise de qualidade |
| `camara-tipos-despesa.json` | `GET /api/v2/referencias/deputados/tipoDespesa` |
| `brasilapi-cnpj.json` | `GET /api/cnpj/v1/36826246000164` |
| `brasilapi-cnpj-invalido.json` | idem, com o CNPJ sentinela — resposta de erro |

---

## 1. Câmara dos Deputados

### `idLegislatura` é obrigatório no endpoint de despesas

**A descoberta mais importante da etapa.** Sem `idLegislatura`, o endpoint responde
`HTTP 200` com `"dados": []` — não um `400`. Testado com `ano`, `mes`, `pagina`, `itens` e
sem parâmetro nenhum: **sempre vazio**. Oito deputados diferentes, mesmo resultado.

```
GET .../deputados/204379/despesas                        -> 200, 0 itens
GET .../deputados/204379/despesas?ano=2026               -> 200, 0 itens
GET .../deputados/204379/despesas?ano=2026&mes=6         -> 200, 0 itens
GET .../deputados/204379/despesas?idLegislatura=57       -> 200, 15 itens
```

Um ingestor escrito a partir da documentação rodaria a coleta inteira, gravaria zero despesas
e **não acusaria erro nenhum**. Isso precisa virar teste de integração e alarme de sanidade:
coleta que retorna zero despesas para um deputado em exercício é falha, não resultado.

Legislatura corrente: **57** (2023–2027).

### Paginação

`itens` é **limitado a 100** — pedir 200, 500 ou 1000 devolve 100. A navegação sai dos links
`self` / `next` / `first` / `last` no corpo da resposta; o total de páginas se lê do `last`.

O deputado da amostra tem **12 páginas** na legislatura 57, ou seja ~1.200 despesas. Para 513
deputados, a ordem de grandeza da carga inicial é de **~6.000 requisições**.

### Desempenho e concorrência

| Cenário | Resultado |
|---|---|
| 20 requisições sequenciais, conexão nova a cada uma | ~2.619 ms/req, nenhum `429` |
| 12 requisições, concorrência 6, conexão reaproveitada | **1.950 ms no total** |

Os 2,6 s por requisição **não eram rate-limit**: eram handshake TLS por conexão. Com pool de
conexões e concorrência moderada, o mesmo trabalho cai de ~31 s para ~2 s — 16×.

Consequência para a Etapa 03: o gargalo não justifica arquitetura defensiva pesada, mas o
`HttpClient` **precisa** vir de `IHttpClientFactory` com pool de conexões, e a concorrência
deve ficar contida (4 a 6) por educação com um serviço público, não por necessidade.

### Contrato de despesa

```json
{
  "ano": 2026, "mes": 7,
  "tipoDespesa": "COMBUSTÍVEIS E LUBRIFICANTES.",
  "codDocumento": "8132888",
  "tipoDocumento": "Nota Fiscal Eletrônica", "codTipoDocumento": 4,
  "dataDocumento": "2026-07-06T00:00:00",
  "numDocumento": "8887",
  "valorDocumento": 385.92, "valorLiquido": 385.92, "valorGlosa": 0.0,
  "urlDocumento": "http://www.camara.leg.br/cota-parlamentar/...",
  "nomeFornecedor": "Auto Posto Bom Tempo Ltda",
  "cnpjCpfFornecedor": "36826246000164",
  "numRessarcimento": "", "codLote": 2253045, "parcela": 0
}
```

Diferenças em relação ao [mapeamento EF Core especificado](../especificacao/Explanando_EFCore_Mappings.md):

| Achado | Impacto no modelo |
|---|---|
| `numDocumento` **existe e vem 100% preenchido** (100/100 na amostra) | Adicionar `NumeroDocumento` — destrava `REG_KYB_005` |
| Existem **três** valores: `valorDocumento`, `valorLiquido`, `valorGlosa` | `Despesa.Valor` é insuficiente. `valorLiquido` é o que o erário pagou e é a base do score; a glosa é sinal próprio (1 em 100 na amostra) |
| `cnpjCpfFornecedor` pode ser **CPF (11 dígitos)**, não só CNPJ | `Fornecedor.Cnpj varchar(14)` não cobre pessoa física |
| **Não há campo de local da despesa** | `REG_GEO_001` fica sem base; `REG_GEO_002` terá de usar a UF do *fornecedor* |
| `ano` e `mes` vêm prontos | Facilita agregação por período de cota (`REG_TX_001`, `REG_TX_002`) |
| `codDocumento` + `codLote` + `parcela` | Boa base para o `HashUnico` de idempotência |

### Dado sujo confirmado

**2 em 100 despesas** trazem `cnpjCpfFornecedor` = `"00000000000001"` — valor sentinela da base
da Câmara. A BrasilAPI responde `400` para ele:

```json
{"message":"CNPJ 00.000.000/0000-01 inválido.","type":"bad_request","name":"BadRequestError"}
```

O ingestor precisa reconhecer o sentinela **antes** de consultar a BrasilAPI, e a despesa precisa
ser persistida mesmo sem fornecedor resolvível — descartá-la esconderia gasto público real.

### Tipos de despesa

24 categorias, com código, em `camara-tipos-despesa.json`. É o domínio fechado sobre o qual a
tabela de compatibilidade CNAE × tipo de despesa (`REG_KYB_001`) precisa ser curada — 24 linhas,
trabalho tratável.

### O detalhe do deputado expõe CPF

`GET /deputados/{id}` devolve `"cpf": "90000000175"` completo, além de `dataNascimento`,
`municipioNascimento` e `redeSocial`.

**Decisão: o CPF do parlamentar não é ingerido nem persistido.** Ele não é insumo de nenhuma
regra do V1, e armazenar dado pessoal sem finalidade viola o princípio da necessidade da LGPD
(art. 6º, III) — o mínimo necessário para a finalidade declarada. O que o V1 consome do detalhe
é `nomeCivil` e `ultimoStatus` (nome eleitoral, partido, UF, situação, foto).

### Outros endpoints úteis (fora do V1)

`GET /deputados/{id}/orgaos` responde `200` — são as comissões de que o parlamentar participa.
É exatamente o insumo que a Camada 2 (LLM) precisa para avaliar "pertinência do gasto versus a
atuação do parlamentar", conforme o documento de Visão e Escopo §3. Anotado para quando a
Camada 2 entrar.

**Não existe endpoint de secretários parlamentares.** `/servidores`, `/secretarios`,
`/deputados/{id}/secretarios` e `/deputados/{id}/servidores` respondem `405`, que é o que essa
API devolve para rota inexistente. `REG_NET_002` continua sem fonte na API REST.

---

## 2. BrasilAPI (CNPJ)

Latência ~470 ms por consulta, 8 consultas sequenciais sem sinal de bloqueio. 48 campos na raiz.

Campos que destravam regras:

| Campo | Regra |
|---|---|
| `cnae_fiscal`, `cnae_fiscal_descricao`, `cnaes_secundarios[]` | `REG_KYB_001` |
| `descricao_situacao_cadastral`, `situacao_cadastral`, `data_situacao_cadastral` | `REG_KYB_002` |
| `data_inicio_atividade` | `REG_KYB_003` |
| `opcao_pelo_mei`, `data_opcao_pelo_mei`, `porte`, `codigo_porte` | `REG_KYB_004` |
| `uf`, `municipio`, `codigo_municipio_ibge`, `logradouro`, `numero`, `cep` | `REG_GEO_002`, `REG_NET_003` |
| `qsa[]` | `REG_NET_001`, `REG_NET_002` |

### O CPF do sócio vem mascarado — e mais do que se supunha

```json
{
  "nome_socio": "FULANO DE TAL PRIMEIRO SOCIO",
  "cnpj_cpf_do_socio": "***456789**",
  "qualificacao_socio": "Sócio-Administrador",
  "faixa_etaria": "Entre 71 a 80 anos",
  "data_entrada_sociedade": "2011-07-18"
}
```

O padrão é `***XXXXXX**`: dos 11 dígitos do CPF, **apenas 6 são visíveis** (posições 4 a 9).
Os três primeiros e os dois últimos vêm ocultos.

Isso confirma e agrava o conflito já identificado nas especificações: `REG_NET_001` (doação
cruzada) exige cruzar o CPF do sócio com o CPF do doador na base do TSE, e esse cruzamento
**não pode ser determinístico**. Terá de combinar os 6 dígitos visíveis com o nome do sócio,
produzindo um match com grau de confiança.

Duas consequências que precisam virar decisão antes da Etapa 04:

1. A flag de `REG_NET_001` tem de carregar **grau de confiança** e a UI tem de dizer
   "compatível com", nunca "é". Afirmar identidade a partir de 6 dígitos seria exatamente o
   tipo de imputação que o [ADR-0004](../decisoes/ADR-0004-nomenclatura-tecnica.md) proíbe.
2. Em compensação, metade da exigência de LGPD se resolve sozinha: o dado completo **nunca
   chega** ao sistema, então não há o que vazar. A rotina de ofuscação prevista na Fase 4 do
   plano deixa de ser necessária para o QSA vindo desta fonte.

Nota: `faixa_etaria` também é dado pessoal e não é insumo de nenhuma regra — não deve ser
persistido.

---

## 3. Viabilidade das 16 regras, com base no dado real

| Regra | V1 | Fundamento |
|---|---|---|
| `REG_KYB_001` CNAE divergente | ✅ | `cnaes_secundarios` + 24 tipos de despesa. Exige curadoria da tabela |
| `REG_KYB_002` Baixa/irregularidade | ✅ | `data_situacao_cadastral` vs `dataDocumento` |
| `REG_KYB_003` Empresa recém-nascida | ✅ | `data_inicio_atividade` |
| `REG_KYB_004` MEI acima do teto | ⚠️ | Campos existem, mas vieram `null` no CNPJ da amostra (não é MEI). Validar contra um MEI real |
| `REG_KYB_005` Nota inicial | ✅ | `numDocumento`, 100% preenchido |
| `REG_NET_001` Doação cruzada | ⚠️ | Só com 6 dígitos + nome. Probabilístico, e depende do TSE (fora do V1) |
| `REG_NET_002` Vínculo de gabinete | ❌ | Sem fonte: não há endpoint de secretários |
| `REG_NET_003` Mesmo endereço | ⚠️ | Endereço do fornecedor é estruturado; falta o do escritório de apoio. Possível inferir pelo fornecedor do tipo de despesa 1 |
| `REG_NET_004` Concentração partidária | ✅ | `siglaPartido` + base completa. Regra agregada, roda ao fim da ingestão |
| `REG_TX_001` Fracionamento | ✅ | `ano`, `mes`, fornecedor, `valorLiquido` |
| `REG_TX_002` Cravado no limite | ⚠️ | Limite CEAP por UF não vem na API. Exige tabela curada de 27 valores |
| `REG_TX_003` Padrão mensal idêntico | ✅ | `valorLiquido` exato por fornecedor e mês |
| `REG_TX_004` Spike de consumo | ✅ | Série histórica por `tipoDespesa` |
| `REG_GEO_001` Teletransporte | ❌ | Não há local nem hora na despesa |
| `REG_GEO_002` Fora da base eleitoral | ✅ | UF do fornecedor vs UF do deputado |
| `REG_GEO_003` Recesso/feriado | ✅ | `dataDocumento` + calendário curado |

**9 regras prontas, 4 parciais, 2 sem fonte, 1 dependente do TSE.**

Um exemplo real já apareceu na amostra: o deputado é do **AP** e a despesa de combustível foi em
um posto de **Goiânia/GO** — candidato direto a `REG_GEO_002`.

---

## Como atualizar estes contratos

Reexecutar as chamadas listadas na tabela do topo e sobrescrever os arquivos. Se algum teste de
integração quebrar depois disso, **o contrato mudou** — investigar antes de ajustar o teste.
