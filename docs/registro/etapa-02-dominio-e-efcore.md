# Etapa 02 — Domínio e EF Core

**Data:** 2026-08-19 · **Situação:** ✅ Concluída

---

## 1. Objetivo

Modelar o domínio do V1 e persisti-lo em PostgreSQL, agora com o contrato real das APIs em
mãos em vez da especificação original — que previa um único campo `Valor` na despesa, não
previa o número da nota fiscal e não modelava CNAEs secundários, situação cadastral nem
limite de cota.

Tudo por TDD, conforme o [ADR-0005](../decisoes/ADR-0005-tdd-e-niveis-de-teste.md).

## 2. Definido

### O ADR-0004 virou invariante de domínio, não recomendação

`VocabularioDeCompliance` conhece os termos proibidos pelo documento de Negócios §1
("fraude", "crime", "corrupção", "lavagem", "desvio", "propina", "caixa dois" e variações)
e `FlagAnomalia.Criar` **recusa** emitir um achado cuja descrição os contenha.

A regra escrita só em documento erode: bastava alguém escrever "indício de fraude" numa
descrição de regra seis meses depois para que a blindagem jurídica se perdesse naquele texto.
Agora o motor não consegue produzir isso.

A comparação é por **palavra inteira**, não substring — casar cego reprovaria "descrição"
(contém "cri") e "discriminado" (contém "crim"), e a regra seria abandonada por inutilizável.

### Objetos de valor onde a especificação usava string

| Tipo | Por quê |
|---|---|
| `CpfCnpj` | O campo `cnpjCpfFornecedor` carrega CNPJ, CPF **e** o sentinela. Tratá-lo como "o CNPJ" espalharia a decisão por cada ponto de uso. Valida dígito verificador e sabe se vale consultar a BrasilAPI |
| `CpfParcial` | Torna impossível confundir os 6 dígitos que a fonte revela com um CPF completo. Expõe `EhCompativelCom` — nunca "é igual a" |
| `Competencia` | O par ano/mês é a unidade de agregação do CEAP e de três regras `REG_TX_*` |

### `Competencia` é persistida como inteiro `AAAAMM`

A codificação preserva a ordem cronológica, então comparação, ordenação e intervalo continuam
funcionando no SQL com um índice só — que é o que `REG_TX_001` (fracionamento no mês) e
`REG_TX_004` (spike contra a série) precisam. Coberto por teste de integração que grava
dezembro/2025, janeiro/2026 e outubro/2026 e confere a ordem vinda do banco.

### `FlagAnomalia` carrega grau de confiança

Consequência direta da Etapa 01: `REG_NET_001` cruza 6 dígitos de CPF e produz indício, não
prova. O `Peso` da flag no score é `gravidade × confiança` — um indício a 50% não pesa como
um fato. Sem isso, a incerteza morreria no motor e a UI afirmaria o que não pode afirmar.

### Dado pessoal deliberadamente não modelado

Nem `Parlamentar` nem `Sociedade` têm campo para o que a API entrega e o produto não usa:
CPF completo do parlamentar, data e município de nascimento, redes sociais, e a `faixa_etaria`
do sócio. Princípio da necessidade, LGPD art. 6º, III.

### `LimiteCota`, ausente da especificação

Entidade nova, sem a qual `REG_TX_002` não tem contra o que comparar. Modelada **com vigência**
porque o teto é reajustado: despesa de 2023 tem de ser confrontada com o limite de 2023.

## 3. Executado

### Domínio (`Explanando.Domain`) — zero pacotes, zero projetos

```
Comum/        Entidade · CpfCnpj · CpfParcial · Competencia · VocabularioDeCompliance
Parlamentares/ Parlamentar · Mandato · TipoParlamentar
Fornecedores/  Fornecedor · Sociedade · SituacaoCadastral · EnderecoFornecedor
Despesas/      Despesa · NovaDespesa
Auditoria/     FlagAnomalia · Gravidade · ScoreRiscoParlamentar
Cotas/         LimiteCota
```

Os métodos de consulta do agregado `Fornecedor` — `PossuiCnae`, `EstavaIrregularEm`,
`DiasDeAtividadeAte`, `EhMei` — são as perguntas que o motor fará na Etapa 04. Ficam no
domínio, testáveis sem banco e sem HTTP; a Etapa 04 apenas as encadeia.

