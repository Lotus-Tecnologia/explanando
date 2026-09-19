# Etapa 12 — Quem trabalha no Estado

**Data:** 2026-09-19
**Situação:** ✅ concluída
**Decisões:** [ADR-0016 §7](../decisoes/ADR-0016-grafo-do-estado.md)
**Contrato:** [`pessoal.md`](../contratos/pessoal.md)

> **O que foi construído.** O grafo do Estado deixa de ser só caixas: cada órgão passa a
> dizer **quanta gente trabalha nele** e **quantos ocupam função de confiança**.
> 759.351 vínculos, 95,5% ligados a um nó do mapa — e **nenhum nome no banco**.

---

## 1. A etapa começou desmentindo a anterior

A [Etapa 11](etapa-11-grafo-do-estado.md) fechou dizendo que as pessoas do grafo dependiam
de um cadastro no INLABS, que só o Gustavo pode fazer. **Estava errado**, e a medição veio
depois de o resto já estar pronto: o Portal da Transparência publica o cadastro completo
dos servidores federais **mensalmente, em dado aberto, sem login nenhum**.

O que de fato depende do INLABS é outra coisa — a *linha do tempo de nomeações e
exonerações*, que vive no Diário Oficial. Tratar as duas como uma só foi o erro, e ele
custou o intervalo entre as duas etapas.

## 2. A decisão que define esta etapa: contagens, não pessoas

O arquivo traz **778.337 vínculos nominais**, com nome e CPF mascarado, em 444 MB de CSV. A
pergunta que o produto faz — "quanta gente trabalha em cada órgão" — se responde com somas.

Guardar a lista nominal traria **dado pessoal em escala**, 444 MB por mês e uma avaliação de
legítimo interesse a fazer antes de qualquer linha de código, para entregar **exatamente a
mesma tela**.

Então a agregação acontece **durante a leitura**, e a arquitetura torna o contrário
impossível em três camadas:

- **A borda** (`VinculoDaFonte`) expõe três campos dos 43: código do órgão, nome do órgão e
  sigla da função. O nome e o CPF são lidos e descartados na mesma linha.
- **O fluxo** é `IAsyncEnumerable`, nunca uma lista. Não há ponto onde 778 mil registros
  existam juntos na memória.
- **A entidade** (`LotacaoDoOrgao`) não tem onde guardar uma pessoa — e há um teste que
  **quebra o build** se alguém acrescentar um campo chamado `Nome`, `Cpf`, `Matricula` ou
  parecido.

O que chega ao banco são **351 linhas por mês**.

## 3. O trabalho de verdade foi a ponte

Os dois cadastros do Estado **não compartilham chave**. O SIAPE identifica o órgão pelo
código SIAFI (`25201` = Banco Central); o SIORG, pelo `codigoUnidade` (`26` = Presidência).
Não existe tabela oficial ligando os dois — procurei: o parâmetro `codigoSIAFI` é aceito e
ignorado pela API do SIORG, e não há campo de código SIAFI em resposta nenhuma.

A ponte é por nome, e cada passo foi medido:

| Regra | Órgãos ligados | Cobertura |
|---|---|---|
| Nome idêntico | 145 | 58,5% |
| + normalizar acento, caixa, pontuação e o sufixo de UF | 161 | 63,6% |
| + duas famílias: `Fundação Universidade…` e o nome longo dos institutos federais | 182 | 70,6% |
| + buscar entre **todas** as 96.658 unidades, exigindo alvo único | 225 | 88,8% |
| + 20 exceções curadas uma a uma contra a base | **243** | **95,5%** |

> **Nenhum passo usa aproximação**, e isso é a regra e não o acaso. Nada de distância de
> edição, nada de "parecido o bastante": um par errado aqui não produz erro visível —
> produz o quadro de pessoal de um órgão exibido dentro de outro, com cara de fato. Nome que
> casa com dois nós não casa com nenhum.

A ordem da busca importa e está testada: **órgãos de topo primeiro**, todas as unidades
depois. "Secretaria-Executiva" existe dezenas de vezes na base, e procurar entre todas de
saída faria um ministério casar com a secretaria de outro. É também assim que os comandos
do Exército, da Marinha e da Aeronáutica — que são *unidades administrativas* — encontram
par.

