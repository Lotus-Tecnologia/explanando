# Etapa 13 — O mapa de fornecedores compartilhados

**Data:** 2026-09-19
**Decisão:** [ADR-0017](../decisoes/ADR-0017-mapa-de-fornecedores.md)
**Referência:** [CivLab §6](../referencias/civlab.md)

---

## 1. A pergunta

A [Etapa 11](etapa-11-grafo-do-estado.md) adaptou a vista de **instituições** da referência.
Faltava a outra: o *Power map*, que é de **pessoas**.

A definição deles é literal — *"as 20 pessoas mais citadas em 1.005 artigos de 3 fontes nos
últimos 90 dias"*. Não temos fonte de notícias, e não adiantaria comprar uma: uma nuvem de
citações mede a imprensa, não o Estado.

A pergunta que restou foi: **existe uma vista de pessoas que a nossa base sustenta?**

## 2. A medição que decidiu tudo

A candidata é fornecedor em comum. Antes de desenhar, contar.

```
41.105 fornecedores na base
15.272 atendem 2 ou mais parlamentares
    96 atendem mais de 50
```

E os mais compartilhados:

| Fornecedor | Parlamentares |
|---|---:|
| TAM Linhas Aéreas | 500 |
| CASCOL Combustíveis | 417 |
| Auto Posto Cinco Estrelas | 369 |
| WMS Papelaria | 313 |
| Telefônica | 258 |

**Este é o achado que definiu o produto inteiro.** Um grafo de fornecedores compartilhados
ligaria quinhentos deputados por voarem na mesma companhia aérea e quatrocentos por
abastecerem no mesmo posto de Brasília. É geografia de uma cidade em que todo mundo
trabalha no mesmo quarteirão — e cada linha desenhada afirmaria uma relação que não existe.

É o mesmo erro que a [ponte SIAPE↔SIORG](etapa-12-quadro-de-pessoal.md) se recusou a cometer
por distância de edição: parecer um achado não é ser um achado.

O sinal está no extremo oposto — fornecedor que atende **poucos** gabinetes e movimenta
**muito**:

| Corte: 2 a 5 parlamentares, R$ 50 mil ou mais | |
|---|---:|
| Fornecedores | 621 |
| Valor | R$ 140,1 milhões |
| Parlamentares | 463 |
| Pares | 1.386 |

Os maiores, agora, fazem sentido como relação escolhida:

```
FRAME COMUNICACAO DIGITAL       4 parlamentares   R$ 1.554 mil
AUTO LESTE                      5                 R$ 1.407 mil
ORBE PRODUTORA & SERVICOS       2                 R$ 1.349 mil
PONTES COMUNICACAO INTELIGENTE  2                 R$ 1.304 mil
Instituto Mais Cidades          4                 R$ 1.017 mil
```

## 3. A segunda medição, que salvou o desenho

Com 1.386 pares em mãos, o grafo parecia pronto. Não estava: **454 dos 463 parlamentares
caem num único componente conexo.**

Basta uma corrente de fornecedores para ligar quase todo mundo a quase todo mundo. Um
desenho em que "estar conectado" vale para 98% das pessoas não distingue coisa alguma — e
seria lido como se distinguisse.

Exigindo **dois** fornecedores em comum:

| | |
|---|---:|
| Pares densos | **247** |
| Pares descartados | 1.139 |
| Parlamentares no desenho | **224** |
| Agrupamentos separados | **49** (de 2 a 14 pessoas) |

## 4. O que o recorte revelou

Dos 247 pares densos:

| | |
|---|---:|
| Do mesmo estado | **91%** |
| Do mesmo partido | **19%** |

**A proximidade é geográfica, não partidária.** Gabinetes de um estado contratam no mercado
daquele estado, e isso explica a maior parte do desenho.

Isso não é decepção — é a explicação de um número que já estava na base e ninguém tinha
lido: a `REG_NET_004`, que exige exclusividade partidária literal, produziu **19 marcações
em 711.796 despesas**. Ela persegue a interseção de "fornecedor recém-aberto" com "todos do
mesmo partido", e o segundo termo é raro porque a variável dominante é outra.

