# Contrato — o quadro de pessoal do Executivo federal

Levantado em 2026-09-19 contra a origem real.

É a fonte da **metade das pessoas** do grafo do Estado. Ela responde "quanta gente trabalha
em cada órgão, e quantos ocupam função de confiança".

Base: `https://dadosabertos-download.cgu.gov.br/PortalDaTransparencia/saida/servidores/`

---

## 1. Não exige cadastro, e isso corrige o que a Etapa 11 disse

A [Etapa 11](../registro/etapa-11-grafo-do-estado.md) afirmou que as pessoas do grafo
dependiam de um cadastro no INLABS. **Está errado para esta metade.** O Portal da
Transparência publica o cadastro completo mensalmente, em dado aberto, sem login, sem token
e sem termo de uso a aceitar.

O que **de fato** depende do INLABS é a *linha do tempo de nomeações e exonerações*, que
vive no Diário Oficial. São coisas diferentes, e tratá-las como uma só atrasou esta etapa.

## 2. O arquivo

```
{AAAAMM}_Servidores_SIAPE.zip
```

Medido em `202607`: **83 MB compactados**, quatro CSVs dentro.

| Arquivo | Tamanho | Para quê |
|---|---|---|
| `{AAAAMM}_Cadastro.csv` | **444 MB** | É o que usamos: 778.337 vínculos, 43 colunas |
| `{AAAAMM}_Remuneracao.csv` | 168 MB | Remuneração individual. **Fora de escopo** |
| `{AAAAMM}_Afastamentos.csv` | 5,9 MB | Afastamentos nominais |
| `{AAAAMM}_Observacoes.csv` | 3,5 MB | Texto livre |

> **A URL do navegador redireciona.** `portaldatransparencia.gov.br/download-de-dados/
> servidores/{AAAAMM}_Servidores_SIAPE` responde **302** para o endereço acima. O cliente
> vai direto ao destino final — um redirecionamento a menos por coleta, e um ponto a menos
> onde a origem pode mudar de forma.

> **Competência inexistente responde `403`, não `404`.** O arquivo de um mês só aparece
> semanas depois do fechamento dele: o de julho de 2026 foi publicado em **3 de setembro**.
> Por isso o comando do worker exige `--ano` e `--mes` e não tem padrão — adivinhar a
> competência daria 403 sem dizer por quê.

> **E `405` quer dizer outra coisa: a origem está recusando por volume.** Medido numa carga
> de 19 meses — os dez primeiros baixaram normalmente, e do décimo primeiro em diante tudo
> voltou `405`, **inclusive um mês que tinha baixado minutos antes**. Depois disso, até o
> `HEAD` que respondia `200` passou a responder `405`: o bloqueio é do IP, não do arquivo.
>
> São 83 MB por competência, e ~1 GB puxados em poucos minutos. A carga inicial precisa ser
> feita **em lotes pequenos, com folga entre eles** — o worker já espera cinco segundos
> entre meses, e isso não bastou. Confundir `405` com `403` mandaria alguém procurar um
> arquivo que existe.

## 3. As colunas que atravessam — e as que param na borda

O `Cadastro.csv` tem **43 colunas**, entre elas `NOME` e `CPF` (mascarado na origem, como
`***.056.281-**`). **Três chegam à aplicação:**

| Coluna | Vira o quê |
|---|---|
| `COD_ORG_EXERCICIO` | O órgão, em código SIAFI |
| `ORG_EXERCICIO` | O nome do órgão — é o que a ponte usa |
| `SIGLA_FUNCAO` | A função de confiança, ou `-1` |

As outras 40 são lidas e descartadas na mesma linha. `VinculoDaFonte` não tem onde guardar
nome nem CPF, e a agregação acontece **durante** a leitura: o que chega ao banco são ~350
contagens por mês, e nenhum registro de pessoa.

## 4. Formato

- **Latin-1**, não UTF-8. Lido como UTF-8, "Ministério" vira caractere de substituição e a
  ponte por nome deixa de casar — sem erro nenhum.
- Separador **`;`**, campos entre aspas.
- `SIGLA_FUNCAO = "-1"` significa "sem função". Não é uma função chamada `-1`.

## 5. O que os números dizem, medido em 2026-07

**778.337 vínculos.**

