# ADR-0003 — Monorepo, com git desde o commit inicial

**Data:** 2026-08-18 · **Situação:** Aceita

## Contexto

O projeto tem backend .NET, frontend Next.js, um worker e um corpo grande de documentação que
se referenciam mutuamente. Um ADR que decide o frontend precisa viver ao lado do código do
frontend; o registro de execução precisa acompanhar o commit que ele descreve.

Há também um histórico concreto: na auditoria da própria pasta de projetos, "projeto grande
sem controle de versão" apareceu como o maior risco identificado — um projeto com 945 arquivos
e 180 documentos de design existia sem um único commit.

## Decisão

Um **monorepo** com `backend/`, `frontend/` e `docs/`, e `git init` como parte da Etapa 0,
antes de qualquer linha de código de produção.

## Alternativas descartadas

**Repositórios separados** para front, back e workers — dá deploy mais isolado e permissões
mais granulares, o que importa quando há times distintos. Não é o caso aqui, e o custo de
manter versões de contrato sincronizadas entre repos em fase inicial é alto.

**Adiar o git** até "ter algo pronto" — é exatamente o caminho que produziu o risco descrito
no contexto.

## Consequências

- Um commit pode atravessar camadas e trazer junto o ADR e o registro que o explicam.
- CI precisa filtrar por caminho para não rodar a suíte inteira a cada mudança de documento.
- Falta ainda um remote privado no GitHub: o repositório local protege o histórico, mas não
  protege contra perda da máquina.
