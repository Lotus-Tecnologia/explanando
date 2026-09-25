# Plano de execução — as ideias de 22/09/2026

*Aberto em 2026-09-24.* Origem: nove ideias trazidas pelo Gustavo, avaliadas uma a uma
com medição na base. Sete entram aqui; duas foram adiadas por ADR e uma espera receita.

| Ideia | Destino |
|---|---|
| 3 · Score explicável | **Fase 1** |
| — · Resposta do gabinete na tela | **Fase 1**, junto — o que sobra de bom das ideias 1 e 4 |
| 8 · Comparador de políticos | **Fase 2** |
| 5 · Alertas automáticos | **Fase 3** (RSS) e **Fase 8** (e-mail, Telegram) |
| 2 · Timeline investigativa | **Fase 4** (pagamentos) e **Fase 7** (doações) |
| 6 · Quem beneficia quem | **Fase 5** |
| — · Investigação do TSE | **Fase 6** — destrava três ideias |
| 9 · Mapa de poder expandido | **Fase 7**, parcial |
| 1 · Discussão pública | ❌ [ADR-0020](decisoes/ADR-0020-participacao-publica-adiada.md) |
| 4 · Community notes | ❌ [ADR-0020](decisoes/ADR-0020-participacao-publica-adiada.md) |
| 7 · IA narrativa | ⏸ [ADR-0021](decisoes/ADR-0021-ia-espera-receita.md) — só com receita |

> **Sobre os tamanhos.** P é até um dia de trabalho, M são dois a quatro, G é uma semana ou
> mais. São estimativas minhas, sem folga para imprevisto de fonte — e fonte de terceiro é
> exatamente o que costuma produzir imprevisto neste projeto.

---

## O que vale para todas as fases

**Vocabulário do ADR-0004.** Nenhuma tela, alerta ou rótulo imputa conduta. "Empresa de
fachada", "investigado", "suspeito" não entram — a informação entra, a acusação não. Cada
fase abaixo lista as frases que precisaram ser trocadas.

**Ausência não é fato (ADR-0007).** Regra que não pôde avaliar aparece como *não avaliada*,
nunca como zero. Vale para o score, para o comparador e para os alertas.

**Medir antes de construir.** Toda fase começa por uma consulta que prova que o dado
sustenta a tela. As Etapas 13 e 14 mostraram três vezes que o número fácil era o errado.

**Casamento literal, nunca aproximado.** Onde houver ligação entre cadastros, a chave é
exata ou a ligação não existe. Distância de edição não entra.

---

## Trilho paralelo — a mudança para a VPS (Gustavo)

[ADR-0019](decisoes/ADR-0019-tudo-numa-vps.md). Não bloqueia as fases 1 a 7, que rodam no
ambiente local e sobem junto quando a máquina existir. **Bloqueia a Fase 8**: e-mail precisa
de domínio com SPF e DKIM para não cair em spam, e Telegram precisa do worker de pé em
produção.

---

## Fase 1 — Score explicável · **P** · ✅ entregue em 2026-09-24

**Objetivo.** Trocar "índice 89" por "índice 89, e é daqui que ele vem".

**O que já existe.** Tudo. `flags_anomalias` guarda `codigo_regra` e gravidade de cada
achado; `CatalogoDeRegras.DescricaoDe` já traduz o código para português de leitor; o bloco
de cobertura já distingue regra que avaliou de regra sem dado. Medido em 2026-09-24: o
deputado de maior índice decompõe numa consulta só — 364 achados de `REG_KYB_001`, 11 de
`REG_TX_003`, 5 de `REG_TX_004`, e assim por diante.

**A correção sobre o formato proposto.** O índice é uma **densidade** — peso dos achados
dividido pelas despesas avaliadas —, e não uma soma de pontos. Mostrar "+30 concentração"
seria inventar uma aritmética que o motor não faz. A forma honesta é a **fatia do peso**:
*"62% do peso vem de fornecedor com ramo incompatível com o tipo de despesa"*.

