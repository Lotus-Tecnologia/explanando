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

**Etapas 00 a 04 concluídas** — fundação, contratos reais das APIs, domínio, persistência,
ingestão e motor de regras. **318 testes verdes** (256 unitários, 52 de integração e ponta a
ponta contra Postgres real, 10 de arquitetura), build com 0 avisos.

A coleta funciona da API ao banco, de forma idempotente e com checkpoint, e **11 das 16
regras determinísticas estão implementadas** — com postura permissiva: na dúvida, o motor se
cala. Ainda não há API pública nem interface.

CI no GitHub Actions roda build, suíte completa e verificação de migrations a cada push.

Os contratos reais das APIs estão em [`docs/contratos/`](docs/contratos/README.md); o andamento
detalhado de cada etapa, em [`docs/registro/`](docs/registro/README.md).

## Como rodar

Pré-requisitos: .NET SDK 10, Docker Desktop, Node 22+ (só a partir da Etapa 6).

```bash
docker compose up -d
```

```bash
dotnet test backend/Explanando.slnx
```

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

- [Visão e escopo](docs/especificacao/Explanando_Visao_Escopo_V1.md)
- [Regras de negócio e proteção jurídica](docs/especificacao/Explanando_Negocios_V1_Complementar.md)
- [Motor de regras determinísticas](docs/especificacao/Explanando_Motor_Regras_V1.md)
- [Integrações e endpoints](docs/especificacao/integracoes.md)
- [Decisões arquiteturais (ADRs)](docs/decisoes/README.md)
- [Registro de execução](docs/registro/README.md)
