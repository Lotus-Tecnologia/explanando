# Especificação de Entidades e Mapeamento de Banco de Dados (EF Core) - Explanando

**Instruções para o Agente de IA (Claude Code / Copilot):**
Ao implementar este documento, utilize **Entity Framework Core** com provedor **PostgreSQL**. 
* Aplique o pacote `EFCore.NamingConventions` para converter as nomenclaturas PascalCase do C# para `snake_case` no banco de dados automaticamente.
* Utilize **Fluent API** (via `IEntityTypeConfiguration<T>`) para configurar as chaves, índices, restrições e relacionamentos. Não polua as classes de domínio com Data Annotations.
* Assuma que todas as entidades herdam de uma classe base `Entity` que contém a propriedade `public Guid Id { get; private set; }`.

---

## 1. Contexto de Parlamentares

### Entidade: `Parlamentar`
* **Tabela:** `parlamentares`
* **Propriedades e Tipagem DB:**
  * `Id` (Guid) -> PK.
  * `IdExterno` (int) -> `integer`. ID da API da Câmara/Senado.
  * `Nome` (string) -> `varchar(200)`, Not Null.
  * `NomeParlamentar` (string) -> `varchar(100)`, Not Null.
  * `FotoUrl` (string) -> `varchar(500)`.
  * `SiglaPartido` (string) -> `varchar(20)`, Not Null.
  * `Uf` (string) -> `char(2)`, Not Null.
  * `Tipo` (Enum) -> `integer`, Not Null. (1 = DeputadoFederal, 2 = Senador).
* **Navegações:**
  * `ICollection<Mandato> Mandatos`
  * `ICollection<Despesa> Despesas`
  * `ScoreRiscoParlamentar ScoreRisco`
* **Índices:**
  * Unique Index em `IdExterno` + `Tipo`.

### Entidade: `Mandato`
* **Tabela:** `mandatos`
* **Propriedades e Tipagem DB:**
  * `Id` (Guid) -> PK.
  * `ParlamentarId` (Guid) -> FK para `parlamentares(id)`, DeleteBehavior.Cascade.
  * `Legislatura` (int) -> `integer`, Not Null.
  * `DataInicio` (DateTime) -> `date`, Not Null.
  * `DataFim` (DateTime?) -> `date`, Nullable.
  * `Ativo` (bool) -> `boolean`, Default `true`.
* **Navegações:**
  * `Parlamentar Parlamentar`
* **Índices:**
  * Index em `ParlamentarId`.

---

## 2. Contexto de Gastos e Auditoria

### Entidade: `Despesa`
* **Tabela:** `despesas`
* **Propriedades e Tipagem DB:**
  * `Id` (Guid) -> PK.
  * `ParlamentarId` (Guid) -> FK para `parlamentares(id)`, Restrict.
  * `FornecedorId` (Guid) -> FK para `fornecedores(id)`, Restrict.
  * `Valor` (decimal) -> `numeric(18,2)`, Not Null.
  * `DataEmissao` (DateTime) -> `timestamp with time zone`, Not Null.
  * `TipoDespesa` (string) -> `varchar(100)`, Not Null.
  * `LinkNotaFiscal` (string) -> `varchar(500)`.
  * `HashUnico` (string) -> `varchar(64)`, Not Null. (Hash MD5/SHA256 da transação para idempotência).
* **Navegações:**
  * `Parlamentar Parlamentar`
  * `Fornecedor Fornecedor`
  * `ICollection<FlagAnomalia> FlagsAnomalia`
* **Índices:**
  * Unique Index em `HashUnico` (Garante que a mesma nota não seja inserida duas vezes).
  * Index em `ParlamentarId`.
  * Index em `FornecedorId`.
  * Index em `DataEmissao`.

### Entidade: `FlagAnomalia`
* **Tabela:** `flags_anomalias`
* **Propriedades e Tipagem DB:**
  * `Id` (Guid) -> PK.
  * `DespesaId` (Guid) -> FK para `despesas(id)`, Cascade.
  * `CodigoRegra` (string) -> `varchar(50)`, Not Null. (Ex: REG_KYB_001).
  * `Gravidade` (Enum) -> `integer`, Not Null.
  * `Descricao` (string) -> `text`, Not Null.
  * `DataIdentificacao` (DateTime) -> `timestamp with time zone`, Not Null.
* **Navegações:**
  * `Despesa Despesa`
* **Índices:**
  * Index em `DespesaId`.
  * Index em `CodigoRegra`.

### Entidade: `ScoreRiscoParlamentar`
* **Tabela:** `scores_risco`
* **Propriedades e Tipagem DB:**
  * `Id` (Guid) -> PK.
  * `ParlamentarId` (Guid) -> FK para `parlamentares(id)`, Cascade.
  * `ScoreCalculado` (decimal) -> `numeric(10,2)`, Not Null, Default 0.
  * `TotalFlagsVermelhas` (int) -> `integer`, Not Null, Default 0.
  * `DataUltimaVarredura` (DateTime) -> `timestamp with time zone`, Not Null.
