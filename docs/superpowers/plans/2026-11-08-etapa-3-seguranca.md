# Etapa 3 — Segurança e privacidade: levantamento e plano

> Roda sobre o código já mesclado (etapas 1 e 2, 08/11/2026). Suíte verde no ponto de partida:
> backend 1.110 testes (Domain 645, Application 134, Architecture 10, Integration 226, E2E 95),
> frontend 344 testes, lint limpo.

## O que o levantamento barato já encontrou

| # | Achado | Evidência | Gravidade | Correção proposta |
|---|---|---|---|---|
| S1 | **Next.js 16.3.6 vulnerável** | `npm audit`: 10 avisos, entre eles 2 RCE sem autenticação (otimização de imagem com SVG, GHSA-2464-fhr4-w9xw; `use cache` com typed arrays, GHSA-673q-mjm6-r5xr), SSRF na otimização de imagem e envenenamento de cache SSG/ISR | **Crítica** | Subir para `next@16.4.5` (sem mudança de major), rodar testes, lint e build |
| S2 | `sharp` < 0.35.5 (librsvg, CVE-2026-96889) | `npm audit` | Alta | Vem junto com S1 ou por `overrides` |
| S3 | `source-map-js` 1.2.1 (DoS) | `npm audit`; dependência de build (postcss, tailwind) | Alta, sem exposição em execução | `overrides` para `^1.2.2` |
| S4 | Pacotes .NET | `dotnet list package --vulnerable --include-transitive` | Nenhum | — |
| S5 | gitleaks: 6 alertas `generic-api-key` | Todos são `_authenticator=…` em `docs/contratos/planalto-*.html`: token anti-CSRF anônimo do Plone, gravado na página pública do gov.br | Falso positivo | Nada no código; a Etapa 6 apaga os snapshots `planalto-*.html` do histórico, porque também carregam ~78 e-mails de servidores |
| S6 | **CPF real em fixture** | `docs/contratos/camara-deputado-detalhe.json` (1), `camara-despesas-pagina-100.json` (3), `docs/contratos/README.md` (1) | Alta (privacidade) | Trocar por CPF sintético com dígito válido; a Etapa 6 tira do histórico |
| S7 | CPFs nos testes | `529.982.247-25`, `111.222.333-96` etc.: sintéticos conhecidos. `123.456.789-09` (`CpfParcialTests.cs`) foi montado para casar com uma máscara do TSE (`MascaraDoSocio`) | Baixa, a confirmar | Conferir se a máscara veio de pessoa real; se veio, trocar a máscara e o CPF por sintéticos |
| S8 | E-mails | Funcionais públicos (`dep.…@camara.leg.br`, senado), 78 em `planalto-vice-quem-e-quem.html`, e o do próprio projeto (`privacidade@`, `alertas@explanando.com.br`) | Baixa | Snapshots do Planalto saem (S5). E-mails funcionais de parlamentar ficam, porque são dado público de cargo |
| S9 | Nome de sócio no QSA | `docs/contratos/brasilapi-cnpj.json` (plano mestre) | Média | Nome fictício |
| S10 | Imagens Docker por tag móvel | `node:22-alpine`, `postgres:17-alpine`, `dotnet/aspnet:10.0-noble-chiseled-extra` | Baixa (sem deploy) | Fixar versão de patch nas três; digest é exagero para um repo que não vai ao ar |

## O que falta (precisa do seu aval)

1. **Varredura de código multiagente (`claude-security:scan`).** Lê o backend e o frontend
   inteiros em paralelo e confere cada achado com verificadores independentes. É a parte cara:
   **dezenas de agentes e muitos tokens**. Alternativa mais barata: `security-review` mais a
   disciplina do `verified-security-sweep` em zonas escolhidas (endpoints públicos, rate
   limit, `ChaveInterna`, renderização de dado externo no frontend, SSRF no `retratoSeguro`),
   com dois ou três revisores. **Recomendo a alternativa:** o site não vai ao ar, e o que vai
   a público é o código. O risco real é alguém subir a stack com uma falha de RCE, que é o S1.
2. **Correções**, uma por commit e com teste quando houver comportamento (ADR-0005):
   S1 a S3, depois S6, S7 e S9, depois S10.
3. **Teste de CI contra regressão de dado pessoal:** um teste (no `Explanando.Architecture.Tests`
   ou num script do CI) que percorre `git ls-files` e falha em CPF com dígito válido fora de uma
   lista curta de CPFs sintéticos permitidos.
4. A reescrita do histórico continua na Etapa 6 e só acontece com o seu "pode". Lista do que sai: os três
   JSON com CPF, `brasilapi-cnpj.json` antigo e os `planalto-*.html`.

## Varredura multiagente (08/11): interrompida

Interrompida a pedido, por custo de tokens: os 39 pesquisadores terminaram e trouxeram 6
candidatos, mas o painel de verificação não rodou, e nenhum candidato foi confirmado. O único
recuperado do registro:

- S11 (não verificado): a rota com `codigo` 0 divide a chave de cache com a série de pessoal do
  Estado inteiro, o que permitiria substituir a resposta global de `/estado/pessoal` por 6 horas
  (`ConsultasDoEstadoComCache.cs`). Conferir à mão antes de corrigir.

## Resultado (08/11)

A varredura multiagente foi abandonada por custo. O resto foi feito assim:

| Achado | Status | Commit |
|---|---|---|
| S1–S3 | Next 16.4.5 + `npm audit fix`; `npm audit --omit=dev` = 0. Restam 8 moderados de `minimatch`, só no lint | `d5e1785` |
| S4, S5 | Nada a fazer | — |
| S6, S7, S9 | CPFs reais → sintéticos; sócios do QSA com nome fictício; a máscara do sócio real (`CpfParcialTests`) trocada | `64142c4` |
| S8 | E-mails funcionais de parlamentar ficam; os do Planalto saem com os snapshots na Etapa 6 | Etapa 6 |
| S10 | Postgres fixado em 17.11-alpine. Node e .NET ficam na tag de major (sem deploy) | `62e7406` |
| S11 | Confirmado e corrigido com teste: chave própria `estado:serie:todos` | `1462516` |
| Regressão | `SemCpfRealVersionadoTests` (Architecture.Tests) | `117198a` |

Suíte: backend 1.112, frontend 344, lint limpo, build sem API passa.

Para a Etapa 6 tirar do histórico: os valores antigos de `camara-deputado-detalhe.json`,
`camara-despesas-pagina-100.json`, `brasilapi-cnpj.json`, `docs/contratos/README.md`, os
testes que citavam a máscara e o nome do sócio, e os `planalto-*.html`.

## Pontos de aprovação

- Esta tabela e a escolha entre a varredura multiagente e a alternativa enxuta.
- Achado grave que a varredura de código trouxer, antes de corrigir.
