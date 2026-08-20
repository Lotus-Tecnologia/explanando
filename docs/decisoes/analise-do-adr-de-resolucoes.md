# Análise do ADR de resoluções de produto e infraestrutura

Avaliação do documento `ADR-0005-resolucoes-produto-infraestrutura.md`.

**Resumo:** o conteúdo é bom e fecha onze das doze pontas levantadas. Mas há **um problema
estrutural que precede tudo** — não existe mais uma definição acordada do que é o V1 — e **um
item técnico que a medição desmente**.

---

# 🔴 O problema estrutural: o V1 não está mais definido

Você pediu que esta etapa deixasse "bem definida uma V1". Ela está indo no sentido oposto, e
vale ver o tamanho da deriva.

O [ADR-0002](ADR-0002-escopo-v1-fatia-vertical.md) definiu o V1 como uma **fatia vertical**:

> Somente Câmara dos Deputados. Ingestão CEAP + BrasilAPI, motor determinístico, score e
> ranking público.
> **Fora do V1:** Senado, pagamento PIX, Camada 2 (LLM), API B2B, painel SaaS Pro,
> contestação do gabinete e cards virais.

As decisões acumuladas nas últimas rodadas trouxeram de volta **todos os sete itens** que
aquele ADR tinha excluído:

| Item que o ADR-0002 excluiu | Onde voltou |
|---|---|
| Senado | Ponta 7 — "deixa de ser opcional para o lançamento comercial" |
| Pagamento PIX | Ponta 2 — mecânica do R$ 2,90 |
| Camada 2 (LLM) | Ponta 2 — dossiê é o produto pago |
| API B2B | Pontas 4 e 5 — metadados de cobertura, filtro por severidade |
| Contestação do gabinete | Ponta 6 — âncora por e-mail institucional |
| Cards virais | §2 do novo ADR — disclaimer dentro da imagem |
| Favoritos e alertas | Ponta 2 — ciclo de notificações |

Nada disso é errado como decisão de produto. O problema é que **"V1" hoje significa o plano
de execução inteiro**, e um V1 que contém tudo não é um V1 — é um lançamento único e distante,
exatamente o risco que o ADR-0002 existia para evitar.

## Proposta: três marcos, não um

As decisões já tomadas se separam bem sozinhas. Sugestão de recorte:

**V1 — Publicar com segurança** *(o que falta é pouco)*
- Câmara, ingestão, motor, os dois rankings — **já existe**
- API pública de leitura e frontend
- LGPD: encarregado identificado, política, canal (§2)
- Otimização de storage (§1)
- Varredura global mensal (§4)

**V1.1 — Monetização B2C** *(depende do V1 no ar)*
- Pagamento PIX, Camada 2, dossiê, contenção do LLM (§3)
- Favoritos, notificações, `VersaoMotor`, cooldown (§4)
- Card viral com disclaimer embutido (§2)

**V2 — Comercial B2B** *(depende de contrato e de cobertura)*
- Senado — outra API, spike próprio
- API corporativa com `MetadadosDeCobertura` e filtro por severidade
- Contestação do gabinete
- Limitação de responsabilidade em contrato

O que muda na prática: o **V1 está a poucas etapas de existir**, e cada marco seguinte tem
dependência clara. Sem esse corte, a Etapa 05 herda um escopo que não cabe nela.

---

# 🔴 O item que a medição desmente

## §1 — "A URL será reconstruída usando o `codigo_documento`"

Medi na base real. **A reconstrução não funciona com o `codigo_documento` sozinho.**

### Há cinco padrões de URL, não um

| `codTipoDocumento` | Despesas | Sem URL | Padrão |
|---|---|---|---|
| 0 (Nota Fiscal) | 15.264 | **7.910** | `/documentos/publ/{carteira}/{ano}/{codigo}.pdf` |
| 4 (NF-e) | 9.919 | 0 | `?ideDocumentoFiscal={codigo}` |
| 1, 2, 3 | 3.357 | 35 | `/documentos/publ/{carteira}/{ano}/{codigo}.pdf` |

### Falta um componente que não temos

O caminho tem um `{carteira}` — 3308, 3536, 3712 — que é o **número da carteira parlamentar**.
Verifiquei duas coisas:

- ✅ É **constante por deputado** (1 valor distinto por parlamentar).
- ❌ **A API da Câmara não expõe esse campo** em nenhum endpoint. Não está no detalhe nem em
  `ultimoStatus`.

Ou seja: o único lugar onde esse número existe é **dentro da própria URL que o ADR propõe
apagar**.

### E 52% das notas fiscais simplesmente não têm documento publicado

7.910 das 15.264 despesas de tipo 0 têm `url_documento` vazio. Reconstruir uma URL para elas
mandaria o cidadão para um 404 — na página pública, no botão "ver a nota fiscal", que é
justamente onde a credibilidade do produto se prova.

### A otimização continua valendo — só precisa de mais três coisas

O ganho de ~2,4 MB é real. Para colhê-lo sem quebrar nada:

