# Etapa 00 — Fundação e git

**Data:** 2026-08-18 · **Situação:** ✅ Concluída

---

## 1. Objetivo

Sair de uma pasta com apenas especificações soltas para um repositório versionado, com a
solution .NET montada em camadas, a infraestrutura local descrita e a direção das dependências
já protegida por teste — antes de qualquer código de produção existir.

## 2. Definido

Antes da etapa, quatro contradições entre os documentos de especificação foram resolvidas.
Cada uma virou um ADR:

| Conflito encontrado | Resolução | ADR |
|---|---|---|
| Visão diz Blazor; outros 4 documentos dizem Next.js | Next.js | [0001](../decisoes/ADR-0001-frontend-nextjs.md) |
| Plano tem 7 fases, nada visível até o fim | V1 = fatia vertical só da Câmara | [0002](../decisoes/ADR-0002-escopo-v1-fatia-vertical.md) |
| Monorepo ou repos separados, em aberto no plano | Monorepo, git no commit inicial | [0003](../decisoes/ADR-0003-monorepo-com-git.md) |
| "Top Mamadores" e "Compartilhar Denúncia" violam a §1 de Negócios | Vocabulário técnico em tudo | [0004](../decisoes/ADR-0004-nomenclatura-tecnica.md) |

Definido dentro da etapa:

| Decisão | ADR |
|---|---|
| TDD obrigatório, com quatro níveis de teste | [0005](../decisoes/ADR-0005-tdd-e-niveis-de-teste.md) |
| Direção das dependências verificada por teste, lendo os `.csproj` | [0006](../decisoes/ADR-0006-arquitetura-verificada-por-teste.md) |
| Registro de execução por etapa, em `docs/registro/` | este arquivo |

Escolhas menores, sem ADR próprio:

- **Shouldly** como biblioteca de asserção, no lugar de FluentAssertions — evita a mudança de
  licenciamento das versões recentes do FluentAssertions.
- **Central Package Management** (`Directory.Packages.props`): versão de pacote existe num
  lugar só.
- **`TreatWarningsAsErrors`** ligado na solution inteira, via `Directory.Build.props`.
- **Collation `C`** no Postgres local: o motor compara texto (CNAE, razão social) e não pode
  mudar de resultado conforme o locale da máquina.

## 3. Executado

### Reorganização do repositório

Os 12 arquivos que estavam soltos na raiz foram para `docs/especificacao/`. Os três PDFs
tinham nome de exportação automática (`Cloudflare CDN & WAF-2026-08-19-*.pdf`) e foram
renomeados pelo conteúdo real, após extração e inspeção:

| Nome anterior | Nome atual | Conteúdo |
|---|---|---|
| `...-001320.pdf` | `Diagrama_Arquitetura_Macro.pdf` | Fluxo edge → API → SQS → Worker → Neon |
| `...-001411.pdf` | `Diagrama_ER_Banco.pdf` | Diagrama entidade-relacionamento |
| `...-001443.pdf` | `Diagrama_Business_Model_Canvas.pdf` | Mindmap do modelo de negócio |

`git init` na branch `main`, com `.gitignore` e `.gitattributes` (`eol=lf`, `autocrlf=false`).

### Solution .NET

SDK **10.0.302**. Formato de solution novo: `Explanando.slnx`.

```
backend/
├─ Explanando.slnx
├─ Directory.Build.props        net10.0, nullable, warnings-as-errors
├─ Directory.Packages.props     versões centralizadas
├─ .editorconfig                convenções + supressões só em tests/
├─ src/
│  ├─ Explanando.Domain          zero projetos, zero pacotes
│  ├─ Explanando.Application     → Domain
│  ├─ Explanando.Infrastructure  → Application
│  ├─ Explanando.Api             → Application, Infrastructure
│  └─ Explanando.Worker          → Application, Infrastructure
└─ tests/
   ├─ Explanando.Domain.Tests
   ├─ Explanando.Application.Tests
   ├─ Explanando.Integration.Tests
   ├─ Explanando.E2E.Tests
   └─ Explanando.Architecture.Tests
```

Os quatro primeiros projetos de teste estão vazios de propósito: recebem conteúdo nas etapas
em que houver o que testar. `Architecture.Tests` já está cheio.

### Infraestrutura local

`docker-compose.yml` com os equivalentes locais dos serviços gerenciados do V1:

| Serviço gerenciado | Equivalente local |
|---|---|
| Neon.tech (Postgres) | `postgres:17-alpine` |
| Upstash (Redis) | `redis:7-alpine` |
| AWS SQS | `localstack/localstack:3` |

`docker/localstack-init/01-criar-filas.sh` cria `explanando-varreduras` e
`explanando-varreduras-dlq`, com `maxReceiveCount: 5` na política de redrive — a mesma
topologia de Dead Letter Queue que o documento de Arquitetura (§1.2) especifica.

**Nenhuma conta na nuvem é necessária para desenvolver o V1.**

## 4. Testes

**10 testes de arquitetura, todos verdes. Build com 0 avisos e 0 erros.**

