# Etapa 01 — Spike de contratos das APIs

**Data:** 2026-08-18 · **Situação:** ✅ Concluída

---

## 1. Objetivo

Descobrir o que as APIs da Câmara e da BrasilAPI **realmente devolvem**, antes de modelar o
domínio na Etapa 02.

A motivação era concreta: a leitura das especificações mostrou que o motor de regras pede
campos que o mapeamento EF Core não modela — número da nota fiscal, UF do gasto, CNAEs
secundários, situação cadastral. Modelar o banco em cima de suposição e descobrir na Etapa 03
que o campo não existe custaria uma migração e retrabalho no motor.

Esta etapa **não escreve código de produção**. Ela produz fatos.

## 2. Definido

- Os contratos capturados ficam versionados em `docs/contratos/` e viram fixture dos testes de
  integração. Quando um teste contra a API ao vivo divergir da fixture, o contrato mudou —
  que é o alarme para o risco de "Single Point of Failure governamental" da Análise de Lacunas.
- **O CPF do parlamentar não será ingerido nem persistido.** A API o entrega completo no
  endpoint de detalhe, mas ele não é insumo de nenhuma regra do V1, e guardar dado pessoal sem
  finalidade viola o princípio da necessidade da LGPD (art. 6º, III).
- **`faixa_etaria` do sócio também não será persistida**, pelo mesmo motivo.

## 3. Executado

Sete contratos reais capturados e validados como UTF-8, em `docs/contratos/`:

| Arquivo | Bytes |
|---|---|
| `camara-deputados-lista.json` | 1.531 |
| `camara-deputado-detalhe.json` | 1.110 |
| `camara-despesas.json` | 2.164 |
| `camara-despesas-pagina-100.json` | 52.656 |
| `camara-tipos-despesa.json` | 2.353 |
| `brasilapi-cnpj.json` | 3.578 |
| `brasilapi-cnpj-invalido.json` | 94 |

A análise campo a campo, com a tabela de viabilidade das 16 regras, está em
[`docs/contratos/README.md`](../contratos/README.md).

## 4. Testes

**Nenhum teste automatizado nesta etapa** — ela é exploratória por natureza, e seu produto são
fatos e fixtures, não comportamento.

Os artefatos aqui produzidos são, porém, o insumo dos testes das etapas seguintes: as fixtures
alimentam os testes de integração da Etapa 03, e a tabela de viabilidade define exatamente
quais regras a Etapa 04 precisa cobrir com teste unitário.

Duas verificações do spike precisam **virar teste** na Etapa 03, e ficam anotadas aqui para não
se perderem:

1. Coleta que retorna zero despesas para deputado em exercício é **falha**, não resultado
   (ver descoberta 5.1).
2. Despesa com CNPJ sentinela `00000000000001` tem de ser persistida mesmo sem fornecedor
   resolvível (ver descoberta 5.4).

## 5. Descobertas

### 5.1 `idLegislatura` é obrigatório — e a falha é silenciosa

A descoberta mais importante da etapa. Sem `idLegislatura`, o endpoint de despesas responde
`HTTP 200` com lista vazia. Não é `400`, não é erro, não é aviso.

```
GET .../despesas                       -> 200, 0 itens
GET .../despesas?ano=2026              -> 200, 0 itens
GET .../despesas?ano=2026&mes=6        -> 200, 0 itens
GET .../despesas?idLegislatura=57      -> 200, 15 itens
```

Confirmado em 8 deputados distintos e em 3 anos diferentes antes de encontrar a causa.

Um ingestor escrito a partir da documentação rodaria a coleta inteira dos 513 deputados,
gravaria zero despesas e reportaria sucesso. **É a falha mais cara possível: silenciosa e
plausível.**

### 5.2 Os 2,6 s por requisição não eram rate-limit

> [!warning] Corrigido depois
> A conclusão de que a BrasilAPI não tinha rate-limit **estava errada**. Ela se apoiava em
> oito requisições — poucas demais para revelar um limite medido por janela de tempo. A
> primeira coleta real, de dois deputados, produziu 166 respostas `429`. Ver
> [etapa-04b-calibragem-real.md](etapa-04b-calibragem-real.md), seção 3.1.

Medição inicial: 20 requisições sequenciais, ~2.619 ms cada, nenhum `429`. A leitura óbvia
seria "a API é lenta, a ingestão vai levar horas".

Errado. Refeito o teste com 12 requisições em concorrência 6 e reaproveitamento de conexão:
**1.950 ms no total**, contra ~31.000 ms sequenciais. Fator de 16×.

O custo era handshake TLS por conexão, artefato do laço com `curl`, não do servidor. Se essa
medição tivesse sido aceita sem checar, a Etapa 03 teria nascido com arquitetura defensiva
desnecessária.

Consequência real: usar `IHttpClientFactory` com pool de conexões, e limitar concorrência a
4–6 **por educação com um serviço público**, não por necessidade técnica.

### 5.3 O modelo de dados especificado não cobre o contrato real

| Achado | Efeito no modelo |
|---|---|
| `numDocumento` existe, 100% preenchido (100/100) | Destrava `REG_KYB_005` |
| Três valores por despesa: `valorDocumento`, `valorLiquido`, `valorGlosa` | `Despesa.Valor` é insuficiente; glosa é sinal próprio (1 em 100) |
| `cnpjCpfFornecedor` pode ser CPF de 11 dígitos | `Fornecedor.Cnpj varchar(14)` não cobre pessoa física |
| Não há campo de local da despesa | `REG_GEO_001` sem base; `REG_GEO_002` terá de usar UF do fornecedor |
| `itens` limitado a 100 | ~12 páginas por deputado, ~6.000 requisições na carga inicial |

