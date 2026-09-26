# ADR-0014 — A lacuna como informação, o dossiê como produto, e a base legal corrigida

**Data:** 2026-08-22 · **Situação:** Aceita
**Atualiza:** [ADR-0010](ADR-0010-resolucoes-produto-infraestrutura.md) §2 e §4

## Contexto

Sete pontas ficaram abertas depois da Etapa 07 — algumas desde as
[questões de negócio](questoes-abertas-de-negocio.md), outras levantadas pela
[análise das respostas](analise-das-respostas-parte-1.md). Elas bloqueavam blocos inteiros do
plano: Camada 2, contestação do gabinete, API B2B e o desdobramento. Este ADR as fecha.

## Decisão

### 1. Lacuna de dado nunca vira suspeita — e a lacuna em si é publicada

Reafirma o [ADR-0007](ADR-0007-postura-permissiva-do-motor.md) e o torna explícito para as
tabelas curadas: **sem o limite oficial daquele estado e período, a regra aborta a avaliação e
reporta `SemDado`.** Zero falso positivo é o critério, não a cobertura.

> Nada a implementar: `RegraGastoNoTetoDaCota` já se comporta assim, com teste unitário e teste
> de cobertura. Fica registrado porque a decisão agora é explícita, e não consequência.

**Não se semeia limite provisório.** Um teto inventado para 2023–2025 produziria flags contra
pessoas nomeadas com base em número que ninguém publicou — exatamente o dano que a abstenção
existe para evitar. A tabela espera o dado real dos Atos da Mesa.

**No B2C, a opacidade é ato cívico.** Onde a origem não publicou, a página diz **"o governo não
disponibilizou esta informação"**, atribuindo a ausência a quem a produziu. O espaço vazio
seria lido como falha da plataforma; a frase transforma a falha de transparência da fonte em
informação para o leitor.

### 2. A Camada 2 é comodidade, não acusação

O entregável do pagamento é o **"Relatório Cidadão de Achados Técnicos"**, estritamente nos
limites do [ADR-0004](ADR-0004-nomenclatura-tecnica.md): fatos, códigos de regra, valores e
links para a fonte. **Vende-se a formatação e a comodidade, nunca a acusação.**

Isso encerra a preocupação que a análise levantou sobre o "PDF pronto para protocolar no
MPF/TCU": o produto não produz o instrumento da representação, produz o relatório que o cidadão
usa como quiser, sob autoria dele.

### 3. O presente é o dossiê; a transparência é de todos

Substitui a carência de 24 h do [ADR-0010](ADR-0010-resolucoes-produto-infraestrutura.md) §4,
que deixava o dossiê inteiro gratuito e apagava a escassez do produto pago.

| O quê | Quem vê |
|---|---|
| Dado determinístico atualizado pela varredura paga | **Todos**, de graça, na página pública |
| O Relatório Cidadão formatado | **Só quem pagou** — por e-mail ou área logada |

Quem paga compra o **poder de atualizar** e a comodidade do relatório. A sociedade ganha a
atualização. O propósito declarado na Visão — transparência pública gratuita — continua
inteiro, e o produto pago passa a ter escassez real.

### 4. Autenticação do gabinete por OTP numérico, não por link

O acesso da Área do Parlamentar é verificado por **PIN de 6 dígitos enviado ao e-mail
institucional** (`@camara.leg.br` ou `@senado.leg.br`).

**Magic link clicável foi descartado por um motivo operacional concreto:** firewalls
corporativos governamentais varrem mensagens clicando nos links em busca de malware — e o
clique do scanner consome o token de uso único antes de a pessoa abrir a caixa. O login
falharia sem ninguém entender por quê, e o sintoma pareceria bug nosso.

Isso resolve a **questão 6**: a verificação é por **posse** da caixa institucional, não por
domínio do endereço. Validar só o domínio deixaria qualquer `@camara.leg.br` — inclusive de
assessoria adversária — publicar "Resposta Oficial do Gabinete" na página de qualquer
parlamentar, e o selo de oficialidade é justamente o que dá peso à resposta.

Limitação que permanece: verifica **o gabinete**, não cada assessor. Quem controla a caixa
responde.

### 5. Base legal: legítimo interesse, com prazo

Corrige o [ADR-0010](ADR-0010-resolucoes-produto-infraestrutura.md) §2, que citava o art. 7º,
**VI** — dispositivo que trata de *exercício regular de direitos em processo judicial,
administrativo ou arbitral*, e não de dado público.

