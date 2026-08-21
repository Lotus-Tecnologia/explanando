# Explanando - Identidade Visual e Design System (Padrão GovTech)

**Status:** Definido
**Objetivo:** Estabelecer as diretrizes de UI/UX para o front-end em Next.js + Tailwind CSS. A interface deve remeter à sobriedade de portais de transparência governamentais (foco em exibição de tabelas densas e leitura de dados), utilizando uma paleta autoral baseada em tons de verde e branco para garantir independência visual e institucional.

---

> [!IMPORTANT]
> **A paleta e a taxonomia de risco deste documento foram revisadas pelo
> [ADR-0013](../decisoes/ADR-0013-identidade-visual-e-taxonomia-de-risco.md)** em 2026-08-21.
>
> Seis valores de cor reprovaram no cálculo de contraste — entre eles `primary #10B981`, que dá
> 2,54:1 com texto branco — e a tag única de "regra quebrada" em vermelho foi substituída por
> quatro pesos visuais, porque só 3,3% das flags da base real são de gravidade alta.
>
> **Onde este documento e o ADR-0013 divergirem, o ADR vence.** Os princípios de design abaixo
> continuam valendo integralmente.

## 1. Princípios de Design
*   **Densidade de Dados (Data-Heavy):** A interface deve priorizar a exibição de muita informação na tela sem parecer poluída. O espaço em branco (whitespace) é usado para separar blocos, não para enfeitar.
*   **Alta Legibilidade:** Foco total no contraste. Textos escuros sobre fundos claros.
*   **Sobriedade B2B/Cívica:** Sem sombras exageradas, sem bordas arredondadas extremas, sem gradientes lúdicos. O design é "Flat" (plano) e funcional.
*   **Independência de Marca:** O logotipo deve ser tipográfico ou usar formas geométricas (ex: grafos ou nós de rede), proibido o uso de brasões, bandeiras ou cúpulas do Congresso.

---

## 2. Paleta de Cores (Tailwind CSS)

A paleta elimina o tradicional azul governamental e assume o **Verde** como cor de marca e confiança, com alto contraste.

```javascript
// tailwind.config.js
module.exports = {
  theme: {
    extend: {
      colors: {
        governo: {
          // Fundos e Superfícies
          background: '#FFFFFF', // Branco puro para o fundo principal
          surface: '#F8FAFC',    // Cinza levíssimo (Slate 50) para zebra de tabelas e cards
          border: '#E2E8F0',     // Cinza claro para linhas separadoras de tabelas
          
          // Tipografia
          textoBase: '#334155',  // Cinza chumbo (Slate 700) para leitura de parágrafos
          titulo: '#064E3B',     // Verde muito escuro (Emerald 900) para H1, H2 e dados em destaque
          
          // Destaques e Ações (O Verde da Marca)
          primary: '#10B981',    // Verde médio (Emerald 500) para botões principais
          primaryHover: '#059669',// Verde escuro (Emerald 600) para hover
          highlight: '#D1FAE5',  // Verde bem claro (Emerald 100) para tags e fundos de destaque
          
          // Alertas de Risco (Essencial para o Motor AML)
          anomaliaFundo: '#FEE2E2', // Vermelho bem claro para a linha da tabela com flag
          anomaliaTexto: '#B91C1C', // Vermelho escuro para o texto da flag
        }
      }
    }
  }
}
```

---

## 3. Tipografia

A tipografia deve ser estritamente **Sem Serifa (Sans-Serif)**, otimizada para telas e leitura de números.

*   **Fonte Principal (UI e Textos):** `Inter` ou `Roboto`. Ambas têm excelente legibilidade em tamanhos pequenos e numerais monoespaçados perfeitos para tabelas de valores financeiros.
*   **Dados Tabulares (Valores e CPFs/CNPJs):** Utilizar a classe `tabular-nums` do Tailwind para alinhar os números verticalmente e evitar que a tabela "trema" ao mudar de linha.

---

## 4. Componentes Estruturais

### Tabelas Densas (O Coração da Interface)
Tabelas financeiras não devem ter cards individuais, devem seguir o padrão "Data Grid".
*   **Cabeçalhos (Headers):** Fundo branco, texto em verde escuro (`text-governo-titulo`), fonte em negrito (`font-bold`), com borda inferior grossa. Sticky header (fixo no topo ao rolar a página).
*   **Linhas (Rows):** Zebra-striping (uma linha branca, uma linha `bg-governo-surface`) para guiar a leitura horizontal em monitores largos.
*   **Células (Cells):** Padding reduzido (`px-3 py-2`) para caber mais dados na tela.

### Blocos de Informação (Painéis e Cards)
*   Formato retangular, bordas levemente arredondadas (`rounded-sm` ou `rounded-md`, nunca `rounded-3xl`).
*   Bordas sólidas de 1px (`border-governo-border`), sem sombras projetadas (`shadow-none`), mantendo a estética formal e de auditoria.

### Tags de Status e Flags (Metadados)
Cruciais para as 21.067 flags detectadas:
*   **Tag de Regra Quebrada:** Fundo vermelho claro, texto vermelho escuro. `bg-governo-anomaliaFundo text-governo-anomaliaTexto px-2 py-1 text-xs font-semibold uppercase`.
*   **Tag de Metadado (Ex: "Aberto desde 2022"):** Fundo verde claro, texto verde escuro. `bg-governo-highlight text-governo-titulo px-2 py-1 text-xs font-semibold`.

---

## 5. Estrutura de Layout (Grid e Largura)
*   **Containers Largos:** Não espremer a aplicação em uma coluna central estreita. Utilizar `max-w-7xl` (1280px) ou `max-w-[1440px]` para aproveitar o monitor de auditores e jornalistas.
*   **Filtros Laterais:** Como no Portal da Transparência, manter uma barra lateral (Sidebar) fixa à esquerda para cruzamento de filtros (Ano, Partido, Estado, Fornecedor), deixando a tabela fluir na área principal à direita.
