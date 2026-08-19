# ADR-0006 — Camadas verificadas por teste, não por revisão

**Data:** 2026-08-18 · **Situação:** Aceita

## Contexto

Clean Architecture só entrega o que promete enquanto a direção das dependências for respeitada.
Na prática ela erode devagar: alguém precisa de um `DbContext` dentro de um caso de uso "só
nesse ponto", e seis meses depois o domínio não compila sem o EF Core.

Revisão de código não segura isso de forma confiável, porque a violação sempre chega justificada
e pontual.

## Decisão

A direção das dependências é **verificada por teste automatizado**, em
`Explanando.Architecture.Tests`. As regras:

1. `Domain` não referencia projeto nenhum.
2. `Domain` não referencia pacote nenhum além da BCL.
3. `Application` referencia apenas `Domain`.
4. `Infrastructure` não é referenciada por `Domain` nem por `Application`.
5. `Api` e `Worker` referenciam apenas `Application` e `Infrastructure`.
6. Nenhum projeto de produção referencia projeto de teste.
7. Projeto de produção vive em `src/`, projeto de teste vive em `tests/`.

Mais duas sobre versionamento: nenhum `.csproj` declara versão de pacote inline, e todo pacote
referenciado tem `PackageVersion` correspondente em `Directory.Packages.props`.

Os testes leem os arquivos **`.csproj` em disco**, não os assemblies compilados. É proposital:
uma `ProjectReference` declarada mas ainda não usada em código some do assembly compilado e
passaria despercebida numa checagem por reflexão. Lida do `.csproj`, ela é pega no ato da
declaração.

## Alternativas descartadas

**NetArchTest / ArchUnitNET** — mais expressivos para regras sobre tipos e namespaces, e
provavelmente entram depois para regras do tipo "entidade de domínio não expõe setter público".
Não foram usados aqui porque a regra desta camada é sobre o **grafo de projetos**, que se lê
melhor no `.csproj` do que por reflexão, e a implementação direta não adiciona dependência.

**Confiar na revisão de código** — descrito no contexto: falha devagar e silenciosamente.

## Consequências

- Violar a arquitetura quebra o build, com mensagem explicando o porquê da regra.
- A regra foi validada ao contrário: um pacote externo foi adicionado ao `Domain` de propósito,
  o teste ficou vermelho na assertiva certa, e a violação foi revertida.
- Se uma regra precisar mudar, muda no teste — e a mudança fica registrada no diff, que é onde
  uma decisão de arquitetura deve aparecer.
