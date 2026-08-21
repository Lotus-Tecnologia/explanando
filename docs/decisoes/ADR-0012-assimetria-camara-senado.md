# ADR-0012 — A assimetria entre Câmara e Senado é exibida, não escondida

**Data:** 2026-08-21 · **Situação:** Aceita

## Contexto

O [spike de contratos do Senado](../contratos/senado.md) mediu duas ausências que não são
detalhes técnicos: elas mudam o que a página pode afirmar.

1. **Não existe link para o comprovante.** Nenhum dos treze campos da CEAPS é URL.
2. **Não existe glosa.** A CEAPS publica um valor único; a CEAP da Câmara publica três
   (pedido, pago, cortado).

A primeira ataca a base do [ADR-0004](ADR-0004-nomenclatura-tecnica.md). O produto não acusa:
ele aponta um indicador e deixa o **documento oficial** falar. Tirar o documento da equação
faz a afirmação passar a ser inteiramente nossa — exatamente o risco que o ADR-0004 existe
para evitar.

A segunda é mais sutil e por isso mais perigosa. Exibir `R$ 0,00` de glosa para um senador
diria que a despesa **passou por conferência e foi integralmente aprovada** — uma afirmação
que ninguém fez. É um falso positivo de leitura, produzido por um valor que parece inocente.

## Decisão

### 1. A ausência do comprovante vira informação, não lacuna

No lugar do botão "Ver Nota Fiscal", o card de uma despesa do Senado exibe uma **tag inativa
com contraste de alerta**:

> 🚫 Senado não disponibiliza o comprovante

A frase atribui a ausência a quem a produziu. Sem ela, o espaço vazio onde o link deveria
estar seria lido como falha da plataforma.

O prompt da Camada 2 passa a tratar a opacidade como parte do achado: a narrativa não fala só
do gasto, fala do agravante de que a prestação de contas daquela Casa não permite conferência
documental.

### 2. Glosa desconhecida é `null`, nunca zero

No banco, `despesas.valor_glosa` passa a ser **nullable**. O valor único da CEAPS vai para
`valor_liquido` e `valor_documento`; `valor_glosa` fica nulo.

`ConsolidadoMensal.TotalGlosa` segue a mesma regra: nulo quando nenhuma despesa do mês informa
glosa.

Na interface, a quebra "Valor Declarado × Glosa" é **omitida por inteiro** quando a glosa é
desconhecida. O leitor vê apenas "Valor Reembolsado".

`Despesa.GlosaConhecida` é a propriedade que a borda consulta. Existe separada de `TemGlosa`
justamente porque as duas perguntas são diferentes: "houve corte?" e "sabemos se houve corte?".

### 3. O Relatório Cidadão traz nota técnica quando o alvo é senador

Texto pré-formatado no rodapé:

> **Nota técnica:** A API de Dados Abertos do Senado Federal, ao contrário da Câmara dos
> Deputados, não fornece links públicos para os comprovantes físicos (notas fiscais) atrelados
> a estes reembolsos. A impossibilidade de anexar os espelhos das notas é uma limitação
> exclusiva da fonte oficial dos dados.

### 4. `REG_NET_004` roda sobre as duas casas juntas

A passada de rede recebe Câmara **e** Senado no mesmo `ContextoDeRede`: 594 parlamentares.

O motivo é aritmético antes de ser analítico. A regra declara piso de 100 parlamentares, e o
Senado tem 82 — sozinho, ele nunca alcançaria o piso. Rodar junto não custa nada: a regra já
agrupava por `DocumentoFornecedor` e lia `SiglaPartido`, sem nunca consultar a casa.

A descrição da flag passa a dizer a composição — "2 deputados e 1 senador" — porque um
fornecedor recém-aberto atravessando as duas casas do Congresso é achado de peso diferente de
um restrito a uma delas.

### 5. Os tipos do Senado entram no mesmo catálogo, e não num `enum`

São **sete** categorias, verificadas em cinco anos de dado sem uma única mudança de esquema.
Elas ocupam os códigos 17 a 23 do `CatalogoDeTiposDeDespesa`, ao lado dos dezesseis da Câmara.

Um `enum` foi considerado e recusado pelo mesmo motivo já aceito para a Câmara — a lista muda
por Ato da Mesa, e recusar o desconhecido rejeitaria despesas no meio de uma coleta — mais um
fato medido: **76 registros já vêm com o tipo nulo**. Um `enum` teria de tratar o nulo *e*
quebrar no tipo novo.

Duas descobertas do caminho:

- A rubrica de **divulgação da atividade parlamentar** é a mesma nas duas casas depois da
  normalização (a da Câmara traz ponto final, que `Normalizar` remove). A tabela de CNAE tem
  uma entrada só, e ela atende as duas. Foi o analisador que pegou a duplicata — ela teria
  sobrescrito em silêncio.
- A rubrica de material de consumo do Senado vem com **espaço no fim**. O catálogo guarda a
  forma aparada.

### 6. O teto da cota passa a ser por casa

`LimiteCota` ganha `Casa`. CEAP e CEAPS são cotas **diferentes**, com valores próprios para a
mesma UF: sem o discriminador, o teto do Acre para deputado e para senador colidiriam no
índice único, e `REG_TX_002` compararia o gasto de um senador contra o teto de um deputado.

## Consequências

**A precisão de `REG_KYB_001` cai no Senado, e isso é inerente à fonte.** A maior rubrica —
44% das despesas — junta locomoção, hospedagem, alimentação e combustível numa categoria só. A
lista de CNAE precisa cobrir os quatro domínios, o que faz quase qualquer ramo plausível
passar. A regra só pega desencontro grosseiro, do tipo "construção civil contra alimentação".

A alternativa seria deixar a rubrica fora da tabela, marcando-a como `SemDado` na cobertura.
Preferiu-se a lista ampla: o desencontro grosseiro continua sendo achado legítimo, e descartá-lo
seria jogar fora sinal que existe. A cobertura reportada fica otimista nessa rubrica, e o
registro da etapa diz isso em voz alta.

**Os limites da CEAPS por UF ainda não existem.** Enquanto não existirem, `REG_TX_002` se
abstém sobre o Senado inteiro — e a `MetadadosDeCobertura` reporta `SemDado`, que é a resposta
honesta.

## Alternativas descartadas

**Preencher a glosa do Senado com zero.** Simplificaria o esquema e a UI. Descartada: produz
uma afirmação falsa sobre conferência que nunca aconteceu, e é o tipo de erro que ninguém
percebe porque parece um dado normal.

**Omitir o Senado da página até haver comprovante.** Descartada: a ausência de transparência
de uma Casa não pode virar motivo para ela sair do escrutínio. Exibir com a tag é mais
pressão, não menos.

**Rodar `REG_NET_004` por casa, com piso reduzido para o Senado.** Descartada: baixar o piso
para acomodar uma base pequena é exatamente a distorção que o piso existe para impedir.
