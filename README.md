# Explanando

Plataforma de utilidade pública que audita, simplifica e expõe os gastos de deputados e senadores
a partir de dados de transparência pública. Um motor de regras determinísticas cruza despesas da
cota parlamentar com dados cadastrais de fornecedores e sinaliza **inconsistências financeiras**
para leitura do cidadão.

> **Postura do produto:** o sistema nunca acusa ninguém de crime. Ele reporta achados sistêmicos
> de um motor de regras e deixa a conclusão para o leitor ou para a autoridade competente.
> Ver [ADR-0004](docs/decisoes/ADR-0004-nomenclatura-tecnica.md).

---

## Estado atual

**Etapas 00 a 06 concluídas; a 07 — observabilidade e deploy — quase.** Fundação, contratos
reais das APIs, domínio, persistência, ingestão, motor de regras, calibragem, coleta completa
da legislatura, API pública de leitura e interface em Next.js.

**701 testes no backend** (428 de domínio, 54 de aplicação, 167 de integração, 42 de ponta a
ponta e 10 de arquitetura) e **113 no frontend**, build com 0 avisos. CI verde nos quatro jobs.

### O que já está no ar

| | Onde | Situação |
|---|---|---|
| **Banco** | Neon (PostgreSQL) | ✅ completo — 513 parlamentares, 713.383 despesas, 21.078 indicadores |
| **Frontend** | Cloudflare Workers | ⚠️ publicado, e **degradado de propósito** |
| **API** | — | ❌ ainda só local |
| **Domínio** | `explanando.com.br` | comprado; ainda não aponta para nada |

A base de produção está verificada: **R$ 803.468.145,38** em despesas, taxa de sinalização de
**2,955%** — contra 2,96% da calibragem da Etapa 04b, sobre outra amostra e outro banco.

O frontend está degradado porque `api.explanando.com.br` ainda não existe: o build caiu no
caminho medido e publicou **9 páginas em vez de 522**. É o comportamento correto — o código
preferiu degradar a quebrar — e a **indexação está trancada** (`NEXT_PUBLIC_INDEXAVEL` ausente
⇒ `Disallow: /`) para o buscador não conhecer o site nesse estado. Os passos que faltam, na
ordem que importa, estão em [docs/desdobramento.md](docs/desdobramento.md).

### O motor

**12 regras determinísticas implementadas** — 11 que avaliam um parlamentar e 1 de rede, que
cruza parlamentares entre si. Quatro ficaram de fora **por falta de fonte, não por escolha**:
`REG_NET_001` (depende do TSE), `REG_NET_002` (a fonte responde 405), `REG_NET_003` (só o
Senado publica endereço de escritório) e `REG_GEO_001` (a despesa não traz local nem hora).

A postura é permissiva ([ADR-0007](docs/decisoes/ADR-0007-postura-permissiva-do-motor.md)): na
dúvida, o motor se cala. E **zero por falta de dado nunca é apresentado como zero por nada
encontrado** — cada regra declara de qual dado externo depende, e a interface consulta a
cobertura **antes de desenhar**, nunca a contagem de indicadores.

São **dois rankings independentes**
([ADR-0009](docs/decisoes/ADR-0009-dois-numeros-nao-um.md)): volume de gastos em reais para o
público, e densidade de indicadores por 100 despesas para compliance — esta com piso amostral
de 100 despesas, abaixo do qual o índice vem **nulo, não zero**. Uma terceira ordenação, por
gravidade, olha só os achados de peso alto: pelo total, quem tem 14 achados graves aparece
abaixo de quem tem zero.

> **Cobertura da V1:** a base cobre a **Câmara dos Deputados**. O Senado tem contratos,
> conversor e testes prontos, mas a ingestão ainda não rodou — e a assimetria entre as duas
> casas está decidida no [ADR-0012](docs/decisoes/ADR-0012-assimetria-camara-senado.md).

Os contratos reais das APIs estão em [`docs/contratos/`](docs/contratos/README.md); o andamento
detalhado de cada etapa, em [`docs/registro/`](docs/registro/README.md); e o estado corrente
para quem está retomando, em [`estado_atual_projeto.md`](estado_atual_projeto.md).

## Como rodar

Pré-requisitos: .NET SDK 10, Docker Desktop, Node 22+.

**Banco local e suíte do backend.** Os três contêineres precisam estar de pé: os testes de
integração usam Postgres, Redis e LocalStack de verdade, não dublês.

```bash
docker compose up -d
```

```bash
dotnet test backend/Explanando.slnx
```

**Frontend.**