`Entidade` usa **UUID v7**: ordenável por tempo de criação, o que mantém a inserção em índice
B-tree sequencial em vez de espalhada — importa numa tabela que vai receber centenas de
milhares de despesas.

### Persistência (`Explanando.Infrastructure`)

`ExplanandoDbContext` + `IEntityTypeConfiguration` por agregado. As entidades não têm Data
Annotations e não sabem que o EF Core existe. `EFCore.NamingConventions` converte para
snake_case.

Migration `InicialV1` gerada e aplicada. Destaques do esquema:

- `fornecedores.cnaes_secundarios` → `text[]` nativo do Postgres.
- `despesas.hash_unico` → `char(64)` com índice único: é a garantia de idempotência.
- `despesas.fornecedor_id` → **nullable**, para o caso do sentinela.
- `flags_anomalias` → único em `(despesa_id, codigo_regra)`: a mesma regra não marca a mesma
  despesa duas vezes numa reexecução.
- `scores_risco.score_calculado` → índice **descendente**, porque o ranking público ordena
  por ele a cada visita.
- `ix_despesas_parlamentar_competencia_fornecedor` → o índice que `REG_TX_001` e `REG_TX_003`
  vão varrer.

`FabricaDeDesignTime` permite gerar migration sem a API configurada e sem banco no ar.

### Infraestrutura local

`docker compose up -d` executado pela primeira vez. Postgres, Redis e LocalStack subiram
**saudáveis**, e o script de init criou `explanando-varreduras` e `explanando-varreduras-dlq`
com a `RedrivePolicy` de `maxReceiveCount: 5`. Pendência da Etapa 00 resolvida.

Banco confirmado com collation `C` e encoding `UTF8`.

### Repositório remoto

Criado **privado** em `github.com/GustavoCriado01/explanando` e sincronizado. A outra
pendência da Etapa 00 fica resolvida.

## 4. Testes

**156 testes, todos verdes. Build com 0 avisos.**

| Nível | Projeto | Testes | O que prova |
|---|---|---|---|
| Unitário | `Domain.Tests` | 132 | Regras de negócio, isoladas de banco e rede |
| Integração | `Integration.Tests` | 14 | Mapeamento contra **Postgres real** (Testcontainers) |
| Arquitetura | `Architecture.Tests` | 10 | Direção das dependências |

Os testes de integração rodam contra `postgres:17-alpine` em contêiner, com as migrations
aplicadas — **não** contra provedor em memória. O que precisa de prova ali é justamente o que
o InMemory não tem: índice único que rejeita duplicata, tipo `text[]`, `numeric(18,2)`,
conversão de objeto de valor e a própria migration. Um teste contra InMemory passaria com o
mapeamento errado.

Cobertura das descobertas da Etapa 01, cada uma virada em teste:

- Despesa com CNPJ sentinela grava com `fornecedor_id` nulo e mantém o valor.
- `numDocumento` sobrevive ao round-trip (destrava `REG_KYB_005`).
- Os três valores (documento, líquido, glosa) são guardados separadamente, e glosa
  inconsistente é rejeitada.
- `opcao_pelo_mei` nulo continua nulo depois do banco — não colapsa em `false`.
- CPF parcial preserva os 6 dígitos e a compatibilidade continua funcionando após leitura.
- Índice único do hash rejeita a mesma nota duas vezes, com `SqlState 23505`.

## 5. Descobertas

### 5.1 `InvariantGlobalization=true` desliga a normalização Unicode em silêncio

O teste de vocabulário reprovou em "Suspeita de corrupção". A causa: a solution roda com
`InvariantGlobalization=true`, e nesse modo `string.Normalize(NormalizationForm.FormD)` é um
**no-op** — "corrupção" continua com 9 caracteres depois da decomposição, e a remoção de
diacríticos nunca acontece. Não lança exceção: apenas devolve a string intacta.

Verificado isoladamente num projeto de sonda antes de concluir.

Só os termos ASCII eram pegos ("fraude", "crime", "lavagem"), então a regra parecia funcionar.
Um teste com acento foi o que revelou.