| Situação | Vínculos |
|---|---|
| Ativo permanente | 490.422 |
| Celetista / empregado | 101.680 |
| Sigiloso | 35.731 |
| Residência e PMM | 26.014 |
| Contrato temporário | 21.044 |

**Funções de confiança:** FEX 31.188 · FG 28.509 · FUC 12.988 · CD 9.498 · CCX 8.925 ·
GF 5.133. Sem função: 638.049.

> **É o Executivo civil, e só ele.** O Judiciário e o Legislativo mantêm folhas próprias e
> aparecem aqui apenas em parte — 11 dos 60 órgãos do Judiciário têm quadro nesta fonte. Por
> isso a tela escreve "sem dado" e nunca "0" para um tribunal.

## 6. A ponte com o SIORG — o trabalho de verdade

**Os dois cadastros não compartilham chave.** O SIAPE identifica o órgão pelo código SIAFI
(`25201` = Banco Central); o SIORG, pelo `codigoUnidade` dele (`26` = Presidência). Não há
tabela oficial ligando os dois, e isso foi procurado: o parâmetro `codigoSIAFI` é aceito e
**ignorado** pela API do SIORG, e não há campo de código SIAFI em resposta nenhuma.

A ponte é por **nome**, com regra explícita em `PonteSiape`, e a evolução foi medida:

| Regra | Órgãos | Cobertura dos vínculos |
|---|---|---|
| Nome idêntico | 145 | 58,5% |
| + normalização (acento, caixa, pontuação, sufixo de UF) | 161 | 63,6% |
| + as duas famílias (`Fundação Universidade…`, o nome longo dos institutos federais) | 182 | 70,6% |
| + busca entre **todas** as unidades, exigindo alvo único | 225 | 88,8% |
| + 20 exceções curadas uma a uma | **243** | **95,5%** |

> **Nenhum passo usa aproximação.** Nada de distância de edição, nada de "parecido o
> bastante": um par errado aqui não produz erro visível — produz o quadro de pessoal de um
> órgão exibido dentro de outro, com a aparência de um fato. Nome que casa com dois nós não
> casa com nenhum.

**Os 4,5% que sobram são ausência real**, não falha da ponte:

| Órgão | Vínculos | Por quê |
|---|---|---|
| Hospital Nossa Senhora da Conceição | 13.763 | Não existe no SIORG |
| Governo do Ex-Território Federal do Amapá | 9.196 | Folha herdada; não é órgão federal atual |
| Governo do Ex-Território Federal de Roraima | 5.951 | Idem |
| Governo do Ex-Território Federal de Rondônia | 3.654 | Idem |

E mais **18.986 vínculos** em rubricas de totalização — "Estados / Municípios / Empresas"
(16.689) e "Poder Executivo Federal" (código `-20`). Elas **nunca** podem casar, porque não
são órgãos, e por isso ficam fora do denominador da cobertura: contá-las mediria a fonte,
não a qualidade da ponte.

## 7. Armadilhas

1. **`string.Normalize` não funciona neste projeto.** `InvariantGlobalization=true` no
   `Directory.Build.props` tira o ICU, e sem ICU o `Normalize` devolve a string **intacta,
   sem lançar exceção**. A tabela de acentos de `PonteSiape` é escrita à mão por isso. Foi
   um teste que pegou — sem ele, alguns órgãos simplesmente não casariam e a cobertura
   cairia sem explicação.

2. **`Competencia` não se decompõe em LINQ.** Ela chega ao banco por um `ValueConverter`;
   `Where(l => l.Competencia.Ano * 100 + l.Competencia.Mes == x)` compila e **estoura em
   tempo de execução**. Comparar com o valor inteiro resolve.

3. **O cache de fetch do Next serve o contrato antigo.** Uma implantação que acrescenta
   campo, com a resposta anterior ainda em cache, entrega `undefined` onde o tipo promete
   `number | null`. As telas usam `== null` frouxo de propósito.

4. **Nulo e zero.** Órgão sem par na ponte tem `null`, nunca `0`. É o ADR-0009 noutra
   tabela: `0` afirma que não há ninguém; `null` diz que não sabemos.

5. **A série tem buracos, e o gráfico precisa mostrá-los.** O bloqueio por volume deixa
   meses sem coletar. Um gráfico que ligue dois pontos distantes com linha cheia afirma uma
   evolução mensal que não foi medida — o desenho quebra a linha e marca o vão com
   pontilhado.
