# Etapa 10 — Calibragem do Senado e rankings por casa

**Data:** 2026-09-17 · **Etapa anterior:** [09 — A ingestão do Senado](etapa-09-ingestao-do-senado.md)

A Etapa 09 trouxe o Senado para a base e terminou com um achado que impedia publicá-lo: ele
sinalizava quase sete vezes mais que a Câmara, por lacuna de tabela. Esta etapa fecha isso —
e separa os dois rankings, que era a outra metade do problema.

---

## 1. Objetivo

Duas coisas que se apoiam e não se substituem.

**Calibrar** o motor para as rubricas da CEAPS, para que um senador não apareça mais
sinalizado que um deputado de comportamento idêntico.

**Separar** os rankings por casa, porque as cotas são distintas. Uma não resolve a outra:
comparar senador só com senador tira a distorção do ranking, mas o perfil individual
continuaria mostrando uma em cada cinco despesas sinalizada.

## 2. Definido

### 2.1 Uma rubrica aberta não sustenta incompatibilidade

A rubrica de consultorias do Senado **saiu da tabela de CNAE**. Não foi ampliação — foi
abstenção.

O texto dela termina em *"...e outros serviços de apoio ao exercício do mandato
parlamentar"*, que a da Câmara não tem. Isso é um conjunto **aberto**, e contra conjunto
aberto nenhum ramo de atividade pode ser declarado incompatível. A medição confirmou: 4.405
flags em 8.462 despesas, **52,06%**. Uma regra que sinaliza mais da metade do que examina
não está achando desencontro grosseiro — 3.800 das 4.405 eram uma agência de viagens
faturando ali, que é escolha de classificação do gabinete.

### 2.2 Generosidade tem limite, e o limite é o texto da rubrica

*"Aluguel de imóveis, compreendendo despesas concernentes a eles"* é amplo e **enumerável**:
condomínio, vigilância, instalações, limpeza, reforma, reparo. Todos entraram.

Ficaram de fora, e continuam sinalizando: contabilidade (`69`), fomento mercantil (`64`) e
publicidade (`73`). Nenhum concerne a um imóvel, e é esse o sinal que a rubrica ainda deve
dar. Um teste guarda que restaurante sob aluguel segue incompatível.

### 2.3 Rubrica que mistura estadia com combustível não conclui

`REG_GEO_003` casa por marcador de texto, o que funciona enquanto a rubrica nomeia uma coisa
só. A do Senado junta cinco e contém "locomoção", "hospedagem" e "alimentação" **junto com**
combustível — então cada tanque cheio no recesso virava "alimentação ou hospedagem
expressiva durante o recesso".

A rubrica agrupada da Câmara — *"LOCOMOÇÃO, ALIMENTAÇÃO E HOSPEDAGEM"* — **não** traz
combustível. É a diferença exata entre as duas casas, e por isso a correção não a alcança.

### 2.4 A casa é obrigatória, não um filtro opcional

No contrato da aplicação, `RanquearAsync` recebe a casa **sem valor padrão**: esquecer de
separar deixa de ser possível — vira erro de compilação, não uma lista silenciosamente
errada. Na API, omitir devolve **400**.

É a mesma classe de erro do `idLegislatura` da Câmara, que responde 200 com lista vazia:
pedido incompleto precisa falhar alto, não devolver algo plausível.

### 2.5 O piso amostral passa a valer dentro da casa

O filtro entra antes de tudo na consulta. Um senador com 200 despesas disputa com os outros
117 senadores, não com 513 deputados cuja cota tem outra regra.

### 2.6 A home deixa de ser um ranking

Ela era o ranking por volume da Câmara. Com as duas casas, qualquer escolha faria uma
parecer a principal e a outra um apêndice. Agora `/` entrega a **escolha**, e as rotas são
simétricas: `/camara` e `/senado` são a raiz de cada casa e o ranking por volume dela.

O tamanho desigual — 513 contra 118 — é real e não vira hierarquia: as duas raízes têm a
mesma prioridade no sitemap, e um teste guarda isso.

### 2.7 O que **não** se separa: `REG_NET_004`

A passada de rede continua cruzando as duas casas.

**Ranking é comparação entre pessoas; a passada de rede é detecção entre pessoas.** Comparar
a densidade de um senador com a de um deputado é injusto, porque as cotas diferem. Já um
fornecedor recém-aberto concentrado num partido que tem deputados *e* senadores é achado
legítimo — e separar a passada perderia sinal real. Ver
[ADR-0012](../decisoes/ADR-0012-assimetria-camara-senado.md).