1. **Extrair a carteira uma vez** de qualquer URL existente do deputado e guardar em
   `Parlamentar.NumeroCarteira`. É um campo por parlamentar, não por despesa.
2. **Guardar um booleano `TemDocumentoPublicado`** — 1 bit no lugar de 86 bytes, e impede o
   link quebrado.
3. **Dois templates**, escolhidos por `codigo_tipo_documento`.

Ressalva que sobra: deputado que nunca teve documento publicado não terá carteira extraível.
Para esses, `TemDocumentoPublicado = false` resolve — não há link a reconstruir mesmo.

---

# 🟡 Ajustes menores

## §1 — `tipo_despesa` como Enum é frágil

A proposta é `smallint` mapeado para Enum em C#. Os códigos da Câmara mostram acréscimos
históricos evidentes: 1 a 15, depois 119, 120, 121, 122, 123, 137, 145, 998, 999. **Eles
adicionam códigos novos ao longo do tempo.**

Um Enum quebra quando aparecer o código 146: ou lança, ou vira um valor inválido silencioso.
Uma **tabela de referência** com FK, semeada do endpoint `/referencias/deputados/tipoDespesa`,
absorve o código novo sozinha — e mantém a mesma economia de bytes.

É o mesmo princípio que o `CompatibilidadeCnae` já aplica: tipo desconhecido faz a regra se
abster, não explodir.

## §2 — A LAI não é base legal da LGPD

O ADR diz que o B2C "opera sob a Lei de Acesso à Informação (LAI)". A LAI ampara **obter** a
informação pública; a LGPD exige que se nomeie uma das hipóteses do **art. 7º** para
**tratar** dado pessoal — e a LAI não é uma delas.

Para o B2B o ADR já acerta ao citar o legítimo interesse (art. 7º, IX). Provavelmente a mesma
base serve ao B2C, com avaliação separada por finalidade — a LAI entra como fundamento de
interesse público dentro dessa avaliação, não no lugar dela.

**Não sou advogado.** Sinalizo porque é o tipo de detalhe que um parecer resolve em minutos e
que, errado numa Política de Privacidade publicada, é difícil de desfazer.

Faltam ainda dois itens que costumam vir junto:
- **Avaliação de legítimo interesse documentada** — a ANPD pode solicitá-la quando a base é o
  art. 7º, IX.
- **Prazo de resposta ao titular** — o ADR define o canal, não o prazo.

## §3 — Lista de palavras não pega insinuação

`VocabularioDeCompliance` recusa "fraude", "crime", "desvio". Não recusa *"o padrão de gastos
indica que o parlamentar direcionou recursos para empresa ligada a seu círculo"* — que não usa
nenhuma palavra proibida e imputa exatamente o mesmo.

A validação é necessária e barata, e deve ficar. Mas precisa de duas companhias:

- **Limite de regenerações.** Se o LLM falhar N vezes, cair para um **dossiê de template
  determinístico** — fatos, regras e links, sem narrativa. Sem isso, o caso ruim é um laço
  infinito consumindo token pago.
- **O prompt como primeira defesa**, não a validação. Instruir a não caracterizar conduta é
  mais eficaz que filtrar depois.

## §4 — A varredura global mensal cabe, e o dado confirma

Vale registrar porque é uma boa notícia: **a rotina mensal é barata.** A primeira coleta de 20
deputados levou 36 minutos, mas a recoleta levou **47 segundos** — os fornecedores já estavam
em cache e o hash pulou o que existia.

Extrapolando: a atualização mensal dos 513 é dominada pela paginação da Câmara (~6.150
requisições), na casa de **20 a 30 minutos**. Cabe folgado.

O que é caro é a **carga inicial** — 30 a 60 mil consultas à BrasilAPI, algo entre 1,5 e 3
horas. Vale o ADR distinguir as duas, porque só a primeira precisa de planejamento.

## §4 — O cooldown deixa uma ambiguidade sobre o que é pago

O ADR diz que, dentro de 24h, o botão "exibirá o dossiê recém-gerado gratuitamente para os
próximos usuários".

Mas na resposta da ponta 2 o que ficava público era **o dado atualizado** — e o **dossiê** era
o produto pago. Se o dossiê inteiro também fica grátis por 24h, o produto pago não tem
nenhuma escassez.

**A decidir:** dentro da carência, o próximo usuário recebe o dossiê completo, ou só a
informação de que o dado está fresco (e o botão fica desabilitado)?

## §4 — `VersaoMotor` sozinha não distingue despesa nova

A coluna é necessária, mas para responder "esta flag veio de despesa nova?" também é preciso
saber quando a despesa entrou. Há uma facilidade já disponível: `Entidade.Id` usa **UUID v7**,
que é ordenável por tempo de criação — dá para saber se a despesa é posterior à varredura
anterior sem coluna nova.

---

# 🔧 Correção mecânica: colisão de numeração

O arquivo foi criado como `ADR-0005`, mas **já existe** um `ADR-0005-tdd-e-niveis-de-teste.md`.
Dois ADRs com o mesmo número quebram a referência cruzada — e este documento já é citado por
outros.

Renomeado para **ADR-0010**, e o índice atualizado.