**Entregáveis.**
- Consulta e endpoint com a decomposição por regra, em fatia do peso e em contagem.
- Bloco no perfil do parlamentar, ordenado da regra que mais pesa para a que menos pesa.
- Regras sem dado listadas à parte, como *não avaliadas* — nunca omitidas.

**Fora do escopo, por falta de fonte.** O exemplo trazia *"empresa sem funcionário"*. Não há
fonte pública de número de empregados por CNPJ — a RAIS identificada não é aberta. Essa
regra não existe e não vai aparecer na decomposição.

**Pronto quando** a soma das fatias fecha 100% em teste, e um parlamentar sem amostra
suficiente mostra a explicação da ausência em vez de uma decomposição vazia.

### Junto: a resposta do gabinete ao lado do achado · **P**

É o que sobra de valioso das ideias 1 e 4, com o menor risco possível — ver o
[ADR-0020](decisoes/ADR-0020-participacao-publica-adiada.md).

O domínio já tem `Contestacao`, com texto, anexo e as situações *Publicada* e *Retirada*, e
o produto já decidiu não moderar resposta oficial. **Nenhuma página a exibe hoje.** Entra na
mesma tela da Fase 1 porque é o mesmo lugar: o achado, por que ele pesa, e o que o gabinete
diz sobre ele.

**Pronto quando** uma contestação *Retirada* some da tela, e um achado sem contestação não
mostra caixa vazia — mostra o caminho para o gabinete contestar.

---

## Fase 2 — Comparador de políticos · **P** · ✅ entregue em 2026-09-24

**Objetivo.** "João × Maria", lado a lado, nas métricas que já calculamos.

**O que já existe.** Todas as métricas: volume, índice, achados por gravidade, fornecedores
únicos, e — depois da Fase 1 — a decomposição por regra.

**Três travas técnicas.**
- **Só compara dentro da mesma Casa.** As cotas da Câmara e do Senado são distintas e os
  números não se comparam — está no contrato desde o ADR-0012. O comparador recusa o par
  misto e diz por quê.
- **Não dá para pré-gerar.** São 131.328 pares na Câmara e 6.903 no Senado. A rota é
  dinâmica, com cache, e com **ordem canônica** (alfabética) para que `a×b` e `b×a` sejam a
  mesma página — senão o buscador vê conteúdo duplicado.
- **Parlamentar sem amostra suficiente** (15 dos 513) aparece com o índice como *não
  calculado*, e não como zero. Zero diria que foi medido e deu nada.

**O risco de leitura.** Dois índices lado a lado convidam "X é mais corrupto que Y". O
cabeçalho do comparador diz, antes dos números, que índice de achados técnicos não é juízo
de conduta.

**Pronto quando** o par misto é recusado em teste, e a URL invertida redireciona para a
canônica.

---

## Fase 3 — Alertas por RSS · **P** · ✅ entregue em 2026-09-24

**Objetivo.** O primeiro degrau da recorrência: quem quiser acompanhar um parlamentar,
acompanha — sem cadastro, sem e-mail, sem dado pessoal nosso.

**Por que RSS primeiro.** Não guarda dado de ninguém, não depende de provedor de e-mail, e
funciona no ambiente atual, antes da VPS. É o alerta que custa quase nada e já testa se há
demanda.

**Entregáveis.** Um feed por parlamentar e um feed geral, gerados a partir dos achados novos
de cada ciclo — distinguindo achado por **despesa nova** de achado por **regra alterada**,
como o contrato já faz com `versaoDoMotor`. Só o primeiro é notícia sobre o parlamentar.

**As frases que foram trocadas.**

