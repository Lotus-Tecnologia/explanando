# Contrato — Senado Federal

Levantado em 2026-08-21 contra os endpoints reais, com o ano de 2025 inteiro (23.615
despesas) como amostra. Nada aqui é suposição: cada número tem uma consulta por trás.

O Senado entrou no escopo pelo [ADR-0011](../decisoes/ADR-0011-v1-e-o-plano-inteiro.md), que
desfez o recorte "somente Câmara" do ADR-0002.

---

## 1. São dois serviços, não um

| Serviço | Host | O que tem | Formato padrão |
|---|---|---|---|
| Dados Abertos Administrativos | `adm.senado.gov.br/adm-dadosabertos` | **CEAPS**, escritórios de apoio, servidores, contratos | JSON |
| Dados Abertos Legislativos | `legis.senado.leg.br/dadosabertos` | Cadastro de senadores, mandatos, matérias | **XML** (JSON com sufixo `.json`) |

O administrativo publica OpenAPI em `/v3/api-docs` (versão 2.2.4 na data do levantamento) e
Swagger UI em `/swagger-ui/index.html`. São 98 endpoints; o que interessa ao V1 são três.

> [!warning] Os envelopes de resposta são inconsistentes entre endpoints do **mesmo** serviço
> `despesas_ceaps/{ano}` devolve um **array cru**. `escritorios-apoio` devolve
> `{ statusCode, msg, data: [...] }`. Um cliente que assuma um formato só quebra no outro.

---

## 2. CEAPS — a despesa

`GET /adm-dadosabertos/api/v1/senadores/despesas_ceaps/{ano}`

> "Retorna uma coleção de despesas CEAPS de todos os senadores do ano especificado."

**Um ano inteiro numa requisição. Sem paginação.** 2025 devolveu 23.615 registros, 10 MB, em
**5,2 s**.

### 2.1 A diferença operacional, e ela é enorme

| | Câmara | Senado |
|---|---|---|
| Requisições de despesa | 513 deputados × páginas | **1 por ano** |
| Consultas de CNPJ (BrasilAPI, 600 ms cada) | ~68.000 | **3.580** |
| Tempo estimado de coleta | **~12,6 h** | **~36 min** |

O Senado inteiro custa menos que 3 deputados. A razão é dupla: são 81 cadeiras contra 513, e
a API entrega tudo de uma vez.

### 2.2 Os treze campos

| Campo | Preenchimento | Nota |
|---|---|---|
| `id` | 100% | Identificador do lançamento |
| `codSenador` | 100% | Liga ao cadastro legislativo — ver §4 |
| `nomeSenador` | 100% | |
| `ano`, `mes` | 100% | A competência |
| `data` | 100% | Data do documento. **Ver as armadilhas** |
| `tipoDespesa` | 99,7% | **7** categorias. 76 registros nulos |
| `tipoDocumento` | 100% | String legível, não código numérico |
| `cpfCnpj` | 100% | 98,6% CNPJ. Ver §3.1 |
| `fornecedor` | 100% | Razão social |
| `documento` | 95,7% | Número da nota. **Só 84% é numérico** |
| `detalhamento` | 38,8% | **Texto livre. A Câmara não tem nada assim** |
| `valorReembolsado` | 100% | **O único valor. Ver §3.2** |

### 2.3 As sete categorias de despesa

| Registros | Tipo |
|---|---|
| 10.393 | Locomoção, hospedagem, alimentação, combustíveis e lubrificantes |
| 3.912 | Aluguel de imóveis para escritório político, compreendendo despesas concernentes a eles. |
| 3.257 | Passagens aéreas, aquáticas e terrestres nacionais |
| 2.451 | Contratação de consultorias, assessorias, pesquisas, trabalhos técnicos… |
| 2.152 | Divulgação da atividade parlamentar |
| 1.365 | Aquisição de material de consumo para uso no escritório político… |
| 9 | Serviços de Segurança Privada |
| 76 | *(nulo)* |

**São outras sete, não as dezesseis da Câmara.** A tabela curada de CNAE × tipo de despesa
não serve: precisa de uma segunda curadoria. E note que a primeira categoria mistura
locomoção, hospedagem, alimentação e combustível numa só — bem mais grossa que as da Câmara,
o que enfraquece `REG_KYB_001` por construção.

### 2.4 Os sete tipos de documento

Nota Fiscal Eletrônica (12.221), Fatura (4.874), Cupom Fiscal (2.007), Recibo (1.700),
Passagem / Bilhete / Código Localizador (1.309), Boleto (848), Nota Fiscal (656).

---

## 3. Onde o Senado é melhor, e onde é pior

### 3.1 Melhor: o CNPJ vem quase sempre, e ele é válido

