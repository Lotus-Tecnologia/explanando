# Etapa 11 — O grafo do Estado

**Data:** 2026-09-19
**Situação:** ✅ concluída (metade das estruturas; a metade das pessoas está bloqueada)
**Decisões:** [ADR-0016](../decisoes/ADR-0016-grafo-do-estado.md)
**Contrato:** [`siorg.md`](../contratos/siorg.md)

> **O que foi construído.** A adaptação ao Brasil do
> [CivLab](https://graph.civlab.org/us): um mapa navegável da administração pública federal
> com **96.658 unidades**, a hierarquia inteira, a base legal de cada uma e um rastreador de
> mudanças estruturais.

---

## 1. O ponto de partida, e por que ele mudou

A pergunta do produto até aqui era "quanto se gastou". Esta etapa acrescenta outra: **"que
órgãos existem, de quem cada um depende, sob que norma — e o que andou mudando"**. São dois
contextos, e o [ADR-0016](../decisoes/ADR-0016-grafo-do-estado.md) registra por que eles não
se misturam.

## 2. O que a medição desmentiu

Três afirmações minhas anteriores caíram, e as três caíram por medição:

**"O rastreador de mudanças vem quase de graça."** Errado. O SIORG traz `operacao`,
`codigoUnidadePaiAnterior` e `dataFinalVersaoConsulta` no esquema, mas os três chegam
**vazios** nas 122 unidades da Presidência, e os parâmetros `versaoConsulta`, `dataConsulta`
e `incluirHistorico` são aceitos e **ignorados**. Não há histórico: a mudança tem de ser
deduzida comparando coletas.

**"São ~19.300 unidades."** Errado por um fator de cinco — **são 96.658**. A estimativa saiu
de uma amostra de doze órgãos, com média 57,7 e mediana 10,5. Doze sorteios não alcançam as
caudas de uma distribuição em que um único órgão (o Ministério da Educação) tem 47.946
unidades abaixo dele. A lição: **amostra pequena não mede distribuição torta**.

**"O tamanho não é problema."** Esta se sustentou, e por acaso: eu errei o número de
unidades para menos e os bytes por unidade para mais. O resultado real são **22 MB** —
14 MB de dados e 8 MB de índices —, contra os 255 MB que as despesas já ocupam.

## 3. A armadilha que teria produzido dado falso

A primeira varredura rodou com seis threads, como as outras fontes do projeto. Ela terminou
em **nove segundos** relatando 2.334 unidades e **zero erros**.

Nada disso era verdade. O SIORG aceita **uma requisição por IP de cada vez** e recusa as
demais com `codigoErro 119` — *"Este IP tem uma requisição em andamento"* — respondendo
**HTTP 200**. Das 335 subárvores, **329 voltaram recusadas**, e um cliente que olhasse o
status HTTP as teria registrado como "sucesso, zero unidades".

Isso não seria só uma coleta ruim. Seria uma coleta **lida como completa**, e coleta
completa é justamente a que tem licença para marcar órgãos como desaparecidos — o produto
teria publicado "94 mil unidades deixaram de constar na estrutura oficial".

A correção tem três partes, e as três estão no cliente: uma trava que serializa as chamadas,
a leitura do `codigoErro` em vez do status HTTP, e a distinção entre a recusa que passa
(119, insiste) e a que não passa (104, não insiste — é a resposta correta a pedir a árvore
de um `ente`).

## 4. As duas políticas que governam a coleta

**A primeira coleta é linha de base.** Gravar 96.658 linhas de "passou a constar na
estrutura oficial" no dia um seria verdade e seria inútil. Consequência que a interface
carrega: lista de mudanças vazia tem dois significados opostos — "ainda não comparamos" e
"comparamos e nada mudou" —, e o mapa devolve `temHistorico` para a tela dizer qual é. É o
erro dos zeros do [ADR-0009](../decisoes/ADR-0009-dois-numeros-nao-um.md) noutra tabela.

**Coleta incompleta nunca conclui desaparecimento.** Uma subárvore que falha some da
resposta exatamente como sumiria se tivesse sido extinta. Só uma coleta em que nenhum órgão
falhou pode marcar ausência — e mesmo então o vocabulário é literal: `Desaparecida`, não
"extinta", porque a fonte não distingue extinção, incorporação e "o cadastro parou de
publicar". A unidade permanece na base, inativa, com a última fotografia que tivemos dela.

## 5. As duas coletas reais

| | 1ª coleta | 2ª coleta |
|---|---|---|
| Órgãos visitados | 330 | 330 |
| Órgãos com falha | **0** | **0** |
| Fora do escopo (esfera) | 5 | 5 |
| Unidades vistas | 96.658 | 96.658 |
| Novas | 96.658 | **0** |
| Atualizadas / desaparecidas | 0 / 0 | **0 / 0** |
| Repetições por recusa | 0 | 0 |
| Tempo de varredura | 8m52s | 8m49s |

> **A segunda coleta é a prova.** Duas varreduras independentes da fonte ao vivo,
> atravessando o domínio inteiro, produziram **zero diferenças**. É o que mostra que a
> comparação funciona — e que ela não inventa mudança onde não houve.

## 6. O Estado, medido

**96.658 unidades**, em 22 MB.

| Corte | Números |
|---|---|
| **Poder** | Executivo 95.687 (99,0%) · Judiciário 743 · Funções Essenciais 125 · Legislativo 101 · sem classificação 2 |
| **Tipo** | Unidade administrativa 94.310 (97,6%) · Colegiado 2.012 · Entidade 222 · Órgão 108 · Ente 6 |
| **Base legal** | **Ato interno 92.244 (95,4%)** · Lei ou decreto 4.414 (4,6%) |

> **O número que vale a manchete:** 95,4% das unidades da administração federal existem por
> **ato interno**, não por lei ou decreto. São 92.244 caixas que somem com uma assinatura.
> O produto não diz que isso é bom nem ruim — diz quantas são, o que ninguém dizia antes.

A árvore, do centro para fora:

```
República Federativa do Brasil          96.657 abaixo
 └─ União                               96.656
     ├─ Poder Executivo                 95.474
     │   └─ Presidência da República    95.473
     │       ├─ Ministério da Educação  47.946
     │       ├─ Ministério da Defesa      6.398
     │       ├─ Ministério da Fazenda     4.568
     │       └─ …mais 53 ministérios
     ├─ Poder Judiciário                   742
     ├─ Funções Essenciais à Justiça       124
     └─ Poder Legislativo                  100
```

## 7. As escolhas de desenho, e o que cada uma custa

**Só a esfera federal.** A lista do SIORG traz o Governo do Distrito Federal e mais quatro
órgãos estaduais — 5 dos 335. Ficam de fora por escolha explícita; `--todas-as-esferas` os
traz.

**O mapa mostra 336 nós, não 96.658.** Entes, órgãos e entidades. Desenhar as 94.310
unidades administrativas produz uma nuvem cinza. O detalhe desce um nível por clique.

**A largura da fatia usa raiz quadrada, e a tela diz isso.** O Executivo tem 95.687
unidades contra 101 do Legislativo: numa escala direta, três dos quatro poderes viram fatias
de menos de um grau e somem. Com a raiz, o Executivo ainda ocupa 86% da volta e os outros
três continuam visíveis. **Comprimir uma escala sem avisar seria mentir** — a legenda do
desenho declara a compressão, e os números exatos aparecem na legenda, na lista e no
`title` de cada fatia.

**Os oito órgãos órfãos.** Medido: os comandos do Exército, da Marinha e da Aeronáutica são
*unidades administrativas* e ficam **entre** o Ministério da Defesa e oito órgãos abaixo
deles. Como unidade administrativa não entra no mapa, esses oito virariam raízes soltas e
sumiriam do desenho em silêncio. A consulta devolve `codigoPaiNoMapa` — o ancestral mais
próximo que também está no mapa — num campo **separado** de `codigoPai`, porque os dois
significam coisas diferentes e sobrepô-los faria a página da unidade mentir sobre a quem ela
responde.

**O desenho para no quinto anel.** A base tem 216 nós no quinto nível e cinco no sexto.
Esses cinco viravam agulhas de um décimo de grau atravessando a borda do disco — ruído que
fazia o desenho parecer quebrado. Continuam na lista e a um clique.

## 8. O que foi construído

**Domínio** — `UnidadeDoEstado` (hierarquia, quatro poderes, esfera, natureza jurídica,
nível de normatização, `Ativa`), `MudancaEstrutural` com o nome congelado na época, e
`VocabularioDoEstado`, que traduz cada classificação para a tela. As invariantes são poucas
de propósito: o SIORG é cadastro oficial, e validar demais recusaria registro legítimo.
Valida-se o que quebraria o grafo — nó sem identidade, nó sem rótulo, aresta apontando para
si mesma.

**Aplicação** — `IFonteDaEstruturaDoEstado`, `IRepositorioDoEstado` e
`ServicoDeColetaDoEstado`, que percorre, resolve ancestrais, compara e conta.

**Infraestrutura** — `ClienteSiorg` (trava de uma requisição por vez, releitura do
`codigoErro`, repetição só do que passa), `ConversorDoSiorg` (as duas grafias de cada
código), as tabelas `unidades_do_estado` e `mudancas_estruturais`, e o lado de leitura com
cache de seis horas — que aqui não é otimização: montar o mapa custa carregar 96 mil
arestas.

**API** — `/api/v1/estado`, `/estado/{codigo}`, `/estado/{codigo}/unidades`,
`/estado/mudancas`.

**Interface** — `/estado` com o desenho radial em SVG puro (**nenhum JavaScript**, montado
no servidor), três painéis de distribuição, a linha do tempo e a lista dos 330; e
`/estado/{codigo}` com a ficha, o link para quem manda nela e os filhos diretos. Menu novo,
separado das duas casas por uma barra — o Estado não é uma terceira casa legislativa.

As **336 páginas de órgão viram arquivo no build**, junto com os 513 perfis, e entram no
sitemap. É a premissa de custo do [ADR-0001](../decisoes/ADR-0001-frontend-nextjs.md): a CDN
só serve o que já existe como arquivo. As outras 96 mil unidades continuam sob demanda —
ninguém compartilha o link de uma coordenação de terceiro nível.

## 9. Testes

| Nível | Novos | Total do projeto |
|---|---|---|
| Domínio | 30 | 475 |
| Aplicação | 10 | 64 |
| Integração | 4 (+12 de contrato) | 195 |
| Ponta a ponta | 10 | 57 |
| Frontend | 12 | 136 |

Tudo verde, build com 0 avisos.

As fixtures congeladas foram escolhidas pelas armadilhas, não pelo caminho feliz: a raiz sem
poder e sem esfera, as duas grafias do mesmo código, o órgão de outra esfera, a recusa que
se repete e a recusa que não se repete.

## 10. O que falta, e não é por escolha

**As pessoas.** A referência mostra cadeiras vagas e ocupantes interinos. O SIORG **não
publica cargo nem titular** — não há campo, em nenhum endpoint. Esse dado vive no Diário
Oficial, e o acesso estruturado a ele é o [INLABS](https://inlabs.in.gov.br/), que exige
cadastro com nome, e-mail, telefone e UF. **É um cadastro que só o Gustavo pode fazer.**

A página declara essa ausência em vez de deixar o mapa parecer completo.

**Agendar a coleta.** A varredura é sequencial por natureza da fonte e leva ~9 minutos.
Cabe no mesmo Cloud Run Job do `ciclo-mensal`, mas o agendamento depende do deploy, que está
em [`pendencias.md`](../pendencias.md) §1.

**A coleta ainda é local.** As 96.658 unidades estão no Postgres do `docker-compose`. Levar
para a Neon é uma decisão do Gustavo — os 22 MB cabem com folga nos 512 MB do plano
gratuito, mas a base de lá já usa 255 MB com as despesas.