| Proposto | Vira | Por quê |
|---|---|---|
| "Novo fornecedor suspeito" | "Fornecedor aberto há menos de 90 dias" | "Suspeito" é juízo; a idade da empresa é fato |
| "Crescimento de despesas acima de 300%" | "Gasto mensal três vezes acima da média do próprio parlamentar" | Mesmo conteúdo, com a base de comparação dita |
| "Gastos em município de reduto eleitoral" | **Depende da Fase 6** | "Reduto" exige votação por município, que é dado do TSE |
| "Empresas de fachada detectadas" | **Não entra** | É acusação. Os fatos que a sustentariam (idade, ramo, endereço) já são alertas próprios |
| "Conexão com investigados pela PF" | **Não entra** | Não há fonte de "investigados", e a frase é a imputação mais grave da lista |

**Pronto quando** o feed valida contra o W3C e um ciclo sem achado novo produz feed sem item
novo — e não um item dizendo "nada encontrado".

---

## Fase 4 — Timeline de pagamentos · **P** · ✅ entregue em 2026-09-25

> **O que a medição mudou.** O plano dizia que o primeiro ano começa em fevereiro de 2023.
> Na base, 2023 tem os doze meses — há deputados com despesa em janeiro, da legislatura
> anterior. Por isso "parcial" ficou **por parlamentar**: o ano em que *ele* não teve despesa
> nos doze meses. Cobre o primeiro ano do mandato, o ano corrente, o suplente que assumiu no
> meio e a licença — com a mesma regra.

**Objetivo.** A escada do exemplo: *2022 — 50 mil · 2023 — 200 mil · 2024 — 800 mil*, por
fornecedor, dentro do perfil do parlamentar.

**O que já existe.** Quatro anos de despesa (02/2023 a 08/2026). O desenho de série
temporal com vão declarado já foi feito uma vez, no quadro de pessoal.

**Por que separada da Fase 7.** A escada de pagamento não depende de nada novo. O último
degrau do exemplo — *"torna-se doador de campanha"* — depende do TSE, e não deve segurar os
três primeiros.

**Trava.** O primeiro ano da série é **parcial** (começa em fevereiro de 2023). Crescimento
calculado a partir de um ano incompleto infla o salto; a tela marca o ano parcial e não
calcula variação contra ele.

**Pronto quando** o ano parcial aparece marcado em teste, e um fornecedor com um ano só não
ganha seta de crescimento.

---

## Fase 5 — Quem beneficia quem · **M** · ✅ entregue em 2026-09-25, **sem o nome do sócio**

> **Entregue na saída conservadora, à espera da sua decisão.** A tela mostra "1 sócio em
> comum · Sócio-Administrador nas duas" e os dois CNPJs — o quadro societário de cada um é
> consulta pública na Receita, e a ligação se confere por ali. O nome do sócio não sai nem no
> JSON (há teste garantindo). Se a decisão for mostrar, é um campo a mais em `CadeiaDeSocio`.
>
> **Medido na base local:** sem corte, 18.327 pares de empresas ligados, com até 271 por
> deputado — quase tudo nota isolada. Com R$ 50 mil em cada ponta e sócio de alcance 2 a 5,
> sobram 128 cadeias. Filial da mesma raiz de CNPJ não conta como "outra empresa".

**Objetivo.** Deputado → fornecedor → sócio → outra empresa do mesmo sócio → outro
parlamentar que a contrata.

**O que já existe, medido em 2026-09-24.**
- **86.810 vínculos de sócio**, cobrindo **36.514 dos 40.774 fornecedores (90%)**.
- **`nome_socio` + CPF mascarado é chave quase única**: 49.136 pares para 49.049 nomes. Só
  **75 nomes** aparecem com CPFs diferentes — esses ficam de fora da ligação, porque não há
  como saber se são a mesma pessoa.
- **22.112 sócios alcançam 2+ parlamentares.** 1.117 alcançam 20+ — e esses são ruído, como
  a companhia aérea no mapa de fornecedores.