| | Registros | % |
|---|---|---|
| CNPJ formatado | 23.289 | **98,6%** |
| CPF mascarado | 306 | 1,3% |
| Malformado | 20 | 0,1% |

**3.580 CNPJ distintos, e os 3.580 passam no dígito verificador.** Zero reprovações — a
Câmara não chega perto disso.

> [!warning] Os 20 malformados **não** se consertam com zero à esquerda
> Têm 12 ou 13 dígitos, e a tentação é preencher à esquerda. Testado: dos 11 distintos,
> **um** passa no dígito verificador depois do padding. Os outros dez são corrupção real.
> Tratá-los como não enriquecíveis, igual ao CNPJ sentinela da Câmara.

O CPF mascarado do Senado expõe **5 dígitos** (`457.***.***-00`), contra os 6 que a BrasilAPI
devolve. Menos material ainda para o cruzamento probabilístico de `REG_NET_001`.

### 3.2 Pior: não há comprovante, e não há glosa

**Não existe link para o documento.** Nenhum campo da resposta é URL — verificado nas treze
chaves. A página do Senado não vai poder oferecer "ver a nota", que na Câmara é o que
transforma um indicador em algo que o leitor confere sozinho. É uma assimetria de produto,
não só de dado.

**Só existe `valorReembolsado`.** A Câmara entrega três valores — pedido, pago e cortado — e a
glosa aparece em ~1 de cada 100 notas, sendo sinal próprio. No Senado esse sinal não existe.

### 3.3 O que o Senado tem e a Câmara não

