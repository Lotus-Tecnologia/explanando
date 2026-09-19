# Contrato — SIORG, a estrutura organizacional do Estado

Levantado em 2026-09-18 e **corrigido em 2026-09-19** contra os endpoints reais, versão de
serviço **4.0.2**.

O SIORG é a fonte do **grafo do Estado** — a adaptação ao Brasil do
[CivLab](https://graph.civlab.org/us). Ele responde a pergunta "que órgãos existem, de quem
cada um depende e sob que norma" para a administração pública federal.

Base: `https://estruturaorganizacional.dados.gov.br/doc`

---

## 1. O que o SIORG dá — e o que não dá

**Dá:** entidades, hierarquia, poder, esfera, natureza jurídica, tipo de unidade e o nível
de normatização que criou cada uma.

**Não dá:** cargos nem pessoas. Nenhum campo de titular, nenhum de ocupante. Os números que
dão vida ao CivLab — *"6 cadeiras vagas, 86 interinos"* — não saem daqui.

> **E não saem todos do mesmo lugar.** Medido em 2026-09-19: **quem ocupa cada cargo** é
> público e dispensa cadastro — o Portal da Transparência publica 778.337 vínculos por mês
> em `dadosabertos-download.cgu.gov.br`, com cargo, função DAS/FCPE, situação e data de
> nomeação. O que trava a junção é que **o SIAPE identifica o órgão pelo código SIAFI e o
> SIORG pelo `codigoUnidade`**: casando por nome, batem 145 dos 354 órgãos, cobrindo 58,5%
> dos servidores.
>
> **As nomeações ao longo do tempo** é que vivem no DOU, cujo acesso estruturado é o
> [INLABS](https://inlabs.in.gov.br/) e exige cadastro. E **as cadeiras vagas** não estão em
> fonte nenhuma: ninguém publica os cargos autorizados por órgão.

> **Não há endpoint de histórico, e isto foi medido.** A unidade traz `operacao`,
> `codigoUnidadePaiAnterior` e `dataFinalVersaoConsulta` no esquema, mas nas 122 unidades da
> Presidência **os três vêm vazios**. Os parâmetros `versaoConsulta`, `dataConsulta` e
> `incluirHistorico` são aceitos e **ignorados** — as três consultas devolvem as mesmas 122
> unidades com `codigoErro: 0`.
>
> Consequência: o rastreador de mudanças estruturais exige **guardar instantâneos e
> comparar**. É o que `ServicoDeColetaDoEstado` faz, e a primeira coleta é linha de base —
> não produz notícia nenhuma.

## 2. Os endpoints que interessam

| Endpoint | O que devolve |
|---|---|
| `/orgao-entidade` | **335 órgãos e entidades** de uma vez, 326 KB. É a raiz da varredura |
| `/estrutura-organizacional?codigoUnidade={n}` | A **árvore inteira** sob um órgão ou entidade |
| `/unidade-organizacional/{n}` | Uma unidade só. É o único jeito de alcançar os `ente` |
| `/poder`, `/esfera`, `/natureza-juridica`, `/tipo-unidade` | As tabelas de referência |

> **`/orgao-entidade` sem parâmetro funciona; `/unidade-organizacional` sem parâmetro não.**
> O segundo responde `codigoErro: 100` — *"O(s) campo(s) Código deve(m) ser preenchido(s)"*.
> Parece detalhe e não é: é o que decide se a varredura começa por uma chamada ou por 335.

## 3. A raiz do grafo, que só aparece quando se sobe

Os 335 órgãos **não são raízes**. Todos têm pai, e os pais não estão na lista — são nós do
tipo `ente`, alcançáveis só um a um:

```
200000  República Federativa do Brasil   (pai vazio — a raiz de verdade)
 ├─ 200001  União
 │   ├─ 208613  Poder Executivo      → Presidência da República (26) e os ministérios
 │   ├─ 244320  Poder Legislativo
 │   ├─ 244321  Poder Judiciário     → 46 órgãos
 │   └─ 244322  Funções Essenciais à Justiça
 ├─ 208614  Distrito Federal         → Poder Executivo (208615) → GDF
 └─ 619492  Estado do Rio Grande do Sul
```

> **Os quatro poderes são nós de verdade, não uma classificação nossa.** É o que dá ao
> desenho radial uma raiz natural: o centro é a República, e os setores são os poderes,
> exatamente como a fonte os organiza.

## 4. Os campos de uma unidade

Todo código vem como **URI**, e o conversor precisa cortar no último `/`.

| Campo | Vira o quê no grafo |
|---|---|
| `codigoUnidade` | O nó |
| `codigoUnidadePai` | **A aresta.** É a hierarquia inteira |
| `codigoOrgaoEntidade` | A que órgão-raiz a unidade pertence |
| `codigoPoder` | **O setor** do gráfico radial |
| `codigoEsfera` | Federal, estadual/distrital ou municipal |
| `codigoNaturezaJuridica` | O tipo do nó |
| `codigoTipoUnidade` | Ente, entidade, órgão, unidade administrativa, unidade colegiada |
| `nivelNormatizacao` | **A base legal** — `LEI_DECRETO` ou `ATO_INTERNO` |
| `nome`, `sigla` | O rótulo |

## 5. As tabelas de referência, medidas

**Poder**, entre os 335 órgãos: Executivo 270, Judiciário 59, Legislativo 3, Funções
Essenciais à Justiça 3.

**Esfera:** Federal 330, Estadual/Distrital 5, Municipal 0.

> O Governo do Distrito Federal está na lista. **O SIORG não é só federal**, e a ingestão
> decide explicitamente o que entra — hoje, só a esfera federal. Sem essa escolha, o grafo
> do Estado brasileiro ganharia nós que não pertencem à União sem ninguém ter decidido.

**Natureza jurídica:** os códigos são 1, 2, 3, 4, **6 e 7** — o 5 não existe, e a lacuna é
da fonte.

**Tipo de unidade:** `ET` Ente, `EN` Entidade, `OR` Órgão, `UA` Unidade Administrativa,
`UC` Unidade Colegiada.

## 6. O tamanho — corrigido

A primeira estimativa, de **~19.300 unidades**, saiu de uma amostra de doze órgãos com média
57,7 e mediana 10,5. **Estava errada por um fator de cinco**, e o erro foi da amostra: doze
sorteios não alcançam as caudas, e a distribuição é dominada por elas — na varredura
sequencial completa, os primeiros 200 órgãos já somavam **68.030 unidades**.

A medição que vale é a da coleta real, registrada em
[`etapa-11`](../registro/etapa-11-grafo-do-estado.md). A lição fica: **amostra de doze não
mede distribuição torta**, e o número que a substituiu veio de percorrer tudo.

## 7. Armadilhas

1. **`HTTP 200` não quer dizer sucesso.** Toda recusa vem com status 200 e `codigoErro` no
   corpo. Um cliente que olhasse o status registraria "sucesso, zero unidades" — e uma
   coleta lida como completa tem licença para concluir que órgãos desapareceram.

2. **Uma requisição por IP de cada vez.** Concorrência é recusada com
   `codigoErro 119` — *"Este IP tem uma requisição em andamento"*. Medido do pior jeito
   possível: com seis threads, **329 das 335 subárvores voltaram recusadas** e o processo
   terminou em nove segundos achando que tinha coletado 2.334 unidades. A coleta é
   sequencial por natureza da fonte; não adianta paralelizar em camada nenhuma.

3. **`codigoErro 104` é esperado, não defeito.** É o que `/estrutura-organizacional`
   responde para um `ente` — *"unidade organizacional deve ser um órgão ou entidade"*.
   Insistir cinco vezes contra um serviço público por um erro que não vai mudar é mau
   comportamento; o cliente distingue os dois casos.

4. **O mesmo campo chega em duas grafias.** `/unidade-organizacional/26` devolve
   `.../id/poder/executivo`; `/estrutura-organizacional` devolve `.../id/poder/1` para a
   **mesma** unidade. Um conversor que soubesse só uma delas funcionaria em metade das
   chamadas e devolveria nulo silencioso na outra metade.

5. **A raiz vem sem poder, sem esfera e com a sigla em branco.** A unidade 200000 devolve
   `codigoPoder: ""`, `codigoEsfera: ""` e `sigla: " "` — um espaço. Exigir esses campos
   recusaria justamente a raiz da árvore.

6. **Acentuação.** A resposta é UTF-8, e a leitura errada produz `Presidência` no primeiro
   nó e em todos os outros.

7. **O Senado tem 5 unidades no SIORG.** Não confundir com os 118 senadores que já estão na
   base: são cadastros diferentes, e o grafo liga um ao outro pelo órgão, não por pessoa.

8. **`unidade-administrativa` domina.** Não é detalhe de apresentação: é a razão de o mapa
   inicial mostrar só entes, órgãos e entidades. Desenhar tudo produz uma nuvem cinza onde
   o Executivo inteiro some.

## 8. Fixtures congeladas

| Arquivo | Para quê |
|---|---|
| `siorg-orgaos-amostra.json` | Seis órgãos escolhidos pelas armadilhas: um por poder, mais o GDF (esfera 2) e uma entidade |
| `siorg-estrutura-presidencia.json` | A subárvore, com a grafia **numérica** dos códigos |
| `siorg-unidade-presidencia.json` | A mesma unidade, com a grafia **por extenso** |
| `siorg-unidade-raiz.json` | A República Federativa do Brasil: sem poder, sem esfera, sigla em branco |
| `siorg-erro-concorrencia.json` | `codigoErro 119` com HTTP 200 — a recusa que se repete |
| `siorg-erro-ente.json` | `codigoErro 104` — a recusa que **não** se repete |
| `siorg-tabelas-de-referencia.json` | As quatro tabelas |