| Teste | Regra que defende |
|---|---|
| `Dominio_nao_referencia_nenhum_outro_projeto` | o domínio é o centro da cebola |
| `Dominio_nao_depende_de_nenhum_pacote_externo` | domínio é C# puro, só BCL |
| `Application_depende_somente_do_dominio` | casos de uso não conhecem banco nem HTTP |
| `Infrastructure_nao_e_referenciada_por_dominio_nem_por_application` | direção da seta |
| `Hosts_montam_application_e_infrastructure_sem_alcancar_o_dominio_direto` (×2) | composition root |
| `Nenhum_projeto_de_producao_referencia_projeto_de_teste` | teste não vaza para produção |
| `Todo_projeto_de_producao_vive_em_src_e_todo_teste_vive_em_tests` | layout legível |
| `Nenhum_csproj_declara_versao_de_pacote_inline` | CPM de verdade |
| `Todo_pacote_referenciado_tem_versao_central_declarada` | CPM de verdade |

### O teste foi visto vermelho antes de ser aceito

Conforme o [ADR-0005](../decisoes/ADR-0005-tdd-e-niveis-de-teste.md), um teste que nunca falhou
não provou que testa alguma coisa. Um `PackageReference` para `Microsoft.Extensions.Hosting` foi
adicionado de propósito ao `Explanando.Domain`. Resultado:

```
Com falha  RegrasDeDependenciaTests.Dominio_nao_depende_de_nenhum_pacote_externo
  Shouldly.ShouldAssertException : dominio.PacotesReferenciados
    o dominio e' C# puro (BCL apenas). EF Core, MediatR e afins vivem na infraestrutura

Com falha: 1, Aprovado: 9, Total: 10
```

A violação foi revertida e a suíte voltou a 10/10. A regra pega o caso real e explica o porquê.

## 5. Descobertas

**Os três PDFs não continham informação nova.** Eram os diagramas Mermaid renderizados dos
mesmos conteúdos já presentes nos markdowns. O terceiro é composto só de imagens, sem camada de
texto — foi preciso extrair os XObjects e recompor a página para confirmar que era o mindmap do
Business Model Canvas.

**O `TreatWarningsAsErrors` reprovou o boilerplate do próprio template do .NET.** O `Worker.cs`
gerado pelo `dotnet new worker` viola CA1848 (usar delegates `LoggerMessage` em vez de
`LogInformation`) e CA1727 (PascalCase em placeholder de log). Foi reescrito com o gerador de
código `[LoggerMessage]` — que é o padrão certo para o worker de ingestão de qualquer forma,
já que ele vai logar em volume alto.

**Central Package Management conflita com os templates.** O `dotnet new` escreve `Version=`
inline em cada `PackageReference`, o que dispara NU1008 sob CPM. Todos os `.csproj` foram
reescritos à mão, sem versão inline — e essa regra virou teste
(`Nenhum_csproj_declara_versao_de_pacote_inline`) para não voltar.

**Os analisadores reprovam `snake_case` em nome de teste (CA1707).** Mas o nome do método de
teste *é* a frase que descreve a regra, e legibilidade do relatório vale mais que a convenção
de nomes da BCL. CA1707 foi desligado com escopo de caminho (`[tests/**/*.cs]`) no
`.editorconfig`, junto de CA1515, CA2007 e CA1861. A supressão não alcança `src/`.

**Docker Desktop não estava em execução** no momento da etapa. O `docker-compose.yml` teve a
sintaxe validada com `docker compose config` (que não precisa do daemon), mas os contêineres
**ainda não subiram nenhuma vez**. Confirmar na Etapa 02, que é quando o Postgres passa a ser
necessário de fato.

## 6. Pendências

| Pendência | Impacto | Quando resolver |
|---|---|---|
| Repositório sem remote — o histórico só existe nesta máquina | Alto | Assim que houver repositório privado no GitHub |
| `docker compose up` nunca executado de fato | Médio | Etapa 02, quando o Postgres for necessário |
| CI (GitHub Actions) rodando build + testes | Médio | Junto com o remote |
| Teste automatizado varrendo termos proibidos pelo ADR-0004 | Baixo | Quando existir texto voltado ao usuário |
| `Domain.Tests`, `Application.Tests`, `Integration.Tests` e `E2E.Tests` vazios | Nenhum | São preenchidos nas etapas 02 a 05 |

## 7. Próxima etapa

**Etapa 01 — Spike de contratos das APIs.**

Bater nas APIs reais da Câmara dos Deputados e da BrasilAPI, salvar as respostas em
`docs/contratos/` e derivar os DTOs **do JSON real, não da documentação**.

A razão é concreta: a análise das especificações apontou que o motor de regras pede campos que
o mapeamento EF Core não modela — número da nota fiscal (`REG_KYB_005`), UF do gasto
(`REG_GEO_001/002/003`), CNAEs secundários e situação cadastral (`REG_KYB_001/002/004`). Antes
de modelar o domínio na Etapa 02, é preciso saber **quais desses campos as APIs realmente
entregam**. Modelar em cima de suposição custaria uma migração depois.

O spike também precisa medir o comportamento de rate-limit da Câmara, que decide a estratégia
de throttling e checkpoint da Etapa 03.
