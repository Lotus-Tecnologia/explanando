# ADR-0018 — Nome de pessoa no banco, uma vez, com endereço de origem

**Data:** 2026-09-21
**Situação:** aceito
**Contexto anterior:** [ADR-0004](ADR-0004-nomenclatura-tecnica.md),
[ADR-0009](ADR-0009-dois-numeros-nao-um.md),
[ADR-0016](ADR-0016-grafo-do-estado.md),
[ADR-0017](ADR-0017-mapa-de-fornecedores.md)

---

## Contexto

O mapa do Estado mostra 336 órgãos e não mostra **uma pessoa sequer**. A referência
[CivLab](../referencias/civlab.md) mostra as duas coisas: o *Graph* de instituições e o
*Power map* de gente. Faltava a segunda metade, e o pedido foi explícito: clicar num poder
e ver "presidência (Lula), ministro X (Fulano)".

Isso colide de frente com uma garantia do produto. A
[Etapa 12](../registro/etapa-12-quadro-de-pessoal.md) ingeriu 778 mil vínculos **nominais**
do Portal da Transparência e decidiu que **nenhum nome chega ao banco**: a agregação
acontece durante a leitura, e um teste de reflexão quebra o build se alguém acrescentar um
campo de nome à entidade de lotação.

## O que foi medido antes de decidir

| Fonte | Resultado |
|---|---|
| SIORG | ❌ não publica titular. Nenhum campo, em nenhum endpoint |
| **gov.br/planalto — "Quem é Quem"** | ✅ Presidente da República |
| **gov.br/planalto — Ministros** | ✅ 38 ministros, cargo → pessoa |
| **API da Câmara** — `/orgaos` | ✅ Mesa Diretora com títulos, presidências de comissão |
| **API do Senado** — `composicao/lideranca` | ✅ 286 designações; 47 líderes das duas Casas |
| Portal do STF | ❌ aplicação de página única; a lista renderizada traz **todos os ministros desde 1891** |
| Wikidata | ❌ estruturado e com foto, mas **sujo**: a consulta por "ministro do STF sem data de saída" devolve Ellen Gracie (aposentada em 2011) e Victor Nunes Leal (morto em 1985) |

Duas coisas dessa tabela merecem registro.

**O gov.br responde `404` — e não `403` — a um cliente HTTP comum.** Só devolve `200` com o
conjunto completo de cabeçalhos de navegador; faltando `Sec-Fetch-Mode` ou
`Accept-Language`, o 404 volta. Isso custou três tentativas e quase produziu a conclusão
errada de que a fonte não existia.

**Os nomes não batiam com o que o modelo sabia.** Miriam Belchior na Casa Civil, Boulos na
Secretaria-Geral, Messias na AGU. Se a tabela tivesse sido preenchida "de memória", teria
posto ministro errado na tela com cara de fato — e é por isso que **nenhum nome deste
contexto pode ser escrito à mão sem leitura ao vivo**.

## Decisões

### 1. A exceção existe, é estreita, e está declarada

Uma tabela, `cadeiras_de_comando`, com ~138 linhas. São cargos de comando cujo ocupante o
**próprio governo publica com nome e sobrenome**, no site da Presidência e nas APIs do
Congresso. Um ministro de Estado não tem expectativa de privacidade sobre o fato de ser
ministro.

A regra da Etapa 12 continua valendo onde nasceu: o quadro de pessoal segue agregado, sem
nome, com o teste de reflexão intacto. As duas coisas não se confundem porque são tabelas,
entidades e ingestões diferentes.

### 2. Sem fonte e sem data, a linha não existe

`fonte` e `verificado_em` são `NOT NULL`, e o construtor do domínio recusa criar a cadeira
sem a URL. Não é metadado de auditoria: é o que permite à tela dizer *"verificado em tal
dia, nesta página"*, que é a diferença entre informar e afirmar.

**Uma tabela de ocupantes envelhece em silêncio** — o ministro troca e o site continua
igual —, e a data é o único antídoto. Por isso o rodapé de cada poder mostra a verificação
mais **antiga** do conjunto, e não a mais nova: é o pior caso, que é o que o leitor precisa
saber.

### 3. Uma fonte que cai não apaga as outras

Cada coletor grava sob o próprio nome e substitui só o que ele mesmo gravou. O Planalto em
manutenção não pode esvaziar o Congresso.

**E coleta vazia sem erro também não apaga.** Uma página que responde `200` com o corpo
trocado é indistinguível de um governo sem ministros, e entre as duas leituras a segunda é
a absurda. O coletor do Planalto ainda tem um piso: abaixo de 20 ministros ele **falha**,
porque um Executivo com seis ministérios parece completo e por isso engana mais que um
mapa vazio.

### 4. O coletor vai na própria linha

A primeira versão deduzia quem tinha gravado o quê pelo prefixo da URL. É uma tabela de
adivinhação escondida no repositório: bastaria o Planalto mudar de domínio para a coleta
parar de apagar a anterior e passar a **dobrar** o Executivo a cada passada, sem erro
nenhum.

### 5. Só quem comanda

Dos 286 registros de liderança, 232 são **vice**-líderes — 185 só na Câmara. E a primeira
versão do coletor da Câmara aceitava qualquer título com "Presidente", rendendo 111
cadeiras: 30 presidências e noventa vice-presidências de comissão.

Um mapa de *comando* com noventa vices não é um mapa de comando, é o organograma da Casa.
Ficam os líderes e os presidentes. **A Mesa mantém os vices**, porque ali o colegiado *é* o
órgão de direção e a 1ª Vice-Presidência da Câmara é cargo de verdade.

### 6. Ocupante nulo é cadeira vaga, não "não sabemos"

O ADR-0009 aplicado a pessoas. Quem não sabe **não coleta a cadeira**; quem coleta com
ocupante nulo está afirmando que a cadeira existe e está vazia. São fatos diferentes e o
desenho os mostra diferentes — vaga recebe anel tracejado, e nunca vermelho.

### 7. Os quatro poderes vêm sempre, inclusive os vazios

Omitir o Judiciário porque não temos os ministros faria parecer que ele não tem comando.
O que existe é uma fonte que não publica, e `porQueFalta` diz isso com todas as letras.

### 8. A mesma camada muda de nome conforme o poder

A `Cúpula` do Executivo é "Ministros de Estado"; a do Judiciário seria "Ministros do
Supremo"; a do Legislativo é "Mesa Diretora". No Brasil *ministro* significa duas coisas
muito diferentes, e é justamente a confusão que um mapa do Estado deveria desfazer.

## Consequências

**O mapa de poder existe e responde ao grafo.** Clicar na cunha de um poder abre quem o
comanda — ligação que a referência não tem, porque lá o *Power map* é uma lista fechada de
vinte pessoas.

**O Judiciário continua sem rosto**, e vai continuar até aparecer fonte servida ou até o
cadastro no INLABS destravar o Diário Oficial, que publica nomeação e exoneração e
resolveria Executivo e Judiciário de uma vez.

**Ler HTML é uma dívida.** As duas páginas do Planalto estão versionadas em
`docs/contratos/`: no dia em que o layout mudar, o `git diff` da fixture mostra o quê.

**A curadoria manual fica autorizada mas não usada.** `OrigemDaCadeira.Curada` existe, a
tela a marca como "transcrito à mão" e o rodapé manda conferir a data. Nenhuma linha hoje
usa isso — e as onze do STF só entrarão assim se alguém as ler de fonte oficial, nunca de
memória.