Substituído por tabela explícita de dobra de acentos — que é o comportamento correto para um
motor de regras de qualquer forma: idêntica em qualquer máquina, sem depender da versão do
ICU instalada.

### 5.2 Value converter impede navegar para dentro do objeto de valor numa query

```
DbSet<Despesa>().Where(d => d.DocumentoFornecedor.Numero == "00000000000001")
  -> InvalidOperationException: could not be translated
```

Com `ValueConverter`, o EF Core mapeia `CpfCnpj` para **uma coluna** e não sabe navegar para
`.Numero`. A comparação tem de ser pelo objeto inteiro:

```csharp
var sentinela = CpfCnpj.Criar("00000000000001");
... .Where(d => d.DocumentoFornecedor == sentinela)
```

**Restrição real para as Etapas 03 e 04:** toda consulta que filtra por documento de fornecedor
precisa construir o objeto de valor antes, não acessar propriedade dele dentro do `Where`.

### 5.3 Os analisadores reprovam o código que o próprio EF Core gera

A migration `InicialV1` dispara CA1861 e CA1825. Migrations são artefato gerado e não devem ser
editadas à mão — a próxima geração desfaria o ajuste. Resolvido marcando
`[src/**/Migrations/*.cs]` como `generated_code = true` no `.editorconfig`, com escopo de
caminho. Nada foi afrouxado no código escrito à mão.

### 5.4 O domínio reprovou os dados do próprio teste

Os CNPJs que eu inventei para os testes de integração (`"11111...0000123"`) foram recusados por
`Fornecedor.Criar` — não passam no dígito verificador. Correto: a entidade não deve aceitar
documento inválido nem vindo de teste. O teste passou a **calcular** os dígitos como a Receita
calcula.

Vale como sinal de que a validação está no lugar certo.

### 5.5 `__EFMigrationsHistory` não segue a convenção de nomenclatura

O teste que exige snake_case em todas as tabelas pegou a tabela de bookkeeping do próprio EF
Core, cujo nome é fixo no provedor. A asserção passou a excluir tabelas com prefixo `__` — a
regra é sobre as nossas tabelas.

### 5.6 O `dotnet new tool-manifest` gravou fora do lugar convencional

Criou `backend/dotnet-tools.json` em vez de `backend/.config/dotnet-tools.json`. Movido, para
que `dotnet tool restore` funcione no CI.

## 6. Pendências

| Pendência | Impacto | Quando resolver |
|---|---|---|
| `limites_cota` existe mas está **vazia** — os 27 valores precisam de curadoria | Alto para `REG_TX_002` | Etapa 04 — **precisa de decisão sua** |
| Tabela CNAE × tipo de despesa (24 linhas) não existe | Alto para `REG_KYB_001` | Etapa 04 — **precisa de decisão sua** |
| Calendário de recesso legislativo | Médio para `REG_GEO_003` | Etapa 04 |
| ~~`HistoricoScore` não modelado~~ | — | ✅ Resolvida em 2026-08-20, com a versão do motor junto para o degrau do gráfico não confundir mudança do parlamentar com mudança do software |
| CI (GitHub Actions) rodando build + testes | Médio | Próximo commit — o remote já existe |
| `Application.Tests` e `E2E.Tests` ainda vazios | Nenhum | Etapas 03 e 05 |
| Ferramentas do EF (10.0.3) atrás do runtime (10.0.4) | Baixo | Quando incomodar |

## 7. Próxima etapa

**Etapa 03 — Ingestão (Câmara + BrasilAPI).**

Clientes HTTP com Polly, respeitando o que a Etapa 01 mediu: `IHttpClientFactory` com pool de
conexões, `idLegislatura` sempre presente, paginação de 100 em 100, concorrência contida em
4–6. Checkpoint por deputado para que uma queda no meio dos 513 não obrigue a recomeçar.

Dois alarmes de sanidade que a Etapa 01 deixou anotados viram teste aqui:

1. Coleta que devolve zero despesas para deputado em exercício é **falha**, não resultado.
2. Despesa com CNPJ sentinela é persistida mesmo sem fornecedor resolvível.

As fixtures de `docs/contratos/` alimentam os testes de integração dos clientes, sem depender
de a API do governo estar no ar durante o teste.