As 20 exceções curadas caem em quatro famílias, e a família está no comentário de cada
linha porque é ela que diz se a entrada continua válida quando a fonte mudar: nome truncado
em caixa alta pelo SIAPE, órgão renomeado, erro de digitação na origem ("Função Escola
Nacional…"), e abreviação. É a mesma natureza da tabela CNAE da Etapa 10 — **curadoria
minha, medida, e que merece uma revisão do Gustavo.**

## 4. Os 4,5% que sobram são ausência real

| Órgão | Vínculos | Por quê |
|---|---|---|
| Hospital Nossa Senhora da Conceição | 13.763 | Não existe no SIORG |
| Governos dos ex-territórios (AP, RR, RO) | 18.801 | Folhas herdadas; não são órgãos federais atuais |

Mais **18.986 vínculos** em rubricas de totalização — "Estados / Municípios / Empresas" e
"Poder Executivo Federal", que tem código **negativo** (`-20`). Elas nunca poderiam casar,
porque não são órgãos, e por isso ficam **fora do denominador**: contá-las contra a ponte
mediria a fonte, não a qualidade do nosso trabalho.

## 5. Dois defeitos que só um teste pegaria

**`string.Normalize` é inerte neste projeto.** O caminho natural para tirar acento é
`Normalize(FormD)` e descartar as marcas de combinação. Aqui isso **não funciona e não
falha**: o `Directory.Build.props` liga `InvariantGlobalization` — decisão da Etapa 00, que
também dá as imagens *chiseled* sem ICU —, e sem ICU o método devolve a string intacta,
silenciosamente. Os testes da ponte acusaram `"MINISTÉRIO MEIO AMBIENTE"` onde se esperava
`"MINISTERIO MEIO AMBIENTE"`. Sem eles, alguns órgãos simplesmente não casariam e a
cobertura cairia sem ninguém saber por quê. A tabela de acentos passou a ser escrita à mão.

*(Auditei o resto do código: nenhum outro ponto depende de `Normalize` ou de comparação
sensível a cultura.)*

**`Competencia` não se decompõe em LINQ.** Ela chega ao banco por um `ValueConverter`, e
`Where(l => l.Competencia.Ano * 100 + l.Competencia.Mes == x)` **compila** e estoura em
tempo de execução. Apareceu na primeira coleta real, depois de ler as 778 mil linhas.

## 6. A coleta real

```
=== QUADRO DE PESSOAL — 2026-07 ===
Vinculos lidos        : 778337
Orgaos encontrados    : 351
  ligados ao grafo    : 243
  por tabela curada   : 20
Agregados descartados : 18986
Vinculos de orgao     : 759351
  ligados ao grafo    : 724839
Cobertura             : 95.5 %
Lotacoes gravadas     : 351
Tempo                 : 00:00:07
```

**Sete segundos** para 83 MB baixados, 444 MB descompactados e 778 mil linhas agregadas.

O que o grafo passou a mostrar:

```
República Federativa do Brasil      724.839 no ramo
 └─ Presidência da República        723.106 no ramo · 4.820 próprios · 3.333 em função
     ├─ Ministério da Educação      407.418 no ramo · 2.904 próprios
     └─ Instituto Nacional do Seguro Social   22.473 · 4.119 em função de confiança
```

## 7. Nulo nunca vira zero

96 dos 336 nós do mapa não têm quadro de pessoal, e a razão é dupla: a ponte não alcança
tudo, e **o SIAPE é a folha do Executivo civil** — o Judiciário e o Legislativo mantêm
sistemas próprios e aparecem só em parte (11 dos 60 órgãos do Judiciário).

A tela escreve **"sem dado"**, nunca "0". Escrever zero afirmaria que um tribunal não tem
servidor nenhum — uma limitação nossa virando um fato sobre o Estado. É o
[ADR-0009](../decisoes/ADR-0009-dois-numeros-nao-um.md) noutra tabela, e há teste de ponta a
ponta para isso.

A página `/estado` diz isso em texto, ao lado do número: de qual poder é o cadastro, qual a
cobertura da ponte, e por que o resto aparece vazio.

## 8. O que foi construído

**Domínio** — `PonteSiape` (normalização, baldes, exceções curadas) e `LotacaoDoOrgao` com
`ContagemPorFuncao`, mais a invariante que impede a entidade de guardar uma pessoa.

**Aplicação** — `IFonteDePessoal` (fluxo de três campos), `IRepositorioDePessoal` e
`ServicoDeColetaDePessoal`, que agrega, liga e conta a cobertura.

**Infraestrutura** — `ClienteDoPortalDaTransparencia` (baixa, descompacta, lê Latin-1 com
separador de campo que respeita aspas), duas tabelas, migration, e a leitura que soma o
pessoal por ramo junto com a contagem de unidades.

**Worker** — `coletar-pessoal --ano --mes`, que imprime a cobertura e **lista os maiores
órgãos sem par**: um ministério nessa lista é sinal de que a ponte precisa de mais uma
entrada.

**Interface** — o número no alto de `/estado`, um painel "Quem trabalha no Estado" com a
cobertura e a ressalva do poder, a contagem por órgão na lista, e em `/estado/{codigo}` os
servidores do ramo, os próprios e os em função de confiança.

## 9. Testes

| Nível | Novos | Total |
|---|---|---|
| Domínio | 28 | 503 |
| Aplicação | 11 | 75 |
| Ponta a ponta | 3 | 60 |
| Frontend | — | 149 |

843 no backend, tudo verde, build com 0 avisos.

## 10. O que ainda falta

| O que | Situação |
|---|---|
| **Nomeações e exonerações no tempo** | DOU, via [INLABS](https://inlabs.in.gov.br/). Exige cadastro que só o Gustavo pode fazer — e é a única coisa do CivLab que ainda está nesse estado |
| **Cadeiras vagas** | Nenhuma fonte publica os cargos **autorizados** por órgão, que é o denominador sem o qual não existe conta de vaga |
| **Série histórica** | A coleta é por competência e a tabela já suporta várias. Falta rodar os meses anteriores e desenhar a evolução |
| **Revisão da ponte** | As 20 exceções são curadoria minha. Merecem os olhos do Gustavo, como a tabela CNAE da Etapa 10 |
