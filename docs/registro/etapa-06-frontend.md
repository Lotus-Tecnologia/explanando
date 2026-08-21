# Etapa 06 — Frontend Next.js

**Data:** 2026-08-21 · **Situação:** ✅ Concluída no recorte "o que a API de leitura sustenta"

---

## 1. Objetivo

Publicar a interface pública sobre o contrato da [Etapa 05](etapa-05-api-publica.md), com a
taxonomia do [ADR-0013](../decisoes/ADR-0013-identidade-visual-e-taxonomia-de-risco.md), e
fazer valer na tela a premissa de custo do
[ADR-0001](../decisoes/ADR-0001-frontend-nextjs.md): **é a CDN, não o Redis, que absorve o
pico viral**.

O recorte foi decidido no começo da etapa: tudo o que a API de leitura sustenta hoje. Card
viral, favoritar, contestar e pagamento ficam fora — dependem de autenticação, gateway e
endpoints de escrita que não existem.

## 2. O que existe agora

```
frontend/src/
  app/
    layout.tsx                          rodapé, metadataBase, template de título
    page.tsx                            ranking por volume — ESTÁTICO
    ranking/[ordenacao]/page.tsx        índice e gravidade — ESTÁTICOS
    parlamentares/[id]/page.tsx         perfil — 513 páginas SSG
    parlamentares/[id]/despesas/        listagem completa, filtros e paginação
    despesas/[id]/page.tsx              o endereço público de uma despesa
    sitemap.ts · robots.ts              516 URLs anunciadas
    not-found.tsx · error.tsx           404 e limite de erro
  componentes/
    TagDeGravidade · Abstencao · Comprovante        (ADR-0013, os obrigatórios)
    TabelaDoRanking     513 linhas com busca no navegador
    ListaDeDespesas     compartilhada por perfil e listagem
    BarraDeFiltros      formulário GET, sem JavaScript
    Paginacao · SerieMensal · Aviso · Rodape
  lib/
    contrato · api · formato · metadados · ordenacoes · filtros · site
```

**Testes: 93 no frontend** (eram 17) e **676 no backend** (eram 666).

## 3. A medição que era o ponto da etapa

| | Antes | Depois |
|---|---|---|
| Home `/` | `ƒ` dinâmica | **`○` estática** |
| Perfis | `ƒ` dinâmicos | **`●` 513 SSG** |
| Ordenações | 1 página com `?ordenar=` | **3 rotas estáticas indexáveis** |
| URLs no sitemap | — | **516** |
| `Cache-Control` na API | ausente | `public, max-age=60, s-maxage=300, stale-while-revalidate=600` |

```
✓ Generating static pages (521/521)
```

Conferido contra a base real: `/` lista **513**, `/ranking/indice` lista **498** — os 15 que o
[ADR-0009](../decisoes/ADR-0009-dois-numeros-nao-um.md) exclui por amostra insuficiente, com a
frase que impede a leitura de "esses 15 não têm achado".

## 4. Descobertas

### 4.1 O filtro por tipo de despesa rodava depois da paginação — e mentia

`ConsultasPublicasEfCore.ListarDespesasAsync` aplicava o filtro por `TipoDespesa` **em
memória, sobre a página já recortada**. O comentário no código assumia a escolha de propósito,
mas não percebia a consequência: `Total` contava o que o filtro descartava, e as páginas do
fim vinham vazias.

Na base real, filtrar por "locação de veículos" numa parlamentar com 3.271 despesas devolvia
"75 de 3.271" na contagem errada e páginas vazias a partir da segunda.

**É a mesma classe de mentira que faz a API devolver 400 para mês sem ano** — aceitar em
silêncio e o leitor achar que filtrou. O filtro passou para o SQL: o nome vira código do
catálogo em C# (tolerando caixa), e a comparação vai num `smallint`. Traduzir o nome uma vez
não duplica o catálogo em SQL, que era a objeção do comentário original.

Três testes E2E travam isso: total correto, alcance além da primeira página, e tipo
desconhecido devolvendo lista vazia em vez da base inteira.

### 4.2 A despesa não sabia de quem era

`ObterDespesaAsync` devolvia um `ItemDeDespesa` sem nenhuma referência ao parlamentar. Uma
página em `/despesas/{id}` — que é o endereço que um link compartilhado abre — mostraria um
valor e um código de regra pairando no ar, sem volta para o perfil.

Isso derruba o arranjo em que o [ADR-0004](../decisoes/ADR-0004-nomenclatura-tecnica.md) se
apoia: o achado é atribuído a uma regra nomeada, sobre o gasto de alguém, com o documento
oficial ao alcance do clique.

Entrou `DespesaDetalhada` = despesa + `ParlamentarDaDespesa`. A `Casa` viaja junto **porque a
tag de comprovante depende dela** (§4.3).

### 4.3 A tag de comprovante ausente dizia "a fonte", e o ADR-0012 pede o nome

