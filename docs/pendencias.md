# Pendências — o que está esperando

*Aberto em 2026-09-18; a seção 5 foi acrescentada em 2026-09-19, quando a Etapa 11 fechou.*

Em 18/09/2026 o foco mudou para o **grafo do Estado** (a adaptação do
[CivLab](https://graph.civlab.org/us) ao Brasil). Tudo abaixo estava em andamento ou
decidido e **não foi abandonado** — está esperando.

> **O que vem a seguir está em [`plano-de-execucao.md`](plano-de-execucao.md)** — as
> ideias de 22/09/2026, em fases. Este arquivo continua sendo o do que está *esperando*.

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
- ~~**A corrida do supervisor.**~~ ✅ **Feito em 2026-09-24.** `EsperarOLogAssentar` espera o log parar de crescer — e não um `Start-Sleep` fixo, que seria chute demais numa coleta que já terminou e de menos num disco lento.
- ~~**Catalogar os 4 tipos de despesa.**~~ ✅ **Feito em 2026-09-24.** Entraram nos códigos 24–27, no fim da lista: os índices estão persistidos em `despesas`, e inserir no meio trocaria o tipo de tudo que já foi ingerido — há teste guardando isso agora. Os outros quatro da CEAP seguem esperando aparecer numa coleta, porque só conhecemos a forma normalizada deles.
- **As 62 despesas rejeitadas por glosa inconsistente** — investigar se é origem ou rigidez nossa.
- **O card viral compartilhável.** Client-side, `html2canvas`, com o disclaimer na imagem e QR de volta. Não depende de backend, e é uma engrenagem de crescimento do Business Model Canvas.
- **O lint e o `.open-next`.** Já resolvido; fica só o registro, porque a armadilha volta a morder quem mexer na config: a config achatada do ESLint **não lê o `.gitignore`**, e a lista dele substitui os padrões do `eslint-config-next` em vez de somar.

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

## 5b. ~~Mostrar o nome do sócio em comum~~ — decidido em 2026-09-25

**Mostrar.** O nome e a qualificação saem como o QSA os publica, no bloco "Sócios em comum" do
perfil; o CPF não sai, nem mascarado. A política de privacidade diz isso na seção de dados
tratados.

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

---

## 7. A fonte de notícia por órgão — medida em 2026-09-21, e nenhuma serve

A barra lateral do mapa do Estado já traz *o que mudou nesta unidade*, que é apuração
nossa. Faltava o segundo feed, de **notícia de verdade** sobre o órgão. Quatro rotas foram
testadas ao vivo, e o resultado é um "não" com número, não um palpite:

| Rota | Resultado medido |
|---|---|
| **Google News RSS** | Funciona, e a **licença do próprio feed proíbe**: *"made available solely for the purpose of rendering Google News results within a personal feed reader for personal, non-commercial use"* |
| **Agência Brasil**, casando o nome do órgão no **título** | **0 de 100** manchetes citam o nome oficial de um órgão. Jornalismo escreve "governo", "Planalto", "Congresso" — não "Ministério do Desenvolvimento, Indústria, Comércio e Serviços" |
| **Agência Brasil**, casando na **descrição** | 46% casam, e a atribuição erra: uma matéria sobre o MPF foi marcada como sendo do **STF**, outra sobre o Comitê Gestor da Internet virou **Congresso Nacional**. É o *entity linking* falhando exatamente como previsto |
| **Feed próprio do órgão no gov.br** | Existe — o da Saúde é `/saude/pt-br/assuntos/noticias-ms/noticias-da-saude/rss.xml` —, mas o caminho é idiossincrático. Descoberta pela sigla acha **5 de 60 autarquias (8%)** e **9 de 26 ministérios (34%)**, e parte desses 34% aponta para o feed da raiz do site ou de uma seção avulsa |

> **A armadilha que quase passou.** A primeira contagem de descoberta deu 60%. Era falsa:
> a maioria dos feeds "encontrados" era `https://www.gov.br/RSS`, o feed do site inteiro,
> servido pela página genérica que aparece quando `gov.br/{sigla}` não existe. Parecer um
> achado não é ser um achado.

### O que sobra, e é decisão de produto

Uma **tabela curada** de ~25 URLs de feed, uma por ministério, verificada item a item. Isso
não é automação: é compromisso de manutenção editorial, o mesmo que o
[ADR-0018](decisoes/ADR-0018-cadeiras-de-comando.md) chama de `OrigemDaCadeira.Curada` e
que hoje não é usado por nenhuma linha.

E há a ressalva de conteúdo: **feed de órgão é o órgão falando de si mesmo**. É release,
não jornalismo. Se entrar, o rótulo tem de ser *"o que o próprio órgão anuncia"* — nunca
*"notícias"* —, porque a diferença entre as duas coisas é o produto inteiro.

### O levantamento feito à mão, em 2026-09-21

A tabela curada foi levantada e **verificada uma a uma**, não estimada. Partiu da lista
oficial em `gov.br/pt-br/orgaos-do-governo` — 38 ministérios e órgãos da Presidência —, e
cada um passou por três tentativas: o caminho padrão `/pt-br/assuntos/noticias`, a
navegação do próprio site atrás de uma página de notícias, e `/RSS` construído sobre a
pasta encontrada. Cada candidato foi então **baixado e lido**.

**Três feeds servem, de trinta e oito.**

| Órgão | Feed | Itens | Estado |
|---|---|---:|---|
| Saúde | `/saude/pt-br/assuntos/noticias-ms/noticias-da-saude/RSS` | 16 | ✅ limpo |
| Desenvolvimento Regional | `/mdr/pt-br/noticias-midr/RSS` | 16 | ✅ limpo |
| Defesa | `/defesa/pt-br/centrais-de-conteudo/noticias/RSS` | 101 | ⚠️ notícia misturada com PDF solto |

O resto se divide em três modos de falhar, e **nenhum deles é "o site está fora do ar"**:

- **12 devolvem o feed da raiz do site**, que no Plone publica todo objeto criado. Os
  primeiros itens do GSI são `Foto 1_5_CNFron.jpeg`; os do MDR-antigo,
  `chuva_em_sao_paulo.webp`; os de Cidades, `8.2026 OUV.pdf`. É o inventário do gestor de
  conteúdo, não a página de notícias.
- **2 devolvem uma lista de pastas**, não de matérias: o MDIC traz
  `2026 (Período eleitoral)` e o feed genérico da Saúde traz `Notícias para os estados`.
- **21 não têm feed nenhum.** MEC, Fazenda, MME e Turismo têm página de notícias, e
  `/RSS` sobre ela responde com zero itens — o recurso não está habilitado.

> **Um tropeço que vale registrar.** A primeira contagem automática deu 14 de 38. Doze
> desses eram o feed da raiz do site. Depois de abrir os itens um a um, sobraram três.
> Duas vezes seguidas neste levantamento o número fácil era o número errado.

### A conclusão

**Não vale construir a ingestão.** Seriam domínio, tabela, migração, coletor, consulta,
endpoint, tela e testes — o mesmo tamanho do bloco das cadeiras — para alimentar **3 dos
336 órgãos do mapa**, menos de 1%. E o efeito colateral seria pior que a ausência: uma
barra lateral que mostra notícia na Saúde, no MDR e na Defesa, e nada nos outros 333, faz
os 333 parecerem abandonados em vez de não publicados.

Se a decisão mudar, o caminho está pronto: as três URLs acima estão verificadas, o formato
é RSS 2.0 comum, e o rótulo tem de ser **"o que o próprio órgão anuncia"** — nunca
"notícias" —, porque é release institucional e não jornalismo.

**Enquanto isso, a barra lateral fica com um feed só**: o nosso, que é a linha do tempo de
mudanças estruturais daquela unidade.
