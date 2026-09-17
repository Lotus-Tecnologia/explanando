# Etapa 08 — A linguagem da interface

**Data:** 2026-09-16 · **Etapa anterior:** [07 — Observabilidade e deploy](etapa-07-observabilidade-e-deploy.md)

Primeira fatia do trabalho de interface pedido depois que o site foi olhado com olhos de
leitor, e não de quem o construiu. A identidade visual e o banho de UI/UX vêm a seguir, com
referências; esta etapa trata só de **o que a página diz**.

---

## 1. Objetivo

Tirar o vocabulário interno da tela.

O produto é lido por duas populações ao mesmo tempo: quem navega em site com naturalidade e
quem não navega. O bloco de cobertura do perfil — cujo título é literalmente **"O que foi
analisado"** — respondia essa pergunta com onze siglas: `REG_GEO_002`, `REG_KYB_001`,
`REG_TX_002`. Mostrava a pergunta e devolvia o identificador.

## 2. Definido

**O código da regra sai da página e continua no contrato.** Para o banco em due diligence de
PEP e para a redação investigativa, `REG_TX_003` é identificador estável de metodologia: é
como se cita a regra. Para o cidadão que abriu a página do próprio deputado, é ruído. A API
publica os dois campos; quem desenha escolhe.

**A redação de leitor é separada de `IRegraAnomalia.Descricao`.** Aquela frase nasceu para
documentação e telemetria e fala a língua de quem implementou a regra — "notas com numeração
inicial, sugerindo cliente pioneiro", "gasto mensal próximo do teto da cota da UF". Serve ao
log e ao ADR; não serve a quem nunca leu o código. Manter as duas separadas custa uma tabela
e dois testes de deriva, e evita que melhorar o texto da tela mexa no que a telemetria agrega.

**A redação passou a valer sob o [ADR-0004](../decisoes/ADR-0004-nomenclatura-tecnica.md).**
Ao virar texto que o produto afirma sobre pessoa nomeada numa página indexada, ela deixou de
ser comentário e passou a obedecer a mesma regra das descrições de flag. Antes disso, nada
verificava o vocabulário dessas frases, porque elas não iam para lugar nenhum.

**Ausência de redação cai para o código, não some com a linha.** O bloco de cobertura existe
para ser **completo**: regra ausente da lista é lida como regra que ninguém considerou, que é
a afirmação mais cara do produto inteiro. Sigla feia é melhor que omissão.

## 3. Executado

| Camada | O quê |
|---|---|
| **Domain** | `Regras/CatalogoDeRegras.cs` — a redação de leitor das 12 regras, por código |
| **Application** | `CoberturaDeRegra.Descricao` no contrato de leitura |
| **Infrastructure** | `ConsultasPublicasEfCore.Traduzir` resolve a frase na leitura |
| **Frontend** | `contrato.ts`, `Abstencao.tsx`, `TagDeGravidade.tsx`, `ListaDeDespesas.tsx`, `despesas/[id]/page.tsx` |

A resolução acontece **na leitura**, não na persistência. O banco guarda o código; melhorar o
texto da tela não pode exigir reprocessar 513 parlamentares.

No frontend, o bloco de cobertura passou a ter a frase como rótulo da linha e a tag de
situação à direita. A `TagDeGravidade` perdeu o código **do texto e do nome acessível** — ele
era lido como *"indicador de alta relevância, regra REG_KYB_002"*, e quem usa leitor de tela
ouvia a sigla soletrada a cada achado da lista. A frase que explica o achado já vem ao lado,
vinda da API, com valor e fornecedor reais.

Também foi corrigido um incômodo de ambiente: `npm run lint` varria `.open-next/` e
`.wrangler/`. Ver §5.

## 4. Testes

| Nível | O que cobre |
|---|---|
| Domínio | Deriva nos dois sentidos, vocabulário do ADR-0004, caixa alta, código desconhecido |
| E2E | O perfil chega à borda com a frase, e sem `REG_` dentro dela |
| Componente | A frase no lugar do código, o fallback que não some com a linha, a tag sem sigla |

**701 no backend** (428 domínio, 54 aplicação, 167 integração, 42 ponta a ponta, 10
arquitetura) e **113 no frontend**. `tsc --noEmit` limpo, lint limpo, build com 0 avisos.

