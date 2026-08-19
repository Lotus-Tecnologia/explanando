# ADR-0001 — Frontend em Next.js, não Blazor

**Data:** 2026-08-18 · **Situação:** Aceita

## Contexto

As especificações se contradiziam. O documento de Visão e Escopo (§4) define o frontend como
**Blazor (WebAssembly ou Server)**. Já o documento de Arquitetura de Software (§2), o de
Infraestrutura (§1), a Fase 1 do Plano de Execução e o Business Model Canvas todos definem
**React / Next.js**. Quatro documentos contra um.

O Business Model Canvas lista "Orgânico: páginas indexadas no Google" como canal de aquisição
principal, ao lado do compartilhamento viral. Ou seja: SEO não é um detalhe de implementação
neste produto, é o motor de crescimento.

## Decisão

O frontend é **Next.js (App Router) + React + TypeScript + Tailwind**.
O trecho do documento de Visão e Escopo que menciona Blazor fica revogado por este ADR.

## Alternativas descartadas

**Blazor WebAssembly** — manteria tudo em C#, um só ecossistema e uma só linguagem no time.
Descartado porque exigiria resolver pré-renderização por fora para o Google indexar as páginas
dos parlamentares, que é exatamente o ativo de SEO do produto, e porque o payload inicial de
runtime penaliza o primeiro carregamento no celular — o dispositivo da maior parte do tráfego
vindo de rede social.

**Blazor Server** — resolveria SEO, mas amarra cada visitante a uma conexão SignalR persistente
no servidor. Isso é frontalmente incompatível com o "scale to zero" do Cloud Run e com a
premissa de custo fixo zero: um pico viral viraria conta de servidor, não cache de CDN.

## Consequências

- Renderização estática com revalidação (ISR) nas páginas de parlamentar, servidas pela CDN
  do Cloudflare. É a CDN, e não o Redis, que absorve o pico viral.
- O projeto passa a ter duas stacks (.NET e Node) — mais superfície de CI, mas ambas já
  dominadas.
- A comunicação entre front e back é uma API HTTP explícita, o que também é o que o produto
  precisa vender depois no B2B.
