# ADR-0008 — O repositório permanece privado

**Data:** 2026-08-19 · **Situação:** Aceita

## Contexto

O repositório contém a lógica de detecção de inconsistências, as tabelas curadas que a
calibram e fixtures com dado real de fornecedores identificados por CNPJ.

Abrir o código de um projeto de transparência tem apelo óbvio. Mas aqui ele traz dois riscos
concretos: expõe exatamente quais padrões o motor procura, para quem tenha interesse em
evitá-los; e um repositório aberto por engano é praticamente impossível de fechar de volta,
porque forks, caches de busca e espelhos sobrevivem ao fechamento.

## Decisão

**O repositório é privado e permanece privado.** A decisão de abri-lo, se um dia for tomada,
é deliberada e não pode acontecer por descuido de configuração.

Para que não aconteça por descuido, o CI tem um job dedicado — `visibilidade` — que **falha o
pipeline** se `github.event.repository.private` deixar de ser verdadeiro. Não impede a
mudança, mas garante que ela apareça em vermelho no próximo push em vez de passar despercebida.

## Consequências

- Minutos de GitHub Actions passam a ser consumidos da cota (repositórios privados não têm
  Actions gratuito ilimitado). Por isso o pipeline ignora mudanças que tocam apenas `docs/`
  e arquivos markdown.
- Se o projeto for aberto no futuro, este ADR precisa ser substituído por outro, e o job
  `visibilidade` removido no mesmo commit — deixando o registro de que foi decisão, não
  acidente.
