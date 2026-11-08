# Etapa 17 — Revisão das regras, dos valores e dos bugs do motor

**Data:** 2026-11-08
**Plano:** [2026-11-08-etapa-1-regras.md](../superpowers/plans/2026-11-08-etapa-1-regras.md) (Etapa 1 do plano mestre da última versão)
**Motor:** versão 6 → **7**

O Explanando foi encerrado como produto em 07/11/2026 e vira portfólio aberto. Esta etapa
revisou as 12 regras, os parâmetros e as leituras contra a base local (790.303 despesas, 631
parlamentares, 23.992 flags no motor 6), conferindo os números por SQL independente contra a
API. Tudo roda local; nada foi publicado.

---

## 1. O que conferiu certo antes de mexer

SQL independente × API, nos 631 perfis e nos 6 rankings (2 casas × volume, índice,
gravidade): volume, despesas avaliadas, flags por gravidade, índice, cobertura de fornecedor,
pisos amostral e de cobertura, composição por regra, flags por regra na cobertura, série
mensal (total, quantidade, glosa e nulidade da glosa), ranking × perfil e ordem dos
rankings. Só `fornecedoresDistintos` divergiu (A10).

## 2. Achados e o que foi feito

| # | Achado | Feito |
|---|---|---|
| A1 | **As "62" rejeições por glosa eram 146, em 78 deputados, e a rigidez era nossa.** A API da Câmara serializa valores com seis dígitos significativos: acima de R$ 10 mil o centavo some do documento e do líquido, mas a glosa vem com duas casas. A conferência com tolerância de R$ 0,01 rejeitava a despesa inteira, e o volume (o número público) saía menor | Tolerância pela precisão da origem (`Despesa.PrecisaoDaOrigem`); glosa que não fecha além disso entra **desconhecida** (nula), nunca rejeitada. Recoleta dos 78 até 08/2026 (§3) |
| A1b | Um caso de incoerência real na origem (documento 16.291, líquido 7.028,39, glosa 11) | Coberto pelo A1: entra com glosa nula |
| A2 | `REG_TX_003` dizia "sem variação de centavos" sobre valores em que a fonte não publica centavo (961 flags da Câmara ≥ R$ 10 mil) | Dado exigido novo `CentavosDoValor`: a regra se abstém na Câmara a partir de R$ 10 mil e a cobertura diz por quê. O Senado continua avaliado |
| A3 | **`REG_TX_002` nunca rodou**: a semente dos limites não tem chamador e `limites_cota` está vazia. Semear também seria errado: o saldo da CEAP acumula no exercício, e 934 de 3.700 meses de 2026 ficariam acima do "teto" (967 falsos positivos) | Não semeado. Comentários corrigidos; a regra se abstém (TetoDaCota ausente) até ser redesenhada sobre o acumulado anual |
| A4 | `REG_GEO_002` tratava como presencial manutenção de escritório, material de consumo do Senado e frete de aeronave | Marcadores: combustível, alimentação, hospedagem, locomoção, táxi, veículo, embarcação |
| A5 | Combustível na Câmara recusava oficina (4520), autopeças (4530) e representante de combustível (4612) | Aceitos |
| A6 | O texto do `REG_KYB_002` abria com "regra da Câmara" também para senador | Texto sem casa |
| A7 | **Fato do fornecedor repetido por nota**: um par deputado–posto gerava 342 flags de `REG_KYB_001`, e o índice media quantas vezes se abasteceu | `REG_KYB_001` emite uma flag por (fornecedor, tipo); `REG_KYB_002` e `REG_GEO_002`, uma por fornecedor; na despesa de maior valor, com quantidade e total na descrição; estorno fica de fora |
| A8 | `REG_KYB_004` usava a opção pelo MEI **de hoje** para pagamentos de 2023 a 2026; a BrasilAPI publica as datas e o cliente as descartava | Guardadas `data_opcao_pelo_mei` e `data_exclusao_do_mei` (migração `DatasDoMei`); a regra só soma o que foi pago enquanto era MEI; optante sem data → sem dado. Reconsulta dos MEI (§3) |
| A9 | Postos com CNAE de aluguel de imóveis na Receita (942 flags num só) | Mantido: o fato é verdadeiro; o A7 tirou a repetição |
| A10 | `fornecedoresDistintos` contava o documento vazio das passagens | Filtrado |
| A11 | Uma despesa do Senado sem `fornecedor_id` com o fornecedor cadastrado. Causa: a consulta do CNPJ falhou numa passada e, na seguinte, a despesa era pulada pelo hash | Ao fim de cada ingestão, as despesas do parlamentar sem fornecedor são ligadas ao fornecedor já cadastrado |
| A12 | O card mostra índice para deputados abaixo do piso de 50% de cobertura | **Para a sessão do frontend** |
| A13 | Bordas exatas sem teste | `BordasDasRegrasTests`: passaram de primeira, nenhum achado novo |
| A15 | **Achado na recoleta:** a Câmara passou a publicar o CNPJ da companhia nas passagens SIGEPA, e o documento estava no hash — toda coleta nova duplicaria as 184.280 passagens da base | Hash da SIGEPA sem o documento (§3) |
| A14 | A Etapa 16 registrou 23.977 flags; a base tinha 23.992 — a diferença são as 15 do `REG_NET_004` | Corrigido aqui |