Também torna o par de estados diferentes o caso que vale olhar. Os maiores:

```
4 forn.  David Soares (PODE-SP)      <->  Marcos Soares (PSDB-RJ)
2 forn.  Alice Portugal (PCdoB-BA)   <->  Antônia Lúcia (MDB-AC)
2 forn.  Gleisi Hoffmann (PT-PR)     <->  Túlio Gadêlha (PSD-PE)
```

E o par extremo da base, de longe: **Leonardo Gadelha e Ruy Carneiro, ambos PODE-PB, com
onze fornecedores em comum e R$ 1,93 milhão.**

## 5. Como foi construído

**Domínio** — `Rede/CriteriosDaRede.cs`: os três números (2, 5, R$ 50 mil), o limiar de
densidade, e as três frases que acompanham o mapa na tela. Dez testes, e os comentários
carregam a medição inteira: quem mudar o teto precisa ler por que ele é cinco.

**Leitura** — `ConsultasDaRedeEfCore`: três consultas, nessa ordem. Primeiro o agrupamento
por CNPJ **no banco**, que é o que impede trazer 711 mil linhas para a memória; depois
quem cada sobrevivente atende; depois os cadastros. Os pares são formados em memória, sobre
621 arestas de no máximo cinco pontas.

**API** — `/api/v1/rede`, com cache de seis horas. Não é otimização: montar o mapa varre a
tabela de despesas inteira, 2,7 segundos a frio.

**Tela** — `/rede`, "Quem contrata com quem". Um quadro por agrupamento; as 21 duplas em
lista; a tabela dos 621 fornecedores como gêmeo do desenho.

## 6. O que a tela pegou e o teste não

Três correções, todas de leitura e nenhuma de estética:

**O forasteiro era metade do grupo.** A primeira versão marcava quem não fosse da UF mais
frequente. Num agrupamento de nove cearenses com cinco goianos, isso acendia cinco anéis de
destaque — e cinco pessoas de um estado não são forasteiras, são a outra metade do grupo. A
regra virou **único do seu estado**, que é o caso raro de verdade: a Gleisi Hoffmann (PR)
entre treze pernambucanos.

**O quadro grande virou uma faixa vertical.** Dar duas colunas ao agrupamento de catorze
dobrou também a altura, porque o viewBox era quadrado. O quadro grande passou a ser
**largo** (520 × 292), e não maior.

**Os rótulos se sobrepunham no miolo** — justamente onde estão as pessoas mais ligadas, as
que o leitor mais quer identificar. A simulação de forças separa *retratos*, de 44 unidades;
o que colide é o par de linhas de texto embaixo, que chega a 52. Entrou uma passada de
separação de retângulos com a largura **medida por nó**, e `nomeCurto` mudou de lugar para o
arranjo medir exatamente o texto que será desenhado.

## 7. O arranjo é determinístico, de propósito

A página é estática. Uma simulação de forças com posições iniciais sorteadas desenharia a
mesma pessoa em lugar diferente a cada build, sem nenhum dado ter mudado — e ninguém
notaria até comparar duas capturas de tela.

Posições iniciais pelo índice no ângulo áureo, número de passos fixo, e um teste que compara
duas execuções.

## 8. O que esta etapa se recusou a fazer

- **Desenhar as arestas de fornecedor muito compartilhado**, mesmo sendo dados reais.
- **Chamar a página de "mapa de poder"**: "poder" já é Executivo/Legislativo/Judiciário no
  vocabulário do produto, e o nome prometeria influência.
- **Traduzir o CNAE.** Não temos tabela de descrição, e escrever "publicidade" a partir de
  um prefixo seria inventar a leitura do código.
- **Usar vermelho em qualquer destaque.** Aqui não há gravidade a comunicar.

## 9. Números

| | |
|---|---:|
| Testes de domínio novos | 10 |
| Testes de integração novos | 9 |
| Testes E2E novos | 8 |
| Testes de frontend novos | 35 |
| Total no repositório | 882 backend · 241 frontend |
| Páginas estáticas no build | 864 |
