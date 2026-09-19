# ADR-0017 — O mapa de pessoas usa a aresta que temos, não a que a referência usa

**Data:** 2026-09-19
**Situação:** aceito
**Contexto anterior:** [ADR-0004](ADR-0004-nomenclatura-tecnica.md),
[ADR-0007](ADR-0007-postura-permissiva-do-motor.md),
[ADR-0013](ADR-0013-identidade-visual-e-taxonomia-de-risco.md),
[ADR-0016](ADR-0016-grafo-do-estado.md)

---

## Contexto

O [CivLab](../referencias/civlab.md) tem duas vistas: o **Graph**, de instituições, que o
ADR-0016 adaptou; e o **Power map**, de pessoas. A definição do Power map é literal e está
escrita na tela deles:

> *"The 20 people named most across 1,005 articles from 3 sources in the last 90 days"*

Retratos circulares, anel colorido pelo partido, tamanho proporcional ao número de artigos.

**Não temos como reproduzir isso, e comprar a fonte não resolveria.** Faltam duas coisas: um
provedor de notícias e o *entity linking* que liga "o ministro Haddad" ao nó certo. Mas o
problema maior não é de engenharia: uma nuvem de citações mede **a imprensa**, não o Estado.
O nosso produto existe para medir dado público primário.

A pergunta que sobreviveu é a boa: **existe uma vista de pessoas que a nossa base sustenta?**

## O que foi medido

A candidata óbvia é fornecedor em comum. Antes de desenhar qualquer coisa, a base inteira
foi contada:

| | |
|---|---|
| Fornecedores na base | **41.105** |
| Atendem 2 ou mais parlamentares | **15.272** |
| Atendem mais de 50 | **96** |

E os mais compartilhados, por CNPJ:

```
TAM Linhas Aéreas             500 parlamentares
CASCOL Combustíveis           417
Auto Posto Cinco Estrelas     369
WMS Papelaria                 313
Telefônica                    258
```

**Um grafo com essas arestas ligaria quase o Congresso inteiro por um fato que é geografia
de Brasília, e não arranjo.** Cada linha desenhada é uma afirmação ao leitor, e essas
afirmariam relações que não existem.

O sinal está no extremo oposto: fornecedor que atende **poucos** gabinetes e movimenta
**muito**.

| Recorte: 2 a 5 parlamentares, R$ 50 mil ou mais | |
|---|---|
| Fornecedores | **621** |
| Valor | **R$ 140,1 milhões** |
| Parlamentares alcançados | **463** |
| Pares formados | **1.386** |

Os maiores não são posto de gasolina: FRAME Comunicação Digital (4 parlamentares, R$ 1,55
mi), Orbe Produtora (2, R$ 1,35 mi), Pontes Comunicação (2, R$ 1,30 mi), Instituto Mais
Cidades (4, R$ 1,02 mi). Agências, consultorias e MEs.

**Mas 1.386 pares ainda não são um mapa.** Medido: **454 dos 463 parlamentares caem num
único componente conexo**. Basta uma corrente de fornecedores para ligar quase todo mundo, e
um desenho em que "estar conectado" vale para 98% das pessoas não distingue nada.

Exigindo **dois** fornecedores em comum, sobram **247 pares em 49 agrupamentos separados**,
de 2 a 14 pessoas. Isso é um mapa.

## Decisões

### 1. A aresta é fornecedor em comum, não citação em notícia

Um fato da base, com CNPJ, nota fiscal e valor — verificável linha a linha.

### 2. O corte é 2 a 5 parlamentares e R$ 50 mil, e vive no domínio

`Domain.Rede.CriteriosDaRede`, com a medição inteira escrita nos comentários e dez testes
que a protegem. Não é parâmetro de tela nem constante de repositório: é uma afirmação sobre
o que o dado significa, e afirmação dessas tem dono e tem teste.

O teto é o que transforma o emaranhado em mapa. Cinco, e não três ou dez, porque a
distribuição tem um vale largo ali: 12.753 dos 15.272 compartilhados atendem de dois a
cinco, e 96 atendem mais de cinquenta.

### 3. O desenho usa pares densos — dois fornecedores em comum, no mínimo

`FornecedoresParaParDenso = 2`. Um fornecedor em comum é coincidência; dois é o menor padrão
que se pode desenhar sem prometer mais do que o dado tem. Os 1.139 pares que ficam de fora
**são contados na tela**, para o leitor saber o tamanho do recorte.

### 4. A ressalva viaja dentro do JSON, não ao lado do desenho

`MapaDaRede.Ressalva` chega junto com os dados:

> *"Fornecedor em comum não é indício de irregularidade. Dois gabinetes contratarem a mesma
> empresa é lícito e comum."*

Quem consumir a API por fora recebe o aviso junto com os números, e não tem como desenhar o
grafo sem tê-lo visto. Na página, a frase vem **antes** do desenho: um mapa de pessoas
ligadas por dinheiro é lido como acusação por padrão, e a correção só funciona se chegar
primeiro (ADR-0004).

### 5. O achado geográfico entra no contrato, e não é deduzido na tela

Dos 247 pares densos, **91% são do mesmo estado** e apenas **19% do mesmo partido**. A
explicação dominante é o mercado local, não a política.

Isso não é decepção, é informação — e é a razão de a `REG_NET_004`, que exige exclusividade
partidária literal, produzir **19 marcações na base inteira**. Torna também o par de estados
diferentes o caso raro que vale olhar, e por isso `MesmaUf` é campo do contrato: o desenho
precisa poder destacá-lo.

### 6. Nenhum vocabulário de risco atravessa para cá

Como no ADR-0016: sem gravidade, sem flag, sem índice. O destaque visual do caso raro é o
acento, **nunca o vermelho**, que o ADR-0013 reserva à gravidade alta.

### 7. O nome da página não é "mapa de poder"

"Poder" já significa Executivo, Legislativo e Judiciário no vocabulário do produto, e o nome
prometeria influência quando o que o dado mostra é fornecedor em comum. A página se chama
**"Quem contrata com quem"**, e vive em `/rede`.

## Consequências

**A vista de pessoas existe, e é nossa.** 224 parlamentares em 49 ilhas, com retrato, partido
e UF — a mesma leitura que o Power map oferece, sobre um dado que podemos defender.

**Ela custa uma varredura da tabela de despesas.** Montar o mapa agrupa as 711 mil despesas
por CNPJ: 2,7 segundos a frio. O cache de seis horas é requisito, não otimização.

**O que continua faltando é o mesmo de antes.** Esta é a vista de pessoas *do Congresso*. Os
ocupantes de cargo do Executivo seguem travados no DOU, e nenhuma decisão aqui os destrava.

**Se a distribuição mudar, o corte precisa ser remedido.** Os números deste documento são de
2026-09-19, sobre 711.796 despesas. Um mandato novo, com fornecedores novos, pode mover o
vale onde o teto de cinco está apoiado — e os testes de domínio guardam o valor escolhido,
não a razão dele.