## 3. Recoleta, reconsulta e re-varredura

Tudo contra a base local (Postgres 17 em Docker), pelo worker.

1. **`recoletar-rejeitadas --ate 202608`** — só os 78 deputados com rejeição no checkpoint e
   só até a competência que a base cobre (ela foi coletada em 21/08/2026), para não dar a
   eles meses que os outros não têm. Um deputado falhou na fonte (valor nulo onde se espera
   número) e foi pulado; nenhuma despesa voltou a ser rejeitada.
2. **A15 — achado novo na recoleta.** Ela trouxe 32.193 despesas, não ~150. **28.630** eram
   passagens SIGEPA já na base: em agosto a Câmara as publicava sem documento do
   fornecedor, e agora publica o CNPJ da companhia. O documento entra no hash, então a
   despesa reaparece como nova. As outras ~3.400 eram despesas lançadas pela Câmara depois
   de agosto. **Removidas todas as linhas inseridas pela recoleta, menos as 151 cuja glosa
   não fechava na tolerância antiga** (as recuperadas pelo A1), e os 110 fornecedores que a
   recoleta criou e que ficaram sem despesa. Nenhuma flag nem contestação apontava para elas.
   **Corrigido no mesmo dia:** o hash da passagem SIGEPA deixa de incluir o documento do
   fornecedor. As 184.280 da base foram gravadas com documento vazio, então o hash delas não
   muda e não há migração. **Prova na fonte real:** a mesma recoleta rodada de novo trouxe
   3.412 despesas (as lançadas depois de agosto, 1.520 delas passagens novas) e só 2 com
   par na base — as mesmas 2 de antes, que mudaram de competência na origem. Essas 3.412
   também foram removidas: a base fica no recorte de 21/08 mais as 151 da glosa.
3. **`reconsultar-mei`** — 1.411 optantes na BrasilAPI; 1.410 voltaram com a data da opção
   (1 sem resposta). 33 deixaram de ser MEI desde a coleta. A primeira tentativa caiu num 503
   e não salvou nada; o comando passou a salvar a cada 50 e a pular a falha.
4. **`varrer`** (com a passada de rede) e **`consolidar`** com o motor 7.

## 4. Antes e depois

**Despesas e volume**

| | Antes | Depois |
|---|---|---|
| Despesas da Câmara | 711.796 | 711.947 (+151) |
| Volume da Câmara | R$ 802.407.895,78 | R$ 803.843.072,61 (**+R$ 1.435.176,83**) |
| Despesas da Câmara com glosa desconhecida | 0 | 46 |
| Senado | 78.507 / R$ 121.516.838,78 | sem mudança |

