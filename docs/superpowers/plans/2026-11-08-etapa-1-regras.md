# Etapa 1 — Regras, valores e bugs do motor: plano de implementação

> **Para quem executa:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development (recomendado) ou superpowers:executing-plans. Passos em checkbox (`- [ ]`).
> **Nada aqui começa antes do aval do Gustavo** (ponto de aprovação 2 do plano mestre). Cada tarefa diz qual achado resolve; achado recusado = tarefa removida.

**Objetivo:** corrigir o que o levantamento de 08/11/2026 mostrou errado no motor e nas leituras, com teste que falha antes, motor na versão 7 e a base re-varrida com antes/depois.

**Arquitetura:** mudanças pequenas nas regras (`backend/src/Explanando.Domain/Regras/`), na validação da despesa (`Despesas/Despesa.cs`), na tabela CNAE e numa consulta. Nenhum campo de API muda de nome ou de tipo; muda o valor de `fornecedoresDistintos` (T8) e as contagens de flags.

**Tech stack:** .NET 10, xUnit, EF Core, Postgres 17 local (`docker compose up -d postgres`, volume `explanando_postgres-dados`).

**Spec:** `docs/especificacao/Explanando_Motor_Regras_V1.md`, ADR-0004, ADR-0007, ADR-0009, ADR-0012; plano mestre `2026-11-07-ultima-versao.md` §Etapa 1.

## Global Constraints

- TDD (ADR-0005): teste vermelho antes de cada mudança de comportamento.
- Na dúvida, silêncio (ADR-0007): toda correção que muda contagem deve reduzir falso positivo, nunca inventar achado.
- Vocabulário técnico (ADR-0004): nenhuma descrição imputa conduta.
- Dois números (ADR-0009): volume não depende de flag; índice é peso/despesas×100.
- Nunca expor CPF, nem em log, teste, registro ou mensagem de commit.
- Mudança que altera resultado sobe `MotorDeRegras.VersaoAtual` (6 → 7) uma vez só, com a lista do que mudou no XML doc.
- Sem push, sem deploy, nada da Oracle/Cloudflare. Não tocar em `frontend/`.

---

## Tabela de achados

Números medidos na base local (790.303 despesas, 631 parlamentares, 23.992 flags, motor 6).

