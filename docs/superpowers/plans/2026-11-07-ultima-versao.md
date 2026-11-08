# A última versão do Explanando — plano mestre

> **Para quem executa:** cada etapa abaixo abre com uma **fase de levantamento** que produz o
> plano detalhado dela (tarefas com teste, no formato da skill `superpowers:writing-plans`).
> Nenhuma etapa começa a mudar código antes de o Gustavo aprovar esse plano detalhado.

**Objetivo:** encerrar o Explanando com uma versão final aberta, bonita, correta e segura, que
sirva de portfólio da Lotus Tech e do site pessoal do Gustavo. O site não vai ao ar: tudo roda
localmente, e o repositório é o produto.

**Decidido em 07/11/2026:**
- frentes B2B e B2C encerradas;
- sem hospedagem (nem Oracle, nem outra). O domínio expira sozinho no ano que vem;
- escopo grande de propósito: redesign completo, regras revisadas, auditoria de segurança,
  prints e GIF, README e post-mortem;
- dois cuidados obrigatórios: **varrer o histórico do git** e **não publicar dado pessoal**.

---

## O que o levantamento já encontrou (07/11)

| Achado | Onde | Consequência |
|---|---|---|
| **CPF de pessoa física em fixture versionada** | `docs/contratos/camara-deputado-detalhe.json` (campo `cpf` do deputado), `camara-despesas-pagina-100.json` (`cnpjCpfFornecedor` de fornecedor pessoa física), citado também em `docs/contratos/README.md:132` | Precisa ser mascarado no arquivo **e no histórico** |
| Nome de sócio no QSA | `docs/contratos/brasilapi-cnpj.json` | Dado público da Receita, mas pessoal. Trocar por nome fictício na fixture |
| gitleaks: 6 alertas `generic-api-key` | `docs/contratos/planalto-*.html` (snapshots de páginas do gov.br) | Provável falso positivo (chave de analytics da própria página pública). Conferir na Etapa 3 |
| Nenhum `.env`, chave privada, token ou senha real no histórico | 121 commits, varredura por padrão + gitleaks | Bom sinal, a confirmar com a varredura completa da Etapa 3 |
| **CI falha se o repositório ficar público** | job `visibilidade` em `.github/workflows/ci.yml` (ADR-0008) | Remover o job e registrar um ADR que supera o 0008 |
| Documentos de estratégia comercial | `docs/especificacao/*Negocios*`, `*Lacunas_B2B*`, `Diagrama_Business_Model_Canvas.pdf`, `docs/decisoes/questoes-abertas-de-negocio.md`, `resposta das questões...txt` | **Decisão sua** (ver abaixo) |
| Banho de UI parado no meio | `docs/pendencias.md` §2: perfil no visual antigo, sem revelação progressiva nas despesas | Vira parte da Etapa 2 |
| 62 despesas rejeitadas por glosa inconsistente, nunca investigadas | `docs/pendencias.md` §4 | Entra na Etapa 1 |
| Stack local pronta | .NET 10.0.403, Node 24.18, Docker 29.9, `deploy/compose.local.yml` | Prints e GIF saem da máquina local, com dado real |

---

## Etapa 1 — Regras de AML, valores e bugs

**Levantamento (gera o plano detalhado):**
1. Ler as 12 regras (`backend/src/Explanando.Domain/Regras/*.cs`, ~2.200 linhas) contra a
   especificação (`docs/especificacao/Explanando_Motor_Regras_V1.md`) e os ADRs 0004, 0007 e
   0009. Para cada regra: o que o texto promete, o que o código faz, quais limiares de
   `ParametrosDoMotor.cs` usa e se os testes cobrem a borda.
2. Rodar o motor na base local e conferir números: contagem de flags por regra e por casa,
   valores somados das telas contra uma consulta SQL independente, consistência entre o
   ranking, o perfil e o card.
3. Investigar as 62 despesas rejeitadas por glosa.
4. Entregar uma **tabela de achados** (regra, problema, evidência, gravidade, correção
   proposta). Você aprova o que entra.

**Execução:** um bug por tarefa, sempre com teste que falha antes (ADR-0005). Mudança de regra
sobe a versão do motor e re-varre a base, registrando o antes e o depois das contagens, como
foi feito na versão 6.

**Skills:** `superpowers:systematic-debugging`, `superpowers:test-driven-development`,
`data:validate-data` (números contra SQL), `mattpocock-skills:code-review` e
`superpowers:verification-before-completion`.

## Etapa 2 — Redesign: design system, UI e UX

**Levantamento e direção (gera o plano detalhado):**
1. Inventário de telas e componentes (`frontend/src/app`, 47 componentes), com print do estado
   atual de cada tela, em claro e escuro, no desktop e no celular.
2. Crítica do estado atual: `design:design-critique`, `vectorlab-ux-skills:ux-audit` e
   `anti-slop`.
3. **Três direções visuais** como página de comparação (artifact), cada uma com paleta,
   tipografia e uma tela-chave (home, ranking e perfil) desenhadas. Você escolhe uma.
   Referências: `awesome-design-md`. Restrição herdada do ADR-0013: a taxonomia de risco (o
   vermelho só para os 3,3% de gravidade alta, forma além da cor, nada de caixa alta) continua
   valendo, porque é posição jurídica, não estética.

**Execução:** primeiro os tokens e componentes base, depois tela por tela, terminando no que
`pendencias.md` §2 deixou parado (perfil e revelação progressiva). Cada tela é verificada no
navegador embutido, nos quatro modos (claro, escuro, desktop e celular), com os testes do
Vitest verdes.