**Flags por regra** (deputados / senadores)

| Regra | Motor 6 | Motor 7 | |
|---|---|---|---|
| REG_KYB_001 | 6.531 / 323 | 1.660 / 98 | −5.096 (A5 + A7) |
| REG_GEO_002 | 2.941 / 386 | 503 / 104 | −2.720 (A4 + A7) |
| REG_TX_003 | 5.973 / 1.038 | 5.014 / 1.038 | −959 (A2), Senado intacto |
| REG_KYB_002 | 681 / 54 | 146 / 26 | −563 (A7) |
| REG_KYB_004 | 14 / 0 | 12 / 0 | −2 (A8) |
| REG_KYB_005 | 1.751 / 287 | 1.752 / 287 | +1 (despesa recuperada) |
| REG_TX_004 | 1.887 / 327 | 1.884 / 327 | −3 |
| REG_TX_001 | 421 / 521 | 421 / 521 | = |
| REG_KYB_003 | 535 / 31 | 535 / 31 | = |
| REG_GEO_003 | 200 / 0 | 200 / 0 | = |
| REG_NET_001 | 73 / 3 | 73 / 3 | = |
| REG_NET_004 | 15 / 0 | 15 / 0 | = |
| REG_TX_002 | 0 | 0 | não roda (A3) |
| **Total** | **23.992** | **14.650** | **−9.342 (−39%)** |

**Índice e ranking**

| | Deputados | Senadores |
|---|---|---|
| Ranqueados por índice | 406 → 406 | 86 → 86 |
| Mediana do índice | 4,20 → 2,60 | 4,83 → 4,33 |
| Top 10 por índice que continua no top 10 | 2 de 10 | 8 de 10 |
| Parlamentares com volume diferente | 69 | 0 |

**Conferência depois:** SQL independente × API de novo, nos 631 perfis e nos 6 rankings, com
**zero divergências** (inclusive `fornecedoresDistintos` e a nulidade da glosa na série
mensal). `dotnet test` verde nos cinco projetos.

## 5. Contrato da API

Nenhum campo novo, renomeado ou com tipo diferente. Mudam valores:

- `fornecedoresDistintos` cai 1 nos perfis com passagem sem documento;
- contagens de flags, composição e índice (motor 7);
- a lista de despesas marca menos notas: a flag de fornecedor fica numa só;
- a cobertura de `REG_TX_003` passa a ter despesas sem dado na Câmara, com o motivo "a Câmara
  publica valores acima de R$ 10 mil sem os centavos"; a de `REG_KYB_004`, nos MEI sem data.

O enum `DadoExigido` ganhou o bit `CentavosDoValor` (4096); ele só aparece no banco
(`coberturas_regra`), não na API, que expõe o motivo em texto.

## 6. O que ficou de fora

- `REG_TX_002` redesenhada sobre o acumulado anual da CEAP.
- Fornecedores que **deixaram** de ser MEI antes da coleta (`opcao_pelo_mei = false` com data
  de exclusão) não foram reconsultados: só os 1.411 optantes. Fornecedor novo já entra com as datas.
- As passagens SIGEPA já na base continuam **sem** o CNPJ da companhia que a Câmara agora
  publica (o A15 só impede a duplicata; não atualiza a despesa conhecida).
- Despesas de um deputado cuja coleta falha no meio são gravadas junto com as do deputado
  seguinte, porque o contexto do EF é compartilhado no laço (vale para `coletar` e para
  `recoletar-rejeitadas`). Na prova do A15, 10 despesas do deputado 160527 entraram assim.
- A despesa órfã do Senado só se religa na próxima coleta do Senado, que não foi feita aqui.
- O checkpoint dos 78 deputados continua registrando as rejeições antigas: a recoleta não
  passa pelo serviço de coleta, que é quem grava checkpoint.