- **O backend do antigo `/rede` foi mantido** e vira a base: o corte de 2 a 5 parlamentares
  e R$ 50 mil do ADR-0017 continua valendo, e `ObterLigacoesAsync`, que ficou como dívida
  declarada na Etapa 14, ganha a casa que estava esperando — o perfil do parlamentar.

**A decisão que é sua, dentro da fase.** Sócio é **pessoa privada**. O nome dele é público no
quadro societário da Receita, e é o que permite ao leitor conferir a ligação. Mas pôr o nome
de um particular num desenho ao lado de um político é diferente de ele constar num cadastro.
As duas saídas: mostrar o nome (verificável, mais exposto) ou mostrar "sócio em comum" com o
nome só na ficha da empresa (menos exposto, menos verificável). Eu mostraria o nome — mas é
decisão com peso de LGPD, e vai para você antes de a tela existir.

**Fora do escopo.** O salto final do exemplo — *"doação para partido"* — espera a Fase 6.

**Pronto quando** os 75 nomes ambíguos comprovadamente não ligam nada em teste, e o sócio de
alcance alto não aparece.

---

## Fase 6 — Investigação do TSE · **P, com prazo fechado**

**Por que é a fase mais importante.** Uma ingestão destrava três ideias: o último degrau da
timeline (2), o último salto do "quem beneficia quem" (6), os doadores no mapa expandido
(9) — e ainda o alerta de "reduto eleitoral" da Fase 3.

**O que se sabe.** O portal `dadosabertos.tse.jus.br` responde `200` com o conjunto completo
de cabeçalhos de navegador — o mesmo comportamento do gov.br. Não é bloqueio.

**As perguntas que a investigação responde, e nada além delas.**
1. Em que formato e com que chaves vêm as doações de 2022 (federal) e 2024 (municipal)?
2. **O CPF do doador vem mascarado como o nosso?** Doação de empresa é proibida desde 2015,
   então todo doador é pessoa física — e se o CPF do TSE e o da Receita forem mascarados em
   posições diferentes, **não há chave literal**, e a ligação fornecedor→doador não existe.
3. A votação por município está disponível, e em que granularidade?

**Pronto quando** houver `docs/contratos/tse.md` com a resposta às três perguntas e um
**sim ou não** para cada uma das quatro ideias que dependem dele. Prazo: um dia. Se o dia
acabar sem resposta, isso também é resposta.

---

## Fase 7 — Timeline com doação e mapa expandido · **M a G, conforme a Fase 6**

**Só começa se a Fase 6 disser sim.**

**Timeline completa (2).** O degrau *"torna-se doador"* entra na escada da Fase 4, se a
pergunta 2 da Fase 6 tiver chave literal.

**Mapa de poder expandido (9), na parte que tem fonte.**

| Tipo de nó | Situação |
|---|---|
| Políticos, fornecedores, sócios, partidos | ✅ temos — é a Fase 5 com navegação |
| Doadores | ⏳ depende da Fase 6 |
| Associações, think tanks, ONGs | ❌ **sem fonte.** Não há cadastro público que as ligue a parlamentares |

A tela não promete o que não tem. Um "Google Maps do poder" com metade do mapa em branco é
pior que um mapa menor que diz onde termina.

---

## Fase 8 — Alertas por e-mail e Telegram · **M**

**Depende da VPS** (ver o trilho paralelo).

**Por que só aqui.** E-mail guarda dado pessoal de quem assina: exige consentimento
registrado, descadastro em um clique e base legal na política de privacidade. A entidade
`Usuario` já tem e-mail, mas assinatura de alerta é finalidade nova e precisa estar escrita
antes de existir.

**WhatsApp fica de fora.** A API oficial cobra por conversa e exige aprovação da Meta. Entra
quando houver receita que pague por ela — o mesmo critério do ADR-0021.

**Pronto quando** o descadastro funciona sem login, e um alerta nunca sai sem o link para o
achado e para a contestação do gabinete.
