# Etapa 09 — A ingestão do Senado

**Data:** 2026-09-17 · **Etapa anterior:** [08 — A linguagem da interface](etapa-08-linguagem-da-interface.md)

O Senado entrou na base. A coleta foi limpa e o número bateu exatamente com o previsto — e a
**varredura revelou um problema de calibragem que impede publicar a casa como está**.

---

## 1. Objetivo

Ingerir a CEAPS na janela 2023–2026, a mesma da legislatura 57 da Câmara, para que o
redesenho de interface não precise ser feito duas vezes: a assimetria do
[ADR-0012](../decisoes/ADR-0012-assimetria-camara-senado.md) é visível na tela, e desenhar sem
saber se o Senado está na base significa desenhar de novo depois.

## 2. Definido

**O cliente do Senado implementa a mesma porta da Câmara.** É a decisão que carrega esta
etapa inteira. As duas casas entregam despesa de formas opostas: a Câmara serve por deputado,
paginado — a identidade está na chamada; a CEAPS serve **o ano inteiro de todos os senadores
num array só**, sem paginação, com `codSenador` dentro de cada registro. "As despesas do
senador X" não existe como operação contra a origem.

O `ClienteDoSenado` então baixa os anos uma vez, agrupa e serve fatias. Com isso o
`ServicoDeColeta` roda **sem alteração nenhuma** — mesmos checkpoints, mesmo freio de dez
alertas seguidos, mesma retomada que salvou a coleta da Câmara numa queda de energia. O
alternativo seria um segundo orquestrador, com a segunda cópia de cada garantia.

**O cadastro precisa de duas legislaturas.** Só a atual perderia nome e UF de quem assumiu
como suplente e saiu — 2025 teve 82 senadores para 81 cadeiras. Medido: a lista atual cobre
101 dos 118 que gastaram; somando a anterior, cobre **118 de 118**, todos com UF, partido e
foto. O `senado.md` §4 já previa a necessidade; agora ela tem implementação.

**Só entram senadores que de fato gastaram.** O cadastro da legislatura 57 tem 245 registros
contra 118 que lançaram despesa. Criar parlamentar para os outros 127 os colocaria no ranking
com volume zero — e zero por não ter gasto é indistinguível de zero por não ter sido coletado.

**A casa atravessa a fronteira da ingestão.** `DeputadoDaFonte` ganhou `Tipo`, com
`DeputadoFederal` como padrão para o caminho da Câmara não mudar, e `ObterParlamentarAsync`
passou a receber o tipo em vez de cravá-lo. A unicidade é do par `(IdExterno, Tipo)` — como o
comentário em `ParlamentarConfiguracao` já dizia — e com o tipo cravado um senador nunca seria
encontrado, e a segunda passada bateria na chave única em vez de atualizar.

## 3. Executado

| Camada | O quê |
|---|---|
| **Infrastructure** | `Integracoes/Senado/ClienteDoSenado.cs` — o cliente que faltava desde a Etapa 01b |
| **Infrastructure** | `ConverterLotePorSenador` e `DespesasDeUmSenador` — o agrupamento que devolve a identidade |
| **Application** | `DeputadoDaFonte.Tipo`, `Tradutor`, `IRepositorioDeIngestao.ObterParlamentarAsync` |
| **Worker** | Comando `coletar-senado`, com guarda de colisão de checkpoint |

A guarda existe porque `checkpoints_ingestao` é único por `(IdExternoParlamentar, Legislatura)`,
**sem a casa** — foi desenhado quando só havia deputados. Hoje as faixas nem se tocam:
senadores vão até 6.381, deputados começam em 62.881. Mas "impossível na prática" envelhece
sem avisar, e a consulta custa milissegundos.

## 4. Testes

**711 no backend** (428 domínio, 54 aplicação, 177 integração, 42 ponta a ponta, 10
arquitetura) e 113 no frontend. 12 testes novos cobrem o agrupamento por senador, a tradução
de legislatura em anos e a fronteira de data com o banco.

## 5. Descobertas

### 5.1 Dois defeitos que só existiam contra o real

**A configuração de resiliência do cliente novo derrubou os 42 testes de ponta a ponta em
bloco.** A janela de amostragem do disjuntor precisa ser o dobro do timeout de tentativa, e o
framework valida isso **na subida**: 90 s de tentativa com 120 s de janela fazia a API inteira
não iniciar. Teria ido para o Cloud Run assim.

**A primeira coleta real falhou nas três primeiras gravações.** A CEAPS entrega a data com o
offset de Brasília, e `timestamp with time zone` só aceita offset zero. A correção é o relógio
de parede declarado UTC, e **não** `ToUniversalTime()` — converter pelo instante também
zeraria o offset e estragaria o dado, porque uma nota emitida às 23h andaria um dia. Data de
documento é data de **calendário**, que é a leitura que o cliente da Câmara já fazia. Virou
teste que não precisa de banco.

