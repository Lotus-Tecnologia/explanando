# ADR-0013 — Identidade visual: a taxonomia de risco é o ADR-0004 em pixels

**Data:** 2026-08-21 · **Situação:** Aceita

## Contexto

O [documento de Identidade Visual](../especificacao/Explanando_Identidade_Visual.md) estabeleceu
os princípios certos — denso, plano, sóbrio, sem brasão nem cúpula. Duas coisas, porém, não
sobrevivem ao confronto com a base real e com o
[ADR-0004](ADR-0004-nomenclatura-tecnica.md).

**A primeira é de posição jurídica.** A especificação previa uma tag única de "regra quebrada",
em vermelho, caixa alta e negrito. O ADR-0004 obriga o produto a **descrever sem imputar**, e a
descrição de cada flag foi redigida com esse cuidado — a ponto de recusarmos a palavra "lavagem"
numa razão social. Um badge vermelho em caixa alta diz "culpado" antes de qualquer palavra ser
lida. **A cor é texto.**

**A segunda é de fidelidade ao dado.** A varredura dos 513 deputados produziu:

| Gravidade | Flags | % |
|---|---|---|
| Alta | 695 | **3,3%** |
| Média | 11.258 | 53,4% |
| Baixa | 9.114 | **43,3%** |

Pintar as 43,3% de gravidade baixa com o mesmo vermelho das 3,3% de gravidade alta deturpa a
distribuição inteira. Não é escolha estética: é afirmar sobre 20.372 achados uma severidade que
o motor não atribuiu.

## Decisão

### 1. Quatro pesos visuais, não um

Cada nível carrega **cor, forma e rótulo**. A forma existe porque matiz sozinho falha para
cerca de 8% dos homens, que têm deficiência de visão de cor — e severidade é justamente o que
não pode se perder.

| Nível | Fundo | Texto | Ícone | Contraste |
|---|---|---|---|---|
| **Baixa** (43,3%) | `slate-50` `#F8FAFC` | `slate-700` `#334155` | `i` em círculo | **9,90** AAA |
| **Média** (53,4%) | `amber-100` `#FEF3C7` | `amber-900` `#78350F` | `!` em triângulo | **8,15** AAA |
| **Alta** (3,3%) | `red-100` `#FEE2E2` | `red-800` `#991B1B` | `×` em octógono | **6,80** AA |
| **Não avaliado** | `slate-100` `#F1F5F9` | `slate-600` `#475569` | `–` em círculo tracejado | **6,92** AA |

**O vermelho fica estritamente com os 3,3%.** A gravidade baixa é praticamente neutra — o
achado aparece, mas não grita.

Nada de `uppercase`. Caixa alta em texto curto é lida como ênfase, e ênfase sobre um achado
descritivo é imputação pela tipografia.

Toda tag precisa de nome acessível — `aria-label` com o nível e o código da regra. Ícone sem
rótulo não existe para leitor de tela, e o leitor de tela é justamente quem depende mais da
forma.

### 2. "Não avaliado" é componente de primeira classe

É a pedra angular da confiabilidade. `REG_TX_002` está em **zero na base inteira** — e zero por
falta de dado tem de ser visualmente distinto de zero por nada encontrado.

O componente exibe o motivo, não só o estado: *"Não avaliado — a tabela de limites da CEAP não
cobre este período"*. Vem da `MetadadosDeCobertura`, que já carrega `SituacaoDaRegra` e
`SilencioEhConclusivo`.

**A interface é proibida de renderizar uma regra abstida como aprovada.** Um perfil sem flags
não pode exibir "nenhum indicador encontrado" quando alguma regra se absteve — precisa dizer
quantas foram avaliadas e quantas não.

### 3. A opacidade do Senado é da fonte, não nossa

Conforme [ADR-0012](ADR-0012-assimetria-camara-senado.md) §1, no lugar do botão de comprovante:

> 🚫 Senado não disponibiliza o comprovante

Tag inativa, `slate-100` com `slate-600`, sem estado de hover e sem cursor de link. A frase
atribui a ausência a quem a produziu — o espaço vazio onde o link deveria estar seria lido como
falha da plataforma.

Idem para a glosa: quando `GlosaConhecida` é falso, a quebra "valor declarado × glosa" é
**omitida por inteiro**. Exibir `R$ 0,00` afirmaria que houve conferência e nada foi cortado.

### 4. Contraste medido, não estimado

Toda cor abaixo foi calculada. **Três valores da proposta original — e três da minha própria
revisão — reprovaram.**

| Papel | Valor | Uso | Razão |
|---|---|---|---|
| `titulo` | `#064E3B` | H1–H3, dados em destaque, **botões de ação** | 9,72 AAA |
| `tituloHover` | `#053E2F` | hover do botão | 12,09 AAA |
| `textoBase` | `#334155` | parágrafos e células | 10,35 AAA |
| `acento` | `#059669` | **só elemento não textual**: barra de gráfico, ícone, borda de destaque | 3,77 — reprova para texto |
| `bordaEstrutural` | `#64748B` | borda inferior do cabeçalho de tabela | 4,76 |
| `bordaSuave` | `#CBD5E1` | separador entre linhas | 1,48 — ver nota |
| `surface` | `#F8FAFC` | zebra e cards | — |

**O que mudou e por quê:**

- **`#10B981` sai de `primary`.** Branco sobre ele dá **2,54:1** — reprova para texto em
  qualquer tamanho. Um botão "Ver nota fiscal" seria ilegível para muita gente.
- **`#059669` também não serve para botão com texto** (3,77). Foi proposto como substituto e
  medido depois; vira `acento`, para uso não textual, onde 3:1 basta.
