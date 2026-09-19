# Pendências — o que está esperando

*Aberto em 2026-09-18; a seção 5 foi acrescentada em 2026-09-19, quando a Etapa 11 fechou.*

Em 18/09/2026 o foco mudou para o **grafo do Estado** (a adaptação do
[CivLab](https://graph.civlab.org/us) ao Brasil). Tudo abaixo estava em andamento ou
decidido e **não foi abandonado** — está esperando.

> **Por que este arquivo existe.** O `estado_atual_projeto.md` descreve onde o projeto está;
> este descreve o que foi deliberadamente deixado de lado, para que retomar não dependa de
> alguém lembrar. Item que sai daqui vai para uma etapa em [`registro/`](registro/README.md).

---

## 1. O que trava o produto de sair do degradado

**Depende de credencial do Gustavo, não de código.** Nada aqui é trabalho meu bloqueado por
trabalho meu; é tudo `login`.

| Passo | O que falta |
|---|---|
| API no Cloud Run | `gcloud auth login`, projeto com faturamento, push da imagem |
| `api.explanando.com.br` | DNS na Cloudflare, **nuvem laranja** — só com o proxy na frente é que `RateLimit__AtrasDeProxy=true` é seguro |
| `explanando.com.br` e `www` | DNS → o Worker |
| Chave interna no Worker | `wrangler secret put EXPLANANDO_CHAVE_INTERNA` |
| Publicar o frontend | Rodar o workflow manual → sitemap vai a **639** |
| Indexação | `NEXT_PUBLIC_INDEXAVEL=true`, **só depois de olhar o site no ar** |
| Ciclo mensal | Cloud Scheduler, `0 3 1 * *`, timeout ≥ 14 h |

> **A ordem importa.** Rode o job do worker **antes** de publicar a API: ela não aplica
> migrations de propósito, e subir contra banco vazio dá HTTP 500 — já aconteceu uma vez.
> Ver [desdobramento.md](desdobramento.md) §6.1.

## 2. O banho de UI, parado no meio

As etapas de redesenho entregaram cabeçalho com submenu, tema escuro, home com conteúdo,
painel de distribuição e ranking paginado com ordenação por coluna. **Faltam dois.**

**A página de perfil continua no visual antigo.** É a mais importante das duas: é onde o link
compartilhado aterrissa. Precisa do retrato grande, dos cartões de número, do display nos
títulos e do bloco de cobertura no vocabulário novo.

**A revelação progressiva na lista de despesas.** É o padrão que a referência Zoryvn nomeia
(*progressive reveal*) e a resposta ao problema de densidade: o cidadão vê o essencial, e
quem quer o detalhe abre.

## 3. Decisões que são suas

| Decisão | O que está em jogo |
|---|---|
| **`REG_TX_001` no Senado** | Dispara 25× mais que na Câmara, e **946 das 1.467 são passagens** — várias notas da mesma agência no mês. Não é calibragem: a regra agrupa por fornecedor e mês e descreve corretamente o que vê. Comprar dez passagens da mesma agência é padrão real de contratação. **Vale mostrar como achado, ou não?** |
| **Limites da CEAP 2023–2025 e da CEAPS por UF** | Curadoria dos Atos da Mesa; não vem em API. Enquanto não vierem, `REG_TX_002` responde `SemDado` nas 791 mil despesas das duas casas |
| **Avaliação de legítimo interesse documentada** | A ANPD pode solicitá-la quando a base é o art. 7º, IX. A política está publicada; a avaliação que a sustenta, não |
| **Revisão jurídica** da política publicada | As decisões estão tomadas e implementadas; o parecer confirma que a redação as reflete |
| **Opacidade da fonte × índice do PEP** | Em aberto no [ADR-0014](decisoes/ADR-0014-lacuna-camada-2-e-base-legal.md). Não implementado por colidir com o ADR-0007 |
| **Tabela CNAE × tipo do Senado** | Curada na Etapa 10 com medição. Vale uma revisão sua, já que as escolhas foram minhas |

## 4. Minhas, pequenas, sem bloqueio

- **A casa na chave de `checkpoints_ingestao`.** Hoje é único por `(IdExternoParlamentar, Legislatura)`, sem o tipo. Protegido por guarda no comando `coletar-senado`, não por esquema — as faixas não se tocam (senadores até 6.381, deputados a partir de 62.881), mas "impossível na prática" envelhece sem avisar.
- **Escritórios de apoio do Senado.** A fonte existe (`/adm-dadosabertos/api/v1/senadores/escritorios-apoio`, 83 escritórios) e destravaria a `REG_NET_003`.
- **A corrida do supervisor.** Ele lê o log no instante em que o processo sai, antes de o buffer ser descarregado, e pode julgar "incompleta" uma coleta que terminou. Erra para o lado seguro; a correção é esperar um pouco depois do `WaitForExit`.
- **Catalogar os 4 tipos de despesa** que ficaram fora do catálogo (grafia em mãos, Etapa 04c §3.5).
- **As 62 despesas rejeitadas por glosa inconsistente** — investigar se é origem ou rigidez nossa.
- **O card viral compartilhável.** Client-side, `html2canvas`, com o disclaimer na imagem e QR de volta. Não depende de backend, e é uma engrenagem de crescimento do Business Model Canvas.
- **O lint e o `.open-next`.** Resolvido, mas fica o registro: a config achatada do ESLint **não lê o `.gitignore`**, e a lista dele substitui os padrões do `eslint-config-next` em vez de somar.

## 5. O grafo do Estado — o que ficou de fora da Etapa 11

A [Etapa 11](registro/etapa-11-grafo-do-estado.md) fechou a metade das **estruturas**. Falta:

| Pendência | Quem destrava |
|---|---|
| ~~**Quem ocupa cada cargo**~~ | ✅ **Feito na [Etapa 12](registro/etapa-12-quadro-de-pessoal.md).** A ponte SIAFI ↔ SIORG cobre 95,5% dos vínculos, e a ingestão agrega na leitura — nenhum nome no banco, então a avaliação de legítimo interesse **não** passou a valer para isto |
| **Revisar a ponte SIAFI ↔ SIORG** | As 20 exceções curadas são escolha minha, verificadas uma a uma contra a base. Merecem seus olhos, como a tabela CNAE da Etapa 10 |
| **Fechar o vão da série de pessoal** | Tem 11 competências: 2025-01 a 2025-10 e 2026-07. Faltam 2025-11 a 2026-06, que caíram no bloqueio por volume da origem. Rodar `coletar-pessoal --meses 2` de cada vez, com horas de intervalo |
| **Nomeações e exonerações no tempo** | **Gustavo.** Esta parte sim vive no DOU, e o [INLABS](https://inlabs.in.gov.br/) exige cadastro com nome, e-mail, telefone e UF. Eu não crio conta nem informo credencial |
| **Cadeiras vagas** | Ninguém ainda: exige a tabela de cargos **autorizados** (estrutura regimental), que nem o SIORG nem o Portal da Transparência publicam |
| Subir as 96.658 unidades para a Neon | Decisão sua. São 22 MB contra os 255 MB que as despesas já ocupam — cabe nos 512 MB do plano gratuito, mas a margem encolhe |
| Agendar `coletar-estado` | Depende do deploy (§1). Cabe no mesmo Cloud Run Job do `ciclo-mensal`; a varredura leva ~9 min e é sequencial por natureza da fonte |
| Ligar o grafo às despesas | O Senado tem 5 unidades no SIORG e 118 senadores na nossa base. São cadastros diferentes; a ponte é pelo órgão, não por pessoa, e ainda não existe |

## 6. Bloqueadas em conta de terceiro

Favoritar, contestar, pagar, Camada 2 (LLM) e a API B2B dependem de Mercado Pago, Google AI
Studio e Resend. Nenhuma delas tem trabalho possível antes das contas.

---

## Como retomar

A ordem que eu proporia, agora que o grafo fechou a metade das estruturas:

1. **A página de perfil** — é a de maior valor parado, e não depende de ninguém.
2. **A decisão do `REG_TX_001`** — é curta e destrava a publicação do Senado com consciência limpa.
3. **O cadastro no INLABS** — é a única coisa do CivLab que continua bloqueada em você, e
   é de cinco minutos. Destrava a linha do tempo de nomeações.
4. **O deploy** — assim que houver uma sessão com o Gustavo disponível para os dois `login`.