| # | Regra / área | Problema | Evidência | Gravidade | Correção proposta |
|---|---|---|---|---|---|
| A1 | Ingestão (glosa) | A API da Câmara serializa valores com **6 dígitos significativos**: acima de R$ 10 mil o centavo some de `valorDocumento`/`valorLiquido`, mas a glosa vem com 2 casas. A conferência `doc − líq = glosa` com tolerância de R$ 0,01 rejeita a despesa inteira | 146 rejeitadas em 78 deputados (não 62). No 141398 as 19 rejeitadas são todas assim (diferenças de −0,05 a +0,09, valores de ~R$ 11 mil). Na base: 14.651 despesas ≥ R$ 10 mil da Câmara, **zero** com dígito de centavo; no Senado 89 de 1.868 têm | **Alta** — volume (o número público) subcontado; ordem de R$ 1–2 mi, a medir | Tolerância pela precisão do valor; glosa incoerente além disso → aceitar a despesa com `valor_glosa` nulo (glosa desconhecida, ADR-0012), nunca rejeitar. Re-coletar os 78 deputados |
| A1b | Ingestão (glosa) | Há 1 caso de origem realmente incoerente (doc 16.291, líq 7.028,39, glosa 11) | deputado 122158, 12/2025 | Baixa | Coberto por A1 (entra com glosa nula) |
| A2 | REG_TX_003 | Afirma "sem variação de centavos" sobre valores em que a fonte não publica centavo | 961 das 5.973 flags da Câmara têm valor ≥ R$ 10 mil | **Média** — afirmação falsa sobre pessoa nomeada | Abster acima do limite de precisão da Câmara e declarar na cobertura (dado exigido novo). Alternativa: baixar o CSV anual da Câmara, se ele tiver precisão (a conferir; ~4 arquivos) |
| A3 | REG_TX_002 | **A regra nunca rodou.** `SementeDeLimitesCota.AplicarAsync` não tem chamador; `limites_cota` tem 0 linhas. E a premissa está errada: semeando, **934 de 3.700** meses de 2026 ficam *acima* do "teto mensal" — o saldo da CEAP acumula no exercício, então o valor mensal não é teto do mês | `grep` sem chamador; simulação SQL: 967 flags em 392 deputados se semeado | **Média** — regra anunciada que não existe; semear daria 967 falsos positivos | Não semear. Corrigir os comentários que dizem "2026 coberto / 17%" e registrar. Redesenho (acumulado anual) fica fora desta etapa salvo pedido |
| A4 | REG_GEO_002 | "MANUTEN" e "LOCA" tratam como presencial o que não é: manutenção de escritório (software, móveis, administradora de imóveis) e a rubrica de material de consumo do Senado ("locação de software"); aeronave fretada idem. Contraria o próprio comentário da regra | tipo 4: 851 flags; tipo 22: 70; tipo 12: 22. Ex.: administradora de imóveis de Curitiba para escritório no RJ, 41 flags | **Média** | Marcadores presenciais: combustível, alimentação, hospedagem, locomoção, táxi, **veículo**, **embarcação**. Sai manutenção, material de consumo e aeronave. −943 flags |
| A5 | REG_KYB_001 | Combustível na Câmara aceita só 473/468/192: troca de óleo em oficina (4520), lubrificante em autopeças (4530) e posto registrado como representante de combustível (4612) viram "outra atividade". O Senado já aceita a divisão 45 na rubrica dele | ~792 flags em combustível com 4520/4530/4612 (principal ou secundário) | Média | Acrescentar `4520`, `4530`, `4612` em `combustiveis e lubrificantes` |
| A6 | REG_KYB_002 | A descrição abre com "Inconsistência com a regra **da Câmara**" também para senador | 54 flags no Senado | Baixa | Texto neutro, sem casa: "A nota de dd/MM/aaaa foi emitida por …" |
| A7 | Índice (KYB_001, KYB_002, GEO_002) | Fato do **fornecedor** repetido por despesa: um par deputado–posto gera 342 flags de KYB_001; KYB_002 chega a 176 num par; GEO_002 a 82. O índice mede quantas vezes se abasteceu, o mesmo defeito que o REG_NET_001 evita de propósito | KYB_001: 6.854 flags em 2.008 pares; KYB_002: 735 em 172; GEO_002: 3.327 em 712 | **Alta** para o índice (ADR-0009) | **Decisão sua.** Recomendo: uma flag por (fornecedor, tipo) no KYB_001 e por fornecedor no KYB_002/GEO_002, na despesa de maior valor, com quantidade e total na descrição. Custo: a lista de despesas deixa de marcar cada nota |
| A8 | REG_KYB_004 | Usa a opção pelo MEI **de hoje** para pagamentos de 2023–2026. A BrasilAPI publica `data_opcao_pelo_mei` e `data_exclusao_do_mei`, e o cliente descarta | 14 flags (gravidade alta); 1.411 fornecedores MEI | Média | Guardar as duas datas; a regra só soma o ano em que o fornecedor era MEI; sem data → silêncio. Exige reconsultar os 1.411 MEI na BrasilAPI (**pedir aval**) |
| A9 | REG_KYB_001 | Postos com CNAE de aluguel de imóveis (6810202) na Receita: "Posto da Torre" sozinho tem 942 flags | 1.498 flags, 39 fornecedores | — | **Decisão sua.** O fato é verdadeiro (é o que a Receita registra). Recomendo manter e deixar A7 tirar a repetição |
| A10 | API perfil | `fornecedoresDistintos` conta o documento vazio das passagens como um fornecedor | +1 em 500 dos 631 perfis | Baixa | Filtrar documento vazio na consulta |
| A11 | Base | 1 despesa do Senado com `fornecedor_id` nulo, mas fornecedor cadastrado pelo documento; o score usa o documento e diverge do SQL por 1 | Irajá, 09/2025 | Baixa | Achar o caminho que não liga e religar |
| A12 | Card (frontend) | O card mostra índice para 92 deputados abaixo do piso de 50% de cobertura (um com 0%), sem a cobertura ao lado — o ranking por índice os exclui | `lib/cartao.ts` usa só `amostraSuficiente` | Média | **Para a sessão do frontend** (não mexo em `frontend/`) |
| A13 | Testes | Bordas exatas sem teste: recesso 17/18 jul, 22/23 dez, 1º/2 fev; 89/90 dias (KYB_003); R$ 81.000,00 exato (KYB_004); 4 notas/R$ 5.000 (TX_001); 3× exato (TX_004) | leitura dos testes | Baixa | Testes de caracterização (sem mudar comportamento) |
| A14 | Registro | Etapa 16 diz 23.977 flags; a base tem 23.992 — a diferença são as 15 do REG_NET_004 (passada de rede) | SQL | Baixa | Corrigir no registro novo |

