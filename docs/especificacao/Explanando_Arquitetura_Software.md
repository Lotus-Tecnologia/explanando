# Explanando - Documento de Arquitetura de Software e Padrões de Código

Este documento consolida a arquitetura técnica de software para o projeto **Explanando**, abrangendo tanto o ecossistema Frontend (React / Next.js) quanto o Backend (.NET / C#), com foco em resiliência, escalabilidade a custo zero, rastreabilidade ponta a ponta e tratamento de falhas.

---

## 1. Arquitetura Backend (.NET / C#)

O backend é dividido em dois principais componentes executados no Google Cloud Run: a **API Principal** (síncrona, reativa) e o **Worker Service** (assíncrono, processador de filas). Ambos seguem os princípios de **Clean Architecture** e **Domain-Driven Design (DDD)**.

### 1.1. Padrões de Arquitetura e Organização de Código
* **Domain Model & Entities:** Regras de negócio encapsuladas no domínio (ex: regras de validação de anomalias AML).
* **CQRS (Command Query Responsibility Segregation):** Separação estrita entre comandos de escrita/mutação e consultas de leitura, otimizando o uso do banco de dados (Neon.tech) e do cache (Upstash).
* **Pipeline de Validação (Chain of Responsibility):** O motor determinístico da Camada 1 é implementado usando o padrão *Chain of Responsibility* (ou *Pipeline Behaviors* via MediatR), onde cada regra (`REG_KYB_*`, `REG_NET_*`) atua como um elo independente.

### 1.2. Resiliência e Tratamento de Falhas (Integrações com Falha)
* **Polly (Transient Fault Handling):** Utilizado em todas as requisições HTTP externas (APIs da Câmara, Senado, BrasilAPI, Gemini). Implementa políticas de:
  * *Retry with Exponential Backoff:* Tentativas automáticas em caso de instabilidade temporária da API governamental.
  * *Circuit Breaker:* Interrompe requisições para uma API externa que esteja fora do ar, evitando esgotamento de recursos (*thread pool exhaustion*).
  * *Fallback:* Retorna dados em cache ou estado degradado seguro caso a fonte externa falhe permanentemente.
* **Dead Letter Queue (DLQ):** Configurada no AWS SQS. Se uma mensagem de varredura falhar repetidamente após o limite de *retries* do worker, ela é movida para uma DLQ para investigação manual, impedindo que trave o fluxo.

### 1.3. Rastreabilidade e Observabilidade
* **OpenTelemetry:** Instrumentação nativa em todo o ciclo de vida da aplicação .NET.
* **Distributed Tracing:** Cada clique em "Nova Varredura" gera um `TraceId` único que flui via gRPC e headers HTTP para o SQS, Worker e chamadas de LLM, permitindo rastrear o gargalo exato em ferramentas de APM.

---

## 2. Arquitetura Frontend (React / Next.js)

O frontend foca em performance extrema, SEO otimizado e manutenibilidade através de componentes limpos e reutilizáveis.

### 2.1. Padrões de Arquitetura e Organização de Código
* **Feature-First / Atomic Design:** Organização de pastas baseada em domínios de negócio (`/components/deputados`, `/components/ranking`, `/hooks`, `/services`, `/store`).
* **Server-Side Rendering (SSR) e Incremental Static Regeneration (ISR):** Páginas estáticas dos deputados são geradas em cache na CDN (Cloudflare) e revalidadas em background, garantindo carregamento instantâneo.
* **Custom Hooks:** Toda a lógica de interação com a API, chamadas assíncronas e gerenciamento de estado local é isolada em hooks customizados (ex: `useDeputadoSearch`, `useTriggerVarredura`).

### 2.2. Tratamento de Erros e Estados na UI
* **Error Boundaries:** Capturam falhas de renderização em componentes específicos, exibindo uma interface de fallback amigável sem quebrar a aplicação inteira.
* **Optimistic Updates & SWR/TanStack Query:** Para ações como favoritar deputados ou disparar varreduras, o front aplica atualizações otimistas na tela e gerencia *re-fetches* automáticos em caso de perda de conexão.

---

## 3. Gestão de Pacotes e Bibliotecas Recomendadas

### 3.1. Ecossistema .NET (Backend)
* `MediatR` / `FluentValidation`: Para desacoplamento de comandos e validação de contratos de entrada.
* `Polly`: Para políticas de resiliência HTTP.
* `OpenTelemetry.Extensions.Hosting`: Para exportação de métricas e traces.
* `AWSSDK.SQS`: Para consumo e publicação na fila de varreduras.
* `StackExchange.Redis`: Para integração otimizada com o Upstash Redis.

### 3.2. Ecossistema React (Frontend)
* `Next.js`: Framework principal com SSR/ISR nativo.
* `Tailwind CSS`: Para estilização rápida, componentizada e leve.
* `TanStack Query (React Query)`: Para gerenciamento de estado de servidor, cache de requisições e sincronização de dados.
* `Zustand`: Para gerenciamento de estado global leve (ex: dados do usuário logado, preferências de filtro).
* `Lucide React`: Biblioteca de ícones leve e modular.
