# Etapa 14 — O mapa de poder

**Data:** 2026-09-21
**Decisão:** [ADR-0018](../decisoes/ADR-0018-cadeiras-de-comando.md)
**Referência:** [CivLab §6](../referencias/civlab.md)

---

## 1. O que entrou

```
planalto      39 cadeiras   Presidente da República + 38 ministros de Estado
camara        52 cadeiras   Mesa Diretora + 30 presidências de comissão
liderancas    47 cadeiras   líderes das duas Casas e do Congresso Nacional
------------------------------------------------------------------------
mesa-cn        1 cadeira    Presidente do Senado e do Congresso Nacional
------------------------------------------------------------------------
             139 cadeiras, 0 fontes com falha
```

Na tela, dentro de `/estado`: um alternador ao pé do desenho troca **Instituições** por
**Quem comanda**, e clicar na cunha de um poder no grafo abre o comando daquele poder.

## 2. Eu tinha dito que não dava

Duas mensagens antes desta etapa, a conclusão registrada era: *"o Executivo não tem fonte
de ocupante"*. Estava errada.

O que produziu o erro foi o **modo de falhar do gov.br**: ele responde `404` — não `403` —
a um cliente HTTP comum, e `200` ao mesmo pedido com o conjunto completo de cabeçalhos de
navegador. Faltando `Sec-Fetch-Mode` ou `Accept-Language`, o 404 volta. Um 403 teria dito
"você não pode"; o 404 disse "não existe", e quase virou uma decisão de produto.

> Isso está escrito no `ClienteDoPlanalto`, porque quem "limpar" aquela lista de cabeçalhos
> por parecer supérflua vai derrubar a coleta do Executivo inteira — e o sintoma será um
> 404, que é o erro mais difícil de diagnosticar que existe.

## 3. E os nomes não batiam com o que o modelo sabia

Miriam Belchior na Casa Civil, Guilherme Boulos na Secretaria-Geral, Jorge Messias na AGU.
Nenhum desses estava no conhecimento do modelo, cujo corte é maio de 2026.

**Nenhum nome deste contexto foi escrito à mão.** Se a tabela tivesse sido "preenchida de
memória" — que era uma opção real, e parecia razoável para ~100 cargos — teria posto
ministro errado na tela com cara de fato.

## 4. As podas

Duas, e são a mesma decisão tomada duas vezes.

| Fonte | Bruto | Depois | O que saiu |
|---|---:|---:|---|
| Lideranças (Senado) | 286 | **47** | 232 vice-líderes, 185 só na Câmara |
| Comissões (Câmara) | 111 | **52** | 90 vice-presidências de comissão |

Um mapa de **comando** com noventa vice-presidentes não é um mapa de comando: é o
organograma da Casa. A Mesa mantém os vices, porque ali o colegiado *é* o órgão de direção
e a 1ª Vice-Presidência da Câmara é cargo de verdade.

## 4-A. A presidência do Senado só apareceu pela porta dos fundos

A primeira entrega tinha **uma** cadeira na camada "Presidência das Casas" — só a Câmara.
Metade do Congresso faltando, num mapa que se propõe a mostrar quem comanda.

O Senado **não publica a Mesa dele**. Foram sondados `composicao/mesa`,
`composicao/mesa/atual`, `composicao/mesa/SF` e `composicao/comissao/MESA` — todos 404 —,
e a lista geral de colegiados traz uma única Mesa: a do **Congresso Nacional**, código
1616. E é o bastante, porque quem preside o Congresso é, por determinação constitucional,
o Presidente do Senado.

```
Presidente da República                                Luiz Inácio Lula da Silva
Presidente da Câmara dos Deputados                     Hugo Motta        REPUBLICANOS-PB
Presidente do Senado Federal e do Congresso Nacional   Davi Alcolumbre   UNIÃO-AP
```

Só o presidente entra. A Mesa do Congresso é mista, e dois dos seus membros já estão no
mapa pela Mesa da Câmara — trazer os sete duplicaria pessoas na mesma tela por ocuparem
dois cargos reais, que é verdade que confunde mais do que informa. **A Mesa do Senado
propriamente dita continua faltando**, e faltará enquanto o Senado não a publicar.

O código do colegiado é um número mágico e não dá para fugir dele: o endpoint de
composição o exige, e a lista geral não filtra por tipo. A defesa é falhar — se 1616
deixar de ser a Mesa, a coleta para em vez de gravar a presidência de outro colegiado
qualquer na chefia do Legislativo.

## 5. O Judiciário ficou de fora, e a tela diz por quê

Três tentativas:

- **Portal do STF** — aplicação de página única. O HTML servido não tem a composição; a
  página que o JavaScript renderiza lista **todos os ministros desde 1891**, começando no
  Visconde de Sabará.
- **Wikidata** — estruturado, com foto, e **sujo**: a consulta por "ministro do STF sem
  data de saída" devolve 18 nomes, entre eles Ellen Gracie (aposentada em 2011), Joaquim
  Barbosa (2014) e Victor Nunes Leal (morto em 1985). Faltam as datas de fim.
- **dados.gov.br** — nada.

Então o endpoint devolve os quatro poderes **sempre**, inclusive os vazios, e o vazio
carrega o motivo. Omitir o Judiciário faria parecer que ele não tem comando.

## 6. Três defeitos que só a tela pegou

**A mesma deputada, três vezes, idêntica.** O cargo das lideranças era montado como
tipo + partido — "Líder do Senado Federal — UNIÃO" —, e a Professora Dorinha Seabra
lidera o partido, o bloco **e** a Bancada Feminina. Três cargos reais viraram três
círculos iguais. O React reclamou de chave repetida; o leitor teria lido como duplicação.
O cargo passou a sair da **unidade** de liderança, que é o que distingue os três.

**O coletor era deduzido da URL.** A primeira versão do repositório apagava as linhas de
uma fonte casando o prefixo do endereço. Bastaria o Planalto mudar de domínio para a
coleta parar de apagar a anterior e passar a **dobrar** o Executivo a cada passada, em
silêncio. O coletor passou a viajar na própria linha.

**O `viewBox` do grafo lia um ref durante a renderização.** O cursor de "arrastando" vinha
de `arrastando.current`, o que o `react-hooks/refs` proíbe com razão: o valor muda sem
provocar novo desenho, e a mão ficaria fechada depois de soltar. Virou `active:` no CSS.

## 7. Um erro de infraestrutura que vale registrar

O `coletar-cadeiras` baixou as duas páginas do Planalto e **estourou na hora de gravar**:
a conexão usa `EnableRetryOnFailure`, e o Npgsql recusa transação iniciada pelo usuário
fora da estratégia de execução. Com razão — uma tentativa repetida no meio de uma
transação aberta refaria só parte do trabalho. A transação passou a rodar dentro de
`CreateExecutionStrategy()`.

## 8. O `/rede` foi desativado

A página de fornecedores compartilhados saiu: o lugar dela na navegação passou a ser o
mapa de poder, e duas vistas de pessoas competindo entre si confundiriam mais do que
somariam.

**O backend continua**, com os 17 testes e o [ADR-0017](../decisoes/ADR-0017-mapa-de-fornecedores.md)
intactos: o achado é real — 91% dos pares densos são do mesmo estado, 19% do mesmo partido
— e `ObterLigacoesAsync` tem casa natural na página de um parlamentar. Fica como dívida
declarada, não como código morto esquecido.

## 9. Números

| | |
|---|---:|
| Cadeiras no banco | 139 |
| Fontes | 4, independentes |
| Testes de backend | 918 |
| Testes de frontend | 213 |
| Páginas estáticas no build | 863 |