**O que conferiu certo** (SQL independente × API, 631 perfis e 6 rankings): volume, despesas avaliadas, flags por gravidade, índice, cobertura de fornecedor, piso amostral e de cobertura, composição por regra, flags por regra na cobertura, série mensal (total, quantidade, glosa e nulidade da glosa), ranking × perfil, ordem dos rankings, nenhum parlamentar sem score. O card lê os mesmos campos do perfil.

**Não implementado por desenho (sem mudança proposta):** REG_KYB_005/NET_002/NET_003/GEO_001 da especificação (GEO_001 e NET_002/003 sem fonte); GEO_003 não olha feriado nem fim de semana (permissivo); NET_004 usa o partido atual.

---

## Review Focus

- Despesa da Câmara de R$ 100 mil ou mais: a precisão cai para o real; a tolerância de A1 e o limite de A2 têm de crescer com a ordem de grandeza, não ser fixos em R$ 10 mil.
- Estorno com glosa (valores negativos): continua fora da conferência de glosa e não pode ser rejeitado por A1.
- Senado: valores com centavo; A2 não pode abster despesa do Senado.
- Rubrica do Senado "Locomoção, hospedagem, alimentação, combustíveis" continua presencial em GEO_002 depois de A4.
- A7 com estornos: a flag única não pode cair numa despesa negativa.

---

## Tarefas

### Task 1 (A1): glosa conferida pela precisão da origem

**Files:** Modify `backend/src/Explanando.Domain/Despesas/Despesa.cs` (bloco da glosa, ~l.200); Test `backend/tests/Explanando.Domain.Tests/Despesas/DespesaTests.cs`.

**Interfaces:** Produces `internal static decimal PrecisaoDaOrigem(decimal valor)` em `Despesa`: 0,01 abaixo de 10.000; 0,1 até 100.000; 1 até 1.000.000 (seis dígitos significativos).

- [ ] Teste `Glosa_dentro_da_precisao_da_origem_e_aceita`: doc 10.698,1, líq 10.463,5, glosa 234,62 → cria, `ValorGlosa == 234.62m`.
- [ ] Teste `Glosa_incoerente_alem_da_precisao_entra_como_desconhecida`: doc 16.291, líq 7.028,39, glosa 11 → cria, `ValorGlosa is null`, `GlosaConhecida == false`.
- [ ] Teste `Glosa_negativa_continua_rejeitada` (comportamento atual preservado).
- [ ] Rodar `dotnet test backend/tests/Explanando.Domain.Tests --filter Glosa` → as duas primeiras falham.
- [ ] Implementar: tolerância = `PrecisaoDaOrigem(doc)/2 + PrecisaoDaOrigem(liq)/2 + 0,01`; fora dela, glosa vira nula em vez de `throw`.
- [ ] Testes verdes; commit `Glosa da Camara conferida pela precisao que a origem publica`.
- [ ] **Com aval:** re-coletar só os 78 deputados com rejeição (`checkpoints_ingestao.despesas_rejeitadas > 0`, ~1.600 páginas da API da Câmara) e registrar despesas e R$ recuperados.

### Task 2 (A2): REG_TX_003 se abstém onde a fonte não tem centavo

**Files:** Modify `Regras/RegrasDeTransacao.cs` (RegraPadraoMensalIdentico), `Regras/Cobertura.cs` (`DadoExigido`, `DisponibilidadeDeDados.Em`); Test `Regras/RegrasDeTransacaoEGeografiaTests.cs`, `Regras/CoberturaTests.cs`.