- **O botão de ação usa `#064E3B`** (9,72), que já era a cor de título.
- **`#E2E8F0` sai da borda estrutural** (1,23 — invisível para baixa visão). O cabeçalho passa
  a `#64748B`.

> **Nota sobre `bordaSuave` (1,48).** Fica abaixo de 3:1 e isso é deliberado: o separador entre
> linhas é **redundante**, porque a zebra já carrega a separação. A WCAG exige 3:1 para o que é
> *necessário* à compreensão — e aqui não é. A borda do **cabeçalho**, essa sim estrutural,
> cumpre 4,76.

### 5. O que se mantém da especificação original

Denso e plano, sem sombra nem canto muito arredondado. `tabular-nums` nas colunas de valor.
Cabeçalho fixo, zebra, padding reduzido. `max-w-7xl` com filtros em barra lateral. Verde no
lugar do azul governamental, sem brasão, bandeira ou cúpula — que afasta a confusão com portal
oficial e é proteção jurídica, não preferência estética.

## Consequências

**A interface não pode ser construída só com a cor certa.** Ela precisa consultar
`SilencioEhConclusivo` e `GlosaConhecida` antes de desenhar, e isso é contrato de API, não de
CSS. A [Etapa 05](../registro/etapa-04d-coleta-completa.md) tem de expor os dois.

**Quatro componentes viram obrigatórios** e devem ser testados: tag de gravidade nos três
níveis, tag de abstenção, tag de comprovante ausente e o bloco de cobertura do perfil.

**Toda cor nova passa pelo cálculo antes de entrar.** Seis valores reprovaram nesta revisão,
três deles propostos por mim depois de já ter apontado o problema nos outros três. Estimar
contraste a olho não funciona nem para quem acabou de medir.

## Alternativas descartadas

**Tag única em vermelho, como na especificação original.** Simples e visualmente forte.
Descartada: afirma severidade alta sobre 96,7% dos achados que não a têm, e converte um produto
descritivo em acusatório pela via visual — contornando o ADR-0004 sem violar uma palavra dele.

**Diferenciar gravidade só por cor.** Menos componentes. Descartada por acessibilidade: falha
para daltonismo e para leitor de tela, e severidade é o atributo que menos pode se perder.

**Omitir a regra abstida da tela.** Interface mais limpa. Descartada: é o mesmo erro do
`Falhas: 0` que nos enganou na coleta — ausência apresentada como resultado. A abstenção
precisa ocupar espaço para ser lida.

---

## Emenda de 2026-09-18 — o tema escuro entra, e a objeção original continua valendo

Este ADR não previa tema escuro, e o `globals.css` carregava a justificativa:

> *Sem bloco de tema escuro. O design se compromete com texto escuro sobre fundo claro — é a
> estética de portal de transparência, e alternar **inverteria o significado das cores de
> risco**, que são o único lugar onde a cor carrega informação.*

O Gustavo pediu o tema escuro com alternância, e a decisão muda. **Mas a objeção era boa, e é
ela que define como a mudança foi feita:** nada inverte.

Âmbar continua sendo o peso médio e vermelho continua sendo o alto. As formas — círculo,
triângulo, octógono, círculo tracejado — e os rótulos seguem idênticos. O que muda é só o par
fundo/texto de cada peso, recalculado para fundo escuro, de modo que a hierarquia
`baixa < média < alta` se leia igual nos dois temas.

O vermelho segue **estritamente** com os 3,3% de gravidade alta, e não existe estado de
"aprovado" em tema nenhum.

### A paleta escura, calculada

| Token | Valor | Sobre | Contraste |
|---|---|---|---|
| `titulo` | `#6EE7B7` | `#0F172A` | **11,71** AAA |
| `texto` | `#E2E8F0` | `#0F172A` | **14,48** AAA |
| `textoSuave` | `#ADBCD0` | `#1E293B` | **7,58** AAA |
| `acento` | `#34D399` | `#0F172A` | **9,29** AAA |
| Baixa | `#CBD5E1` | `#1E293B` | **9,85** AAA |
| Média | `#FCD34D` | `#422006` | **10,11** AAA |
| Alta | `#FCA5A5` | `#450A0A` | **8,51** AAA |
| Não avaliado | `#ADBCD0` | `#1E293B` | **7,58** AAA |

**Uma assimetria que só o cálculo revela.** No escuro o acento alcança **9,29** e *pode* ser
texto; no claro, `#059669` dá **3,77** e não pode. A regra "acento só em elemento não textual"
vale para o tema claro, não para os dois — e escrevê-la sem essa ressalva teria proibido um uso
legítimo.

### Três estados, não dois

Quem nunca escolheu segue `prefers-color-scheme` e não tem nada marcado no `<html>`; quem
escolheu carrega `data-theme` e vence a preferência do sistema **nos dois sentidos**. Tratar
como booleano quebraria o caso mais comum: sistema no escuro, pessoa que quer claro.

A escolha é aplicada por um script inline no `<head>`, antes da primeira pintura. Sem ele, quem
escolheu escuro recebe um lampejo branco a cada navegação, porque o React só hidrata depois de
o navegador já ter pintado.

### A tipografia ganha um display

**Inter continua no corpo e nas tabelas**, onde as figuras tabulares importam. Os títulos e os
números grandes passam a usar **Familjen Grotesk**, com `latin-ext` — sem esse subconjunto, "ã",
"õ" e "ç" caem no fallback e a linha aparece com duas fontes misturadas, o que num produto em
português acontece já no primeiro título.

O motivo não é estético: a queixa que abriu o redesenho foi o site parecer painel genérico, e é
nos títulos que um rosto próprio resolve isso sem custar legibilidade ao corpo do texto.