O tratamento dos dados dos sócios do QSA — pessoas físicas que **não** são agentes públicos —
é amparado pelo **legítimo interesse (art. 7º, IX)**, com finalidade de prevenção à fraude e
controle social. **Prazo de resposta ao titular: 15 dias úteis.**

A LAI continua no raciocínio, mas no lugar certo: como fundamento do interesse público dentro
da avaliação de legítimo interesse, não como base legal em si — ela ampara **obter** a
informação, não **tratar** dado pessoal.

Consequência: a avaliação de legítimo interesse precisa ficar **documentada**, porque a ANPD
pode solicitá-la quando a base é o art. 7º, IX.

### 6. A decisão sobre o PEP é do comprador

A API corporativa Lotustech leva disclaimer contratual transferindo **integralmente à
instituição financeira** a decisão final de negar ou não relacionamento. O Explanando fornece
**índice indicativo e sinalização de risco**, nunca veredito.

É a contrapartida da exigência que a [questão 4](questoes-abertas-de-negocio.md) levantou:
vender índice a instituição regulada sem divulgar as lacunas de cobertura é afirmar uma
cobertura que não existe. O contrato transfere a decisão; os `MetadadosDeCobertura`, que a API
já expõe, dão ao comprador com o que decidir.

### 7. A infraestrutura está escolhida

**Vercel** (frontend) · **Cloud Run** (API e worker) · **Neon** (PostgreSQL) · **Cloudflare**
(CDN e WAF). Confirma o que o documento de Infraestrutura previa.

## O que este ADR **não** decide

**Se a opacidade da fonte deve elevar o índice de risco do PEP.** A proposta apareceu junto com
a decisão §1 e ficou de fora por conflito direto com o [ADR-0007](ADR-0007-postura-permissiva-do-motor.md):

Hoje `IndiceDeRisco` é peso das flags dividido por despesas avaliadas, e cobertura não entra.
Se a ausência de trilha de auditoria passasse a somar no índice, **um senador teria índice
maior que um deputado de comportamento idêntico** — porque o Senado não publica comprovante.
Isso é sinal sobre a instituição, atribuído a uma pessoa nomeada numa página indexada.

A alternativa que entrega o mesmo valor ao comprador sem inverter a invariante é expor a
opacidade como **dimensão própria ao lado do índice**, que é o que `MetadadosDeCobertura` já
faz. O comprador vê "índice 3,2 · 8 de 12 regras aplicáveis · a fonte não publica comprovante"
e pondera como sua metodologia mandar.

**Fica em aberto, para decisão explícita.**

## Consequências

- A página de **Política de Privacidade** passa a ser condição de publicação, com a base legal,
  o prazo de 15 dias úteis e o Encarregado nomeado — este último já no rodapé desde a Etapa 06.
- A **avaliação de legítimo interesse documentada** entra como pendência própria.
- A Camada 2 ganha requisito de entrega privada: e-mail ou área logada, o que traz o **Resend**
  e a autenticação para dentro do caminho crítico dela.
- O OTP exige armazenamento de código com expiração e tentativa limitada — um `enum` a mais no
  domínio de usuários e uma tabela.
- **Nada aqui dispensa revisão jurídica antes de publicar.** As decisões estão tomadas e serão
  implementadas como decididas; o parecer confirma que a redação publicada as reflete.

---

## Adendo — 2026-09-26: a finalidade declarada

**Decisão do Gustavo, por recomendação da segunda auditoria (Etapa 16):** a finalidade do
legítimo interesse passa de "prevenção à fraude e controle social" para **"transparência e
controle social"** do uso de recursos públicos.

- **A base legal não muda:** continua o art. 7º, IX.
- **O motivo de a expressão antiga ter entrado não se sustentou.** Ela foi escolhida como "o
  termo da LGPD para o legítimo interesse". Conferido no texto da lei (planalto.gov.br, em
  2026-09-26): "prevenção à fraude" só aparece no **art. 11, II, "g"**, que trata de dado
  **sensível** em processos de identificação e autenticação de cadastro. O art. 7º, IX e o
  art. 10, que regem o legítimo interesse, não a usam.
- **E ela colidia com o ADR-0004:** o produto tira "fraude" de toda a interface, e a política
  declarava que o tratamento existe para prevenir fraude — é juízo implícito sobre quem aparece.

A revisão jurídica da política, já pendente, confirma a redação.