**Interfaces:** Produces `DadoExigido.CentavosDoValor` (novo bit); disponível quando a casa é o Senado ou `|ValorLiquido| < 10.000`. Consumes `Despesa.PrecisaoDaOrigem` (Task 1) para o limite.

- [ ] Teste `Padrao_mensal_se_abstem_na_camara_acima_de_dez_mil`: deputado, 3 meses de R$ 18.970,40 ao mesmo fornecedor → 0 flags.
- [ ] Teste `Padrao_mensal_continua_no_senado_acima_de_dez_mil`: senador, mesmo cenário → 1 flag.
- [ ] Teste de cobertura: as despesas do primeiro cenário contam em `DespesasSemDado` de REG_TX_003.
- [ ] Vermelho → implementar (`Exige = DocumentoDoFornecedor | CentavosDoValor`; filtro na regra) → verde → commit.
- [ ] Ver o rótulo do novo dado nos textos de abstenção (`MotivoDaAbstencao`) e acrescentar a frase "a Câmara publica valores acima de R$ 10 mil sem os centavos".

### Task 3 (A3): REG_TX_002 dita como está

**Files:** Modify comentários em `Regras/RegrasDeTransacao.cs:72-86`, `Auditoria/CoberturaDoParlamentar.cs:18`, `Infrastructure/Persistencia/Semente/SementeDeLimitesCota.cs` (nota: não aplicada de propósito, e por quê: saldo acumula, 934/3.700 meses acima).

- [ ] Sem mudança de comportamento; `dotnet build` limpo; commit `REG_TX_002: o valor mensal da CEAP nao e teto do mes`.

### Task 4 (A4): GEO_002 só olha o que é presencial

**Files:** Modify `Regras/RegrasGeograficas.cs` (`TiposPresenciais.Marcadores`); Test `Regras/RegrasDeTransacaoEGeografiaTests.cs`.

- [ ] `[Theory]` `Fora_da_base_se_cala_para_rubrica_que_nao_e_presencial` com `"MANUTENÇÃO DE ESCRITÓRIO DE APOIO À ATIVIDADE PARLAMENTAR"`, `"LOCAÇÃO OU FRETAMENTO DE AERONAVES"` e a rubrica 22 do Senado → 0 flags.
- [ ] `[Theory]` `Fora_da_base_continua_para_rubrica_presencial` com locação de veículos, embarcações e a rubrica 17 do Senado → 1 flag.
- [ ] Vermelho → marcadores `COMBUST, ALIMENTA, HOSPEDAGEM, LOCOMO, TÁXI, TAXI, VEÍCULO, VEICULO, EMBARCA` (saem `MANUTEN` e `LOCA`; `EhAlimentacaoOuHospedagem`/`EhRubricaAmbigua` inalteradas) → verde → commit.

### Task 5 (A5): combustível aceita oficina, autopeças e representante

**Files:** Modify `Cotas/CompatibilidadeCnae.cs:83`; Test `Cotas/CompatibilidadeCnaeTests.cs` (ou o arquivo de testes da tabela existente).

- [ ] `[Theory]` `Combustivel_aceita_familias_de_lubrificante` com `4520005`, `4530703`, `4612500` → `Compativel`.
- [ ] Vermelho → `["473", "468", "192", "4520", "4530", "4612"]` → verde → commit.

### Task 6 (A6): KYB_002 sem nome de casa

**Files:** Modify `Regras/RegrasDeFornecedor.cs:97`; Test `Regras/RegrasDeFornecedorTests.cs`.

- [ ] Teste `Situacao_irregular_nao_cita_a_camara`: descrição não contém "Câmara" e abre com "A nota de".
- [ ] Vermelho → texto → verde → commit.

### Task 7 (A7, só se aprovado): fato do fornecedor vira uma flag

**Files:** Modify `Regras/RegrasDeFornecedor.cs` (KYB_001, KYB_002), `Regras/RegrasGeograficas.cs` (GEO_002); Tests nos arquivos das regras.