O componente exibia *"Fonte não disponibiliza o comprovante"* para todo mundo. O
[ADR-0012](../decisoes/ADR-0012-assimetria-camara-senado.md) §1 exige atribuir a ausência ao
**Senado nominalmente** — genérico devolve a suspeita de falha para a plataforma.

E a frase não pode ser a mesma nas duas casas: no Senado a ausência é **estrutural** (nenhum
dos treze campos da CEAPS é URL); na Câmara é **daquela nota** (70,5% têm link). "A Câmara não
disponibiliza" seria falso. São três frases, decididas pela casa.

> **Não verificável contra dado real ainda.** A base tem só a Câmara. O caminho do Senado está
> coberto por teste unitário, e só a ingestão do Senado vai confrontá-lo com dado.

### 4.4 Três campos do contrato nunca tinham sido usados

`fotoUrl`, `serieMensal` e `dataUltimaVarredura` viajavam no payload desde a Etapa 05 sem
nenhum consumidor. Os três entraram no perfil.

A data da varredura ficou **ao lado do nome, não em rodapé**, pelo motivo que a
[análise das respostas](../decisoes/analise-das-respostas-parte-1.md) §4 levantou: com
atualização sob demanda, dois parlamentares têm frescor diferente, e quem foi varrido mais
recentemente parece pior — não por ser, mas por ter sido olhado mais perto.

### 4.5 `export const revalidate` não aceita constante importada

`export const revalidate = SEGUNDOS_DE_REVALIDACAO` derruba o build inteiro com *"Invalid
segment configuration export"*. O Next lê esse export **estaticamente**, antes de executar
qualquer módulo. Precisa ser literal — em cinco arquivos, com o comentário explicando por quê
para ninguém "limpar" a duplicação depois.

O erro não aponta o arquivo: diz "veja as falhas acima" e não há falha acima.

### 4.6 Página fora da faixa dizia a frase errada

Pedir `?pagina=9` num filtro de 3 páginas devolvia *"Nenhuma despesa corresponde a este
filtro"* — falso, o filtro casa 75. Nem todo vazio tem a mesma causa, e a tela precisa
distinguir "o filtro não achou" de "esta página não existe".

## 5. Decisões de interface

**Os 513 numa página só, com busca no navegador.** A API não tem endpoint de busca, e paginar
de 50 em 50 esconderia 463 pessoas atrás de dez cliques — num produto cujo leitor típico chega
procurando **um** parlamentar. Com a lista completa já na página, achar alguém custa uma
digitação e nenhuma requisição, e a página continua sendo arquivo estático.

**A ordem continua vindo do servidor.** Ordenar no navegador duplicaria a regra do ADR-0009 —
que exclui quem está abaixo do piso — onde ela sairia de sincronia sem ninguém perceber.

**A barra de filtros é um formulário GET de verdade.** Funciona sem JavaScript, o resultado
tem endereço próprio que se cola em qualquer lugar, e o botão de voltar desfaz o filtro.

**Um filtro nunca pode produzir lista vazia por engano.** As opções vêm do parlamentar — anos
da série mensal, tipos da API —, valor impossível é descartado e o formulário volta a exibir
"todos", e mudar um filtro volta para a página 1. Lista vazia numa página de gastos é lida
como "não gastou", que é a mesma classe de erro do `Falhas: 0` da coleta.

**O gráfico tem tabela de verdade ao lado, não `alt`.** SVG sozinho não existe para leitor de
tela, e num produto de utilidade pública o número é o conteúdo. A barra usa `--color-acento`,
o verde que reprova para texto (3,77) e serve só para elemento não textual — o uso que o
ADR-0013 reservou para ele. **Nenhuma cor nova entrou**, então a regra do cálculo de contraste
não foi acionada.

**A meta description carrega as ressalvas.** É o que muita gente vai ler sem abrir a página, e
ali não há bloco de cobertura ao lado para corrigir a impressão. Densidade insuficiente vira
frase, nunca `0,00`; regra sem dado é declarada no resumo. Há teste varrendo os termos do
ADR-0004 no título e na descrição.

**O rodapé nomeia o Encarregado de Dados.** ADR-0010 §2, e a LGPD art. 41 §1 pede identidade
**e** contato — só o e-mail não cumpre.

## 6. Pendências

| Pendência | Impacto | Observação |
|---|---|---|
| Card viral com disclaimer na imagem | Médio | Fase 5. `html2canvas` é client-side e não depende de backend — cabe numa etapa própria |
| Favoritar, contestar, varredura paga | Médio | Exigem autenticação, pagamento e endpoints de escrita |
| Tag do Senado sem confronto com dado real | Baixo | Só a ingestão do Senado resolve |
| Limitação de taxa na API | Médio | Sem autenticação, só o teto de 200 itens/página protege |
| Limites da CEAP 2023–2025 | **Alto** | `REG_TX_002` segue `SemDado` na base inteira |

## 7. Próxima etapa

**Etapa 07 — Observabilidade e deploy.** OpenTelemetry, Cloud Run, Cloudflare à frente, e o
Neon no lugar do Postgres local. Os cabeçalhos de cache já saem prontos para a CDN; falta a
CDN existir.