- **`detalhamento`**: texto livre em 38,8% dos registros ("Despesa com aluguel do escritório
  de apoio em PVH"). É insumo direto da Camada 2, que na Câmara teria de inferir tudo do tipo
  de despesa.
- **Escritórios de apoio com endereço completo** — ver §5. Isso destrava `REG_NET_003`, que na
  Câmara está sem fonte.
- **`chefeGabinete`** nominal na lista de senadores. Não é a lista de servidores que
  `REG_NET_002` pediria, mas é uma pessoa nomeada onde a Câmara devolve 405.

---

## 4. Cadastro de senadores, e a ligação entre os serviços

Duas listas, de serviços diferentes, com conteúdos diferentes:

`GET /adm-dadosabertos/api/v1/senadores` — 81 registros, com `nomeParlamentar`, `uf`,
`partido`, `titularSuplente`, `mandato` ("2023 / 2031"), `email`, `chefeGabinete`.
**Não traz código.**

`GET /legis.senado.leg.br/dadosabertos/senador/lista/atual.json` — 81 registros, com
`CodigoParlamentar`, `NomeParlamentar`, `UrlFotoParlamentar`, `SiglaPartidoParlamentar`,
`UfParlamentar`, bloco e liderança.

> [!important] `codSenador` da CEAPS **é** o `CodigoParlamentar` do legislativo
> Verificado: dos 82 senadores presentes na CEAPS de 2025, **72 casam por nome com a lista
> atual e os 72 têm código idêntico. Zero divergências.**
>
> Os 10 restantes não estão na lista de "em exercício" — são suplentes que assumiram e
> saíram, ou mandatos encerrados. **A ingestão precisa de uma fonte histórica de senadores**,
> ou vai perder o nome e a UF de 12% de quem gastou.

Repare também: **82 senadores gastaram em 2025, para 81 cadeiras.** Substituições dentro do
ano são normais e o modelo precisa aceitá-las.

---

## 5. Escritórios de apoio

`GET /adm-dadosabertos/api/v1/senadores/escritorios-apoio` — 83 escritórios, 77 senadores.

```json
{ "parlamentar": { "nome": "...", "estado": "SE" },
  "setor": { "nome": "Escritório de Apoio nº 1 do Senador ...",
             "endereco": "RUA FREI PAULO, 444. SUISSA, ARACAJU, SE. CEP:49052-270",
             "telefone": null } }
```

| | |
|---|---|
| Com CEP | **78 de 83 (94%)** |
| Com número | 79 de 83 |
| Sem endereço | 3 |
| Com telefone | 21 |

O endereço vem numa string só, não estruturado — exige parse. E há degraus de qualidade: ao
lado de endereços completos aparece `"RIO BRANCO."`, só a cidade.

**Isso destrava `REG_NET_003`** (fornecedor no mesmo endereço do escritório de apoio) para o
Senado. Na Câmara essa regra está marcada como sem fonte.

---

### 2.5 O contrato é o mesmo em todos os anos

Baixados e comparados campo a campo:

| Ano | Registros | Campos |
|---|---|---|
| 2019 | 21.634 | 13 |
| 2023 | 18.824 | idênticos |
| 2024 | 21.432 | idênticos |
| 2025 | 23.615 | idênticos |
| 2026 | 11.665 (em curso) | idênticos |

Zero divergência de esquema em cinco anos, e o histórico alcança pelo menos 2019 — mais fundo
que a legislatura 57 da Câmara, que começa em 2023. Cada ano leva de 1 a 5 segundos.

## 6. Armadilhas medidas

### 6.1 Datas corrompidas na origem — 1,36%

321 registros têm data fora do ano declarado. Não são casos de virada de ano:

| Data | Ano declarado | Valor |
|---|---|---|
| `0202-02-24` | 2025 | R$ 6.000,00 |
| `2001-12-27` | 2025 | R$ 128,82 |
| `2005-10-14` | 2025 | R$ 650,00 |
| `2020-02-13` | 2025 | R$ 115,69 |

O ano **202** é erro de digitação na origem. A ingestão precisa decidir o que fazer: rejeitar
esconde gasto público, e aceitar coloca uma despesa do ano 202 na série histórica, onde ela
estraga `REG_TX_004` (pico contra a média) e qualquer gráfico.

**Recomendação:** aceitar o registro, marcar a data como suspeita e usar a competência
declarada (`ano`/`mes`) para tudo que dependa de ordem cronológica.

### 6.2 Data diverge da competência — 12,6%

2.971 registros têm `data` num mês diferente de `ano`/`mes`. Diferente do item anterior, isso
é provavelmente **legítimo**: nota emitida em março e reembolsada na competência de abril. Mas
confirma que `ano`/`mes` é a competência e `data` é o documento — as duas coisas não são
intercambiáveis, e o modelo da Câmara já faz essa distinção.

### 6.3 `documento` não é numérico em 16%

Aparecem `08/2025`, `00000266`, além dos números puros. `REG_KYB_005` (nota fiscal inicial)
depende de numeração sequencial baixa; sobre esses 16% ela precisa se abster, não adivinhar.

### 6.4 `tipoDespesa` nulo em 76 registros

0,3%. Sem tipo, `REG_KYB_001` não tem com o que comparar o CNAE — abstenção, e a cobertura
precisa dizer isso.

### 6.5 A infraestrutura falha de forma intermitente

Durante o levantamento, várias conexões falharam com timeout em ~21 s, alternando com
respostas em 200 ms para a mesma URL. Nenhuma devolveu 429. O cliente precisa de retry com
recuo, e a coleta não pode tratar uma falha como "não há dado".

---

## 7. O que cada regra faz no Senado

| Regra | Situação | Motivo |
|---|---|---|
| `REG_KYB_001` CNAE divergente | ⚠️ | CNPJ em 98,6%, mas **exige nova tabela curada**: os 8 tipos do Senado são outros, e mais grossos |
| `REG_KYB_002` Situação cadastral | ✅ | Mesma fonte, mesmo comportamento |
| `REG_KYB_003` Empresa recém-nascida | ✅ | Idem |
| `REG_KYB_004` MEI acima do teto | ✅ | Idem, com a mesma limitação de dado |
| `REG_KYB_005` Nota inicial | ⚠️ | 16% dos números não são sequenciais |
| `REG_TX_001` Fracionamento | ✅ | |
| `REG_TX_002` Cravado no teto | ❌ | **Precisa da tabela de limites da CEAPS**, que é outra, distinta da CEAP |
| `REG_TX_003` Padrão mensal | ✅ | |
| `REG_TX_004` Pico de consumo | ⚠️ | Funciona, mas as datas corrompidas contaminam a série se não forem tratadas |
| `REG_GEO_002` Fora da base | ✅ | UF do fornecedor via BrasilAPI, UF do senador no cadastro |
| `REG_GEO_003` Recesso | ✅ | Mesmo calendário constitucional |
| `REG_NET_003` Mesmo endereço | 🆕 | **Destravada** pelos escritórios de apoio |
| `REG_NET_004` Concentração partidária | ❌ | **82 senadores está abaixo do piso de 100** que a própria regra declara |

O caso de `REG_NET_004` é o mais interessante: ou a passada de rede roda sobre Câmara e Senado
**juntos** — 594 parlamentares, bem acima do piso — ou a regra nunca fala sobre o Senado. A
primeira opção é melhor e é gratuita: a regra agrupa por fornecedor, e nada nela exige que os
parlamentares sejam da mesma casa. Um fornecedor que atende deputados e senadores do mesmo
partido é, se alguma coisa, mais notável.

## 8. O que ainda falta levantar

- **Tabela de limites da CEAPS por UF.** Como na Câmara, não vem em API. Sem ela `REG_TX_002`
  não roda.
- **Fonte histórica de senadores**, para os 12% que não estão em exercício.
- ~~Tabela CNAE × tipo de despesa do Senado~~ — feita, ver ADR-0012 §5.