- [ ] Testes: 10 notas de combustível ao mesmo fornecedor incompatível → 1 flag de KYB_001, na de maior valor, descrição com "10 despesas" e o total; 2 tipos diferentes ao mesmo fornecedor → 2 flags; o mesmo para KYB_002 (por fornecedor) e GEO_002 (por fornecedor); nenhum cai em estorno.
- [ ] Vermelho → agrupar como o REG_NET_001 faz → verde → commit por regra.

### Task 8 (A10): fornecedores distintos sem o vazio

**Files:** Modify `Infrastructure/Persistencia/Consultas/ConsultasPublicasEfCore.cs:212-218`; Test em `backend/tests/Explanando.Integration.Tests` (consultas do perfil).

- [ ] Teste: parlamentar com 2 fornecedores e 1 passagem sem documento → `FornecedoresDistintos == 2`.
- [ ] Vermelho → `.Where(d => d.DocumentoFornecedor != CpfCnpj.Vazio)` (o mesmo vazio que `EhVazio` usa) → verde → commit.

### Task 9 (A8, só com aval do download): MEI pela data da opção

**Files:** Modify `Fornecedores/Fornecedor.cs` (+`DataOpcaoPeloMei`, `DataExclusaoDoMei`, `bool? EraMeiEm(int ano)`), `Integracoes/BrasilApi/ClienteBrasilApi.cs:117`, `Regras/RegrasDeFornecedor.cs` (KYB_004), configuração EF + migração `DatasDoMei`; Tests de domínio e de mapeamento.

- [ ] Testes: opção em 2025 → pagamentos de 2024 não somam; excluído em 2024 → 2025 não soma; datas nulas e `OptantePeloMei == true` → silêncio (nulo); mapeamento grava e lê as datas.
- [ ] Vermelho → implementar → verde → commit; reconsultar os 1.411 MEI.

### Task 10 (A11): religar a despesa órfã

- [ ] Achar onde a ligação `fornecedor_id` é feita depois do enriquecimento e por que esta ficou de fora (systematic-debugging); teste que reproduz; corrigir; religar pela correção, não por `UPDATE` à mão.

### Task 11 (A13): bordas

**Files:** Tests em `RegrasDeTransacaoEGeografiaTests.cs`, `RegrasDeFornecedorTests.cs`.

- [ ] `[InlineData]` do recesso: 17/07 fora, 18/07 dentro, 22/12 fora, 23/12 dentro, 01/02 dentro, 02/02 fora.
- [ ] KYB_003 89 dias sinaliza, 90 não; KYB_004 81.000,00 não, 81.000,01 sim; TX_001 4 notas somando 5.000,00 sinaliza, 3 não; TX_004 exatamente 3× sinaliza.
- [ ] Devem passar de primeira (caracterização); se algum falhar, é achado novo: parar e relatar.

### Task 12: versão 7, re-varredura e registro

**Files:** Modify `Regras/MotorDeRegras.cs` (`VersaoAtual = 7` e item da lista); Create `docs/registro/etapa-17-revisao-das-regras.md`; Modify `docs/pendencias.md` §4 (glosa resolvida) e `docs/registro/README.md`.

- [ ] `dotnet test backend/Explanando.sln` verde nos cinco projetos.
- [ ] Antes: guardar `select codigo_regra, tipo, count(*)` (já medido: tabela acima). Rodar `varrer` e `consolidar` (e a passada de rede) pelo worker contra a base local.
- [ ] Depois: mesma consulta; tabela antes/depois por regra e casa; conferir de novo o SQL × API (script de comparação deste levantamento) com zero divergências.
- [ ] Registro com A1–A14, números, e o aviso de contrato: nenhum campo novo/renomeado; `fornecedoresDistintos` −1 em 500 perfis; contagens mudam.
- [ ] Commit.

## Estimativa de contagens (motor 6 → 7, se tudo aprovado sem A7)

| Regra | Hoje | Esperado |
|---|---|---|
| REG_GEO_002 | 3.327 | ~2.384 (−943) |
| REG_KYB_001 | 6.854 | ~6.060 (−792) |
| REG_TX_003 | 7.011 | ~6.050 (−961) |
| REG_KYB_004 | 14 | ≤ 14 |
| Total | 23.992 | ~21.300 |

Com A7, KYB_001 cai para a ordem de 2.000 e KYB_002 para ~170.