## 3. Executado

| Camada | O quê |
|---|---|
| **Domain** | `CompatibilidadeCnae` recalibrada; `TiposPresenciais` com a ressalva de ambiguidade; `MotorDeRegras.VersaoAtual` → 4 |
| **Application** | `RanquearAsync` com a casa obrigatória; `ItemDoRanking.Casa` |
| **Infrastructure** | Filtro por casa antes do piso; casa na chave do cache |
| **Api** | `?casa=` obrigatório, 400 explicando por quê |
| **Frontend** | `lib/casas.ts`; seis rotas de ranking; home como porta; `caminhoDoRanking` |

## 4. Testes

**735 no backend** (445 domínio, 54 aplicação, 179 integração, 47 ponta a ponta, 10
arquitetura) e **116 no frontend**. `tsc` limpo, lint limpo, build com 0 avisos.

A semente de ponta a ponta ganhou uma senadora — sem alguém da outra casa nela, um filtro
quebrado passaria sem ninguém notar. Ela tem 8 despesas de propósito, para provar que o piso
amostral também vale por casa.

A API falsa do frontend passou a conhecer a casa, pelo mesmo motivo.

## 5. Descobertas

**Cada adição à tabela saiu de consulta à base, não de hipótese.** O método foi: agrupar as
flags de `REG_KYB_001` do Senado por rubrica e por prefixo de CNAE, e ler os fornecedores
recorrentes. Foi assim que `81` (condomínio, 1.638 ocorrências) e `77` (locadora, 1.052)
apareceram — nenhum dos dois estaria numa lista escrita de memória.

**Duas rubricas já estavam bem calibradas**, e isso validou a abordagem: passagens em 0,09%
e divulgação em 1,17%. Rubrica estreita funciona; o problema é sempre o pacote.

**O EF não traduz método C# dentro da projeção.** `NomeDaCasa(...)` dentro do `Select`
quebraria em tempo de execução, não de compilação. Como a consulta já está filtrada por uma
casa só, o nome é constante e foi resolvido antes, entrando como parâmetro.

**A chave do cache precisava da casa.** Sem ela, o ranking da Câmara seria servido para quem
pediu o do Senado durante os cinco minutos da janela — dado certo, casa errada, e nada no
corpo denunciaria.

## 6. O resultado

| | Antes | Depois |
|---|---|---|
| Flags na base | 36.527 | **25.047** |
| `REG_KYB_001` | 17.933 | **6.933** |
| `REG_GEO_003` | 682 | **202** |
| Câmara | 2,955% | **2,957%** |
| **Senado** | **19,773%** | **5,080%** |
| Senado, gravidade alta | 0,061% | **0,061%** |

De 6,7× para 1,7×. Em **alta gravidade o Senado está abaixo da Câmara** — 0,061% contra
0,097%. E as nove regras não tocadas ficaram com a contagem idêntica, o que confirma que a
mudança foi cirúrgica.

## 7. Pendências

| Pendência | De quem é |
|---|---|
| **`REG_TX_001` no Senado** — 25× a taxa da Câmara, 946 das 1.467 em passagens | **Sua** — é decisão de produto, não calibragem. A regra agrupa por fornecedor e mês, sem olhar rubrica, e descreve corretamente o que vê: várias notas da mesma agência no mês é padrão real de contratação. Não há evidência de que esteja errada |
| Limites da CEAP e da CEAPS | **Sua** — `REG_TX_002` segue em `SemDado` nas duas casas |
| Acrescentar a casa à chave de `checkpoints_ingestao` | Minha — hoje protegido por guarda no comando |
| Escritórios de apoio do Senado (`REG_NET_003`) | Minha — a fonte existe e não foi ingerida |

## 8. Próxima etapa

**Identidade visual e UI/UX**, com as referências do Gustavo. A estrutura de rotas já está
no lugar; o redesenho reestiliza, não reestrutura.

O que ele tem de herdar continua sendo o [ADR-0013](../decisoes/ADR-0013-identidade-visual-e-taxonomia-de-risco.md):
vermelho estritamente na gravidade alta, cada nível com cor **e** forma **e** rótulo, sem
caixa alta, e toda cor nova passando pelo cálculo de contraste antes de entrar.