```bash
cd frontend && npm ci && npm test
```

**Contra a base de produção na Neon.** A string de conexão fica em `backend/.env.neon`, que não
vai para o git. A API sobe na 5000 e o worker aceita `coletar`, `varrer`, `consolidar` e
`ciclo-mensal`.

```bash
cd backend && EXPLANANDO_CONEXAO="$(tr -d '\r\n' < .env.neon)" dotnet run --project src/Explanando.Api
```

> **A API não aplica migrations, de propósito** — API de leitura não mexe em esquema. Quem cria
> as tabelas é o worker, em qualquer comando. Subir a API contra banco vazio dá HTTP 500.

> **O bundle do Cloudflare não sai do Windows.** `npm run cf:build` completa na primeira
> execução e pendura nas seguintes. Use WSL, ou deixe o CI publicar.

## Layout do repositório

```
explanando/
├─ backend/                      solution .NET 10
│  ├─ src/
│  │  ├─ Explanando.Domain           entidades e motor de regras (C# puro)
│  │  ├─ Explanando.Application      casos de uso, CQRS
│  │  ├─ Explanando.Infrastructure   EF Core, clientes HTTP, cache, fila
│  │  ├─ Explanando.Api              API pública
│  │  └─ Explanando.Worker           ingestão e varredura assíncrona
│  └─ tests/
│     ├─ Explanando.Domain.Tests        unitários do domínio
│     ├─ Explanando.Application.Tests   unitários dos casos de uso
│     ├─ Explanando.Integration.Tests   banco, HTTP e fila reais
│     ├─ Explanando.E2E.Tests           fluxo ponta a ponta
│     └─ Explanando.Architecture.Tests  direção das dependências
├─ frontend/                     Next.js (Etapa 6)
├─ docs/
│  ├─ especificacao/             documentos originais de visão, negócio e regras
│  ├─ decisoes/                  ADRs — o que foi decidido e por quê
│  ├─ contratos/                 respostas reais das APIs externas, congeladas
│  └─ registro/                  o que foi planejado e executado, por etapa
└─ docker-compose.yml            Postgres + Redis + LocalStack (SQS)
```

## Arquitetura

Clean Architecture com a direção das dependências **verificada por teste**, não por revisão
de código: `Explanando.Architecture.Tests` lê os `.csproj` e quebra o build se alguém apontar
uma seta para o lado errado.

```
Domain  ←  Application  ←  Infrastructure
              ↑                  ↑
              └───── Api ────────┤
              └──── Worker ──────┘
```

O `Domain` não referencia projeto nenhum e não usa pacote nenhum além da BCL.
Ver [ADR-0006](docs/decisoes/ADR-0006-arquitetura-verificada-por-teste.md).

## Testes

TDD é obrigatório: o teste vem antes da implementação, em todas as etapas.
Ver [ADR-0005](docs/decisoes/ADR-0005-tdd-e-niveis-de-teste.md).

| Nível | Projeto | Prova que |
|---|---|---|
| Unitário | `Domain.Tests`, `Application.Tests` | a regra de negócio está correta |
| Integração | `Integration.Tests` | banco, HTTP e fila realmente funcionam |
| E2E | `E2E.Tests` | o fluxo completo entrega o resultado |
| Arquitetura | `Architecture.Tests` | as camadas continuam separadas |

## Documentos

> **Quando um ADR contradiz `docs/especificacao/`, o ADR vence.** Os documentos de
> especificação são o ponto de partida do projeto e foram deliberadamente mantidos como
> estavam: valem como registro da intenção original, não como descrição do que existe. O
> documento de visão, por exemplo, ainda descreve Blazor, gRPC e RabbitMQ — a stack foi
> decidida de outro jeito no [ADR-0001](docs/decisoes/ADR-0001-frontend-nextjs.md) e no
> [ADR-0015](docs/decisoes/ADR-0015-frontend-no-cloudflare.md).

- [Visão e escopo](docs/especificacao/Explanando_Visao_Escopo_V1.md)
- [Regras de negócio e proteção jurídica](docs/especificacao/Explanando_Negocios_V1_Complementar.md)
- [Motor de regras determinísticas](docs/especificacao/Explanando_Motor_Regras_V1.md)
- [Integrações e endpoints](docs/especificacao/integracoes.md)
- [Decisões arquiteturais (ADRs)](docs/decisoes/README.md)
- [Registro de execução](docs/registro/README.md)
- [Desdobramento — contas, segredos e a ordem do deploy](docs/desdobramento.md)
- [Estado atual, para quem está retomando](estado_atual_projeto.md)