* **Navegações:**
  * `Parlamentar Parlamentar`
* **Índices:**
  * Unique Index em `ParlamentarId` (Relacionamento 1:1).

---

## 3. Contexto de Fornecedores (KYB)

### Entidade: `Fornecedor`
* **Tabela:** `fornecedores`
* **Propriedades e Tipagem DB:**
  * `Id` (Guid) -> PK.
  * `Cnpj` (string) -> `varchar(14)`, Not Null. Apenas números.
  * `RazaoSocial` (string) -> `varchar(200)`, Not Null.
  * `CnaePrincipal` (string) -> `varchar(20)`.
  * `DataAbertura` (DateTime) -> `date`.
  * `Endereco` (string) -> `varchar(500)`.
  * `Ativo` (bool) -> `boolean`, Default `true`.
* **Navegações:**
  * `ICollection<Sociedade> QuadroSocietario`
  * `ICollection<Despesa> Despesas`
* **Índices:**
  * Unique Index em `Cnpj`.

### Entidade: `Sociedade`
* **Tabela:** `sociedades`
* **Propriedades e Tipagem DB:**
  * `Id` (Guid) -> PK.
  * `FornecedorId` (Guid) -> FK para `fornecedores(id)`, Cascade.
  * `NomeSocio` (string) -> `varchar(200)`, Not Null.
  * `CpfMascarado` (string) -> `varchar(14)`, Not Null.
  * `Qualificacao` (string) -> `varchar(100)`.
* **Navegações:**
  * `Fornecedor Fornecedor`
* **Índices:**
  * Index em `FornecedorId`.
  * Index em `CpfMascarado`.

### Entidade: `DoacaoCampanha`
* **Tabela:** `doacoes_campanhas`
* **Propriedades e Tipagem DB:**
  * `Id` (Guid) -> PK.
  * `ParlamentarId` (Guid) -> FK para `parlamentares(id)`, Restrict.
  * `CpfCnpjDoador` (string) -> `varchar(14)`, Not Null. (Pode ser o CNPJ do fornecedor ou CPF de um sócio).
  * `ValorDoado` (decimal) -> `numeric(18,2)`, Not Null.
  * `AnoEleicao` (int) -> `integer`, Not Null.
* **Navegações:**
  * `Parlamentar Parlamentar`
* **Índices:**
  * Index em `ParlamentarId`.
  * Index em `CpfCnpjDoador` (Importante para otimizar os joins do motor de regras).

---

## 4. Contexto de Usuários e Operacional

### Entidade: `Usuario`
* **Tabela:** `usuarios`
* **Propriedades e Tipagem DB:**
  * `Id` (Guid) -> PK.
  * `Email` (string) -> `varchar(255)`, Not Null.
  * `HashSenha` (string) -> `varchar(255)`, Not Null.
  * `DataCriacao` (DateTime) -> `timestamp with time zone`, Default `now()`.
  * `Ativo` (bool) -> `boolean`, Default `true`.
* **Navegações:**
  * `ICollection<Favorito> Favoritos`
* **Índices:**
  * Unique Index em `Email`.

### Entidade: `Favorito`
* **Tabela:** `favoritos`
* **Propriedades e Tipagem DB:**
  * `Id` (Guid) -> PK.
  * `UsuarioId` (Guid) -> FK para `usuarios(id)`, Cascade.
  * `ParlamentarId` (Guid) -> FK para `parlamentares(id)`, Cascade.
  * `DataCriacao` (DateTime) -> `timestamp with time zone`, Default `now()`.
* **Navegações:**
  * `Usuario Usuario`
  * `Parlamentar Parlamentar`
* **Índices:**
  * Unique Constraint composta: `(UsuarioId, ParlamentarId)` (Para evitar que o usuário favorite o mesmo deputado duas vezes).

### Entidade: `SolicitacaoVarredura`
* **Tabela:** `solicitacoes_varreduras`
* **Propriedades e Tipagem DB:**
  * `Id` (Guid) -> PK.
  * `ParlamentarId` (Guid) -> FK para `parlamentares(id)`, Restrict.
  * `UsuarioId` (Guid) -> FK para `usuarios(id)`, Restrict.
  * `DataSolicitacao` (DateTime) -> `timestamp with time zone`, Not Null.
  * `Status` (Enum) -> `integer`, Not Null.
  * `PaymentId` (string) -> `varchar(100)`. (ID do gateway, ex: Mercado Pago).
  * `ValorPago` (decimal) -> `numeric(10,2)`.
* **Navegações:**
  * `Parlamentar Parlamentar`
  * `Usuario Usuario`
* **Índices:**
  * Index em `ParlamentarId`.
  * Index em `Status` (Para otimizar consultas de filas travadas).
  * Unique Index em `PaymentId` (Garante unicidade do pagamento).