Os dois testes de deriva guardam sentidos opostos: regra nova que entre no motor sem redação,
e redação que sobreviva a uma regra removida. O segundo evita o bloco prometer uma análise que
não acontece mais.

Verificação contra a base real da Neon, nas quatro páginas: **zero `REG_` no texto visível**.
As 37 ocorrências que restam no HTML do perfil estão dentro de `<script>`, no payload RSC,
onde o código é a `key` do React — invisível para leitor e para leitor de tela.

## 5. Descobertas

**A busca por nome já existia, e quase foi reconstruída.** A ausência de parâmetro de busca no
endpoint do ranking e a lista de rotas do frontend levaram à conclusão errada de que não havia
como um cidadão achar o próprio deputado. `TabelaDoRanking` faz isso desde a Etapa 06: filtra
por nome, partido e UF, dobra acento, preserva a posição original ao filtrar e anuncia a
contagem numa região viva. **Ler o componente, não inferir da API.**

**E ela cobre os 513, não os 200 do teto.** `ranquearTudo` pagina até o fim de propósito —
`ConsultasPublicasEfCore.Normalizar` faz `Math.Clamp(itens, 1, 200)`, então `?itens=513`
devolve **200 e um `200 OK`**, sem erro e sem aviso. A armadilha já estava documentada em
`lib/api.ts`.

**A dobra de acento do frontend usa `normalize("NFD")`, e pode.** A armadilha do
`InvariantGlobalization=true`, que torna `String.Normalize(FormD)` um no-op silencioso, é do
.NET. No JavaScript o método funciona. O domínio continua dobrando por tabela explícita — são
ambientes diferentes, e os dois estão certos.

**O ESLint não lê o `.gitignore`.** A config achatada ignora só `node_modules` e `.git` por
conta própria, e o `globalIgnores` do projeto **substitui** os padrões do `eslint-config-next`
em vez de somar a eles. Como `.open-next/` e `.wrangler/` estavam no `.gitignore` mas não ali,
um `npm run lint` rodado depois de qualquer `cf:build` varria o bundle gerado: **43.595
problemas em código que não é nosso**. O CI nunca viu — lá o checkout é limpo e o lint roda
antes de existir build. Quem pagava era quem desenvolve.

**O cache de dados do Next mascara mudança de contrato.** Depois de a API passar a devolver
`descricao`, a página seguiu servindo a resposta velha: o campo novo existia no JSON e não na
tela. Conferir a API direto antes de acusar o componente.

**`frontend/AGENTS.md` e `CLAUDE.md` são regerados pelo `next dev` a cada subida.** Ignorá-los
não para o ruído — só o transforma em arquivo permanentemente ignorado. Foram versionados.

## 6. Pendências

| Pendência | De quem é |
|---|---|
| **Identidade visual e banho de UI/UX** | Próxima fatia; referências virão do Gustavo |
| **Limites da CEAP 2023–2025 e da CEAPS** | Sua — enquanto não vierem, `REG_TX_002` responde `SemDado` e a interface diz isso |
| Decidir se o Senado entra **antes** do redesenho | Sua — a assimetria do ADR-0012 é visível na tela, e desenhar sem saber significa desenhar duas vezes |
| Uma página de metodologia, onde o código da regra teria casa | Minha — hoje ele só existe na API |

## 7. Próxima etapa

**Identidade visual e UI/UX**, com as referências que o Gustavo trouxer. O que o redesenho
tem de herdar, e não é gosto:

- O vermelho fica **estritamente** com a gravidade alta, que é 3,3% dos achados
- Cada nível carrega **cor, forma e rótulo** — matiz sozinho falha para ~8% dos homens
- **Sem caixa alta**: ênfase sobre achado descritivo é imputação pela tipografia
- Toda cor nova passa pelo cálculo de contraste **antes** de entrar — seis valores já
  reprovaram numa revisão, três deles propostos depois de o problema ter sido apontado

Ver [ADR-0013](../decisoes/ADR-0013-identidade-visual-e-taxonomia-de-risco.md).

Em paralelo, e sem depender disto: a API no Cloud Run e o DNS, que continuam sendo o caminho
crítico e dependem de credencial. A trava de indexação é o que torna as duas trilhas
simultâneas seguras — dá para subir em produção sem o buscador conhecer o site.