### 5.2 A coleta

| | |
|---|---|
| Senadores | 118 com despesa; 115 visitados, 3 pulados por checkpoint válido |
| Despesas novas | **78.132** (75.652 + 2.480 da validação) |
| Rejeitadas · Alertas · Falhas | **0 · 0 · 0** |
| Tempo | **34m20s** |

78.132 é **exatamente** o total contado na origem antes de escrever a primeira linha de código.

A CEAPS é rápida de um jeito que muda o desenho: um ano inteiro por requisição, 10 MB em
0,7 s. O custo da coleta está inteiro na BrasilAPI, não na fonte do Senado.

### 5.3 A base depois da varredura

| | Antes | Depois |
|---|---|---|
| Parlamentares | 513 | **631** |
| Despesas avaliadas | 713.383 | **791.515** |
| Flags | 21.078 | **36.527** |
| Volume | R$ 803.468.145,38 | **R$ 924.487.309,77** |
| Fora do ranking por amostra | 15 | **49** |
| Sitemap | 517 | **635** |

### 5.4 O achado que trava a publicação do Senado

**A taxa de sinalização do Senado é quase sete vezes a da Câmara.**

| | Despesas | Flags | Taxa | Altas |
|---|---|---|---|---|
| Deputados | 713.383 | 21.078 | **2,955%** | 695 |
| Senadores | 78.132 | 15.449 | **19,773%** | 48 |

Uma em cada cinco despesas do Senado recebe indicador, contra uma em cada 34 na Câmara. **Não
é comportamento — é artefato de tabela.**

`REG_KYB_001` — ramo do fornecedor destoa do tipo do gasto — dispara em **15,83%** das
despesas do Senado contra **0,918%** na Câmara, e sozinha responde por cerca de 80% do
excesso. A causa é estrutural: a CEAPS tem **sete rubricas**, a CEAP tem vinte e quatro. A
rubrica *"Locomoção, hospedagem, alimentação, combustíveis e lubrificantes"* junta cinco
coisas que a Câmara separa, e a lista de CNAEs dela não cobre tudo que cabe no pacote.

Amostra de 420 despesas sinalizadas, com os fornecedores recorrentes:

| Fornecedor | O que é | Família CNAE ausente |
|---|---|---|
| AEROTUR (121), UBER (68) | Agência de viagem, transporte por app | Tecnologia e serviços, não transporte |
| CONDOMÍNIO / EDIFÍCIO (50) | Taxa condominial do escritório | `81` — administração predial |
| RENT (20) | Locadora de veículos | `77` — aluguel de veículos |
| ASSOCIAÇÃO / TAXISTAS (37) | Associação de taxistas | `94` — organizações associativas |

**Nada disso é irregularidade.** Taxa de condomínio é despesa concernente ao imóvel alugado,
que é o que a própria rubrica diz cobrir; Uber é locomoção com CNAE de tecnologia.

> **Por que isso não pode ir ao ar assim.** O ADR-0004 proíbe o produto de imputar conduta. Um
> senador aparecer com sete vezes mais indicadores que um deputado de comportamento idêntico,
> por causa de uma lacuna na tabela, é imputação pela calibragem — a mesma objeção que o
> [ADR-0014](../decisoes/ADR-0014-lacuna-camada-2-e-base-legal.md) levanta contra deixar a
> opacidade da fonte elevar o índice do PEP.

O consolo parcial: as flags de **gravidade alta** do Senado são 48, contra 695 da Câmara. O
vermelho — que o ADR-0013 reserva aos 3,3% — não foi contaminado. A enxurrada é de baixa e
média.

## 6. Pendências

| Pendência | De quem é |
|---|---|
| **Curar a tabela CNAE × rubrica do Senado** e revarrer | **Sua** — pedida desde a Etapa 01b, agora com a medição que ela precisava |
| Decidir se o Senado aparece no ranking antes da curadoria | **Sua** |
| Limites da CEAPS por UF | **Sua** — `REG_TX_002` segue em `SemDado` nas duas casas |
| Acrescentar a casa à chave de `checkpoints_ingestao` | Minha — hoje protegido por guarda, não por esquema |
| Escritórios de apoio do Senado (`REG_NET_003`) | Minha — a fonte existe e não foi ingerida |

## 7. Próxima etapa

**Identidade visual e UI/UX**, com as referências do Gustavo — agora com as duas casas na base,
que era a condição para desenhar uma vez só.

A curadoria da tabela é independente do redesenho e pode correr em paralelo. Depois dela,
`varrer` recalcula tudo: as flags não são imutáveis, e recalibrar não custa recoleta.