### 5.4 Dado sujo real: o CNPJ sentinela

2 em 100 despesas trazem `cnpjCpfFornecedor` = `"00000000000001"`. A BrasilAPI devolve `400`.

A despesa precisa ser persistida mesmo assim — descartá-la esconderia gasto público real, que
é o oposto do propósito do produto. E o sentinela precisa ser reconhecido **antes** da consulta
externa, para não gastar requisição nem poluir o log de erro.

### 5.5 O CPF do sócio é mais mascarado do que se supunha

O padrão é `***XXXXXX**`: dos 11 dígitos, **só 6 são visíveis**.

Isso agrava o conflito já identificado na leitura das especificações. `REG_NET_001` (doação
cruzada) não pode ser determinística: terá de combinar 6 dígitos com o nome do sócio e produzir
um match com **grau de confiança**. A UI terá de dizer "compatível com", nunca "é" — afirmar
identidade a partir de 6 dígitos seria a imputação que o
[ADR-0004](../decisoes/ADR-0004-nomenclatura-tecnica.md) existe para impedir.

O lado bom: o dado completo nunca entra no sistema. A rotina de ofuscação de CPF do QSA prevista
na Fase 4 do plano **deixa de ser necessária** para esta fonte.

### 5.6 A API entrega mais dado pessoal do que o produto precisa

`GET /deputados/{id}` devolve `cpf` completo, `dataNascimento`, `municipioNascimento` e
`redeSocial`. Nada disso alimenta regra do V1. Decidido não ingerir (ver seção 2).

### 5.7 Não há fonte para `REG_NET_002`

`/servidores`, `/secretarios`, `/deputados/{id}/secretarios` e `/deputados/{id}/servidores`
respondem `405` — o que essa API devolve para rota inexistente. A regra de vínculo com
secretário de gabinete segue sem fonte na API REST.

Em compensação, `/deputados/{id}/orgaos` responde `200` e entrega as comissões do parlamentar —
insumo exato da Camada 2 (LLM) para avaliar pertinência do gasto. Anotado para quando ela entrar.

### 5.8 Viabilidade consolidada das 16 regras

**9 prontas** · **4 parciais** · **2 sem fonte** · **1 dependente do TSE**

Detalhamento na [tabela de viabilidade](../contratos/README.md#3-viabilidade-das-16-regras-com-base-no-dado-real).

Um caso real de `REG_GEO_002` já apareceu na amostra de 100 despesas: deputado eleito pelo
**AP** com abastecimento em posto de **Goiânia/GO**.

## 6. Pendências

| Pendência | Impacto | Quando resolver |
|---|---|---|
| `REG_KYB_004` não validada: `opcao_pelo_mei` veio `null` no CNPJ da amostra | Médio | Consultar um CNPJ que seja MEI de fato, na Etapa 04 |
| Tabela CNAE × tipo de despesa (24 linhas) precisa de curadoria humana | Alto para `REG_KYB_001` | Etapa 04 — **precisa de decisão sua** |
| Tabela de limite CEAP por UF (27 valores) não vem em API | Alto para `REG_TX_002` | Etapa 04 — **precisa de decisão sua** |
| Calendário de recesso legislativo | Médio para `REG_GEO_003` | Etapa 04 |
| ~~Nada verificado sobre o Senado~~ | — | ✅ Resolvida em 2026-08-21: [etapa-01b](etapa-01b-contratos-senado.md) e [contratos/senado.md](../contratos/senado.md) |
| Nada verificado sobre o TSE | **Alto** — `REG_NET_001` depende dele | Spike próprio. A carga do TSE é volumosa e conflita com a premissa de 0,5 GB do [ADR-0010](../decisoes/ADR-0010-resolucoes-produto-infraestrutura.md) |

## 7. Próxima etapa

**Etapa 02 — Domínio e EF Core.**

Agora com fato no lugar de suposição. O modelo precisa incorporar o que a Etapa 01 descobriu:

- `Despesa` ganha `NumeroDocumento`, `ValorDocumento`, `ValorLiquido`, `ValorGlosa`,
  `Ano`, `Mes`, `CodDocumento`, `CodLote`, `Parcela`.
- `Fornecedor` deixa de ser só CNPJ: precisa acomodar pessoa física e o sentinela.
- `Fornecedor` ganha `CnaesSecundarios`, `SituacaoCadastral`, `DataSituacaoCadastral`,
  `DataInicioAtividade`, `OptanteMei`, `Porte`, e endereço estruturado com UF e município.
- `Sociedade.CpfMascarado` documenta explicitamente que carrega 6 dígitos, não 11.
- Entra `LimiteCota`, exigida por `REG_TX_002` e ausente da especificação original.

A etapa começa pelos testes: entidades e invariantes de domínio primeiro (unitário), depois
mapeamentos contra Postgres real via Testcontainers (integração), conforme o
[ADR-0005](../decisoes/ADR-0005-tdd-e-niveis-de-teste.md).

**Pré-requisito:** Docker Desktop precisa estar rodando — pendência herdada da Etapa 00.