**Skills:** `superpowers:brainstorming`, `frontend-design`, `awesome-design-md`,
`vectorlab-ux-skills` (typography, colour-palette, spacing, surfaces, empty-states, loaders,
motion, reduced-motion, forms), `design:ux-copy`, `audit-suite:make-interfaces-feel-better`,
`audit-suite:vercel-react-best-practices`, `design:accessibility-review` e
`web-design-guidelines`.

## Etapa 3 — Segurança e privacidade

Roda **depois** das etapas 1 e 2 estarem mescladas, para auditar o código final.

1. Varredura de código: `claude-security:scan` (multiagente), conferida pela disciplina do
   `verified-security-sweep` (cada achado reproduzido antes de virar correção) e
   `security-review`.
2. Dependências: `npm audit`, `dotnet list package --vulnerable --include-transitive` e
   imagens Docker com versão fixada.
3. **Histórico do git:** `gitleaks` completo, conferindo um a um os 6 alertas.
4. **Dado pessoal:** varredura de CPF, e-mail e telefone em tudo que é versionado (fixtures,
   testes e docs). CPF real vira CPF sintético com dígito válido, e nome de sócio vira nome
   fictício. A varredura vira um teste de CI que impede regressão.
5. Correções com teste, como na Etapa 1.

**Skills:** `claude-security:scan`, `verified-security-sweep`, `security-review` e
`superpowers:verification-before-completion`.

## Etapa 4 — Prints e GIF

1. Subir a stack local (`compose.local.yml`) com a base real e o motor final.
2. Roteiro de capturas: home, ranking (Câmara e Senado), perfil com achados, composição do
   índice, sócios em comum, mapa do Estado e comparador, cada um em claro e escuro, desktop e
   celular.
3. Dois ou três GIFs curtos (de 10 a 20 segundos): busca → perfil → achado; navegação no
   grafo do Estado; tema escuro.
4. Conferir cada imagem por dado pessoal. Nenhum CPF, nem mascarado, aparece em print.

**Skills:** `run`, navegador embutido para os prints e `gif_creator` do Claude in Chrome (ou
gravação do Playwright convertida com ffmpeg) para os GIFs.

## Etapa 5 — README, post-mortem e encerramento

1. **README novo:** o que é, prints, como rodar localmente do zero, arquitetura em uma
   figura, as 12 regras em uma tabela, testes, licença e aviso de dados congelados.
2. **Post-mortem** (`docs/post-mortem.md`): a tese, o que foi construído, o que funcionou, o
   que não funcionou (B2C desconectado do eleitor, B2B genérico demais, competir com players
   consolidados) e o que você faria diferente.
3. **ADR-0022 — Encerramento:** supera o ADR-0008 (repositório público) e registra o fim das
   frentes.
4. Remover o job `visibilidade` do CI, atualizar `estado_atual_projeto.md` e fechar
   `pendencias.md`.
5. Revisão final do texto: `humanizer`.

## Etapa 6 — A publicação (só com o seu "pode")

1. **Reescrever o histórico** com `git filter-repo` para tirar CPFs e o que mais a Etapa 3
   encontrar. Vem por último porque reescrever o histórico quebra qualquer branch aberta.
2. Repetir o `gitleaks` e a varredura de CPF no histórico reescrito: precisam sair zerados.
3. Tornar o repositório público. **Ação externa e irreversível na prática**, por isso só com
   a sua confirmação explícita na hora.

---

## Paralelismo

```
Etapa 1 (backend) ──┐
                    ├──► mesclar ──► Etapa 3 ──► Etapa 4 ──► Etapa 5 ──► Etapa 6
Etapa 2 (frontend) ─┘
```

As etapas 1 e 2 rodam ao mesmo tempo, cada uma em sua própria sessão e worktree, porque mexem
em pastas diferentes. Exceção prevista: se a Etapa 1 mudar algum contrato da API (campo novo
ou renomeado), a Etapa 2 é avisada e ajusta. A mesclagem das duas branches aqui é feita com a
sua autorização. Daí em diante tudo é sequencial.

## Pontos de aprovação (onde eu paro e espero você)

1. Este plano mestre.
2. A tabela de achados da Etapa 1, antes de corrigir.
3. A direção visual da Etapa 2, entre as três.
4. A mesclagem das etapas 1 e 2.
5. Os achados de segurança graves, se houver.
6. O texto do README e do post-mortem.
7. A reescrita do histórico e a abertura do repositório.

## Decisões que preciso de você antes de começar

1. **Documentos de negócio** (canvas, análise B2B, questões e respostas de negócio): publicar
   junto ou tirar do repositório público? Recomendação: tirar, guardando uma cópia privada. É
   estratégia comercial da Lotus, não engenharia, e o post-mortem conta a história em
   público.
2. **Licença:** MIT (mais simples, melhor para portfólio) ou AGPL-3.0 (obriga quem rodar como
   serviço a abrir o código). Recomendação: MIT.
3. **Identidade da Lotus Tech:** existe logo, paleta ou fonte que o portfólio deva seguir? Se
   não existir, as três direções saem livres.
4. **Repositório:** abrir o próprio `Lotus-Tecnologia/explanando`, com o histórico reescrito,
   ou criar um repositório novo? Recomendação: o mesmo, porque os 121 commits e os 21 ADRs
   são a melhor parte do portfólio.
