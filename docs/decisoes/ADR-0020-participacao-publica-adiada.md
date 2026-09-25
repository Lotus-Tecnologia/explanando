# ADR-0020 — Discussão pública e notas da comunidade esperam audiência e parecer jurídico

**Data:** 2026-09-24
**Situação:** Adiada — reavaliar quando os gatilhos da seção final forem cumpridos
**Contexto anterior:** [ADR-0004](ADR-0004-nomenclatura-tecnica.md) ·
[ADR-0007](ADR-0007-postura-permissiva-do-motor.md) ·
[plano de execução](../plano-de-execucao.md)

---

## O que foi proposto

Duas ideias de 22/09/2026, da mesma família — **conteúdo gerado por usuário** sobre os
achados:

1. **Discussão pública**, no formato de fórum com votação: apontamento → resposta oficial →
   comentário da comunidade → votação ("resolve?", "evidência suficiente?").
2. **Notas da comunidade**, no formato do X: sob um apontamento como *"fornecedor recebeu 2
   milhões"*, uma nota como *"esse fornecedor possui contrato licitado desde 2025"* ou
   *"a empresa pertence ao irmão do assessor parlamentar"*.

As duas são boas ideias **para um produto com audiência**. Nenhuma das duas é boa agora.

## Por que não agora

**O custo não é construir, é moderar — todo dia, para sempre.** O cadastro, o comentário e o
voto são CRUD de uma semana. A moderação de um espaço público que nomeia políticos e
empresas é uma função permanente, com alguém de plantão. Hoje não há esse alguém.

**A nota do exemplo é o risco inteiro, numa frase.** *"A empresa pertence ao irmão do
assessor"* é uma afirmação factual, não verificada, sobre **pessoa privada**, publicada
dentro do nosso produto. O projeto inteiro foi construído para não fazer isso — o ADR-0004
proíbe imputar conduta, a ponte SIAPE↔SIORG recusa ligação aproximada, o mapa de
fornecedores traz a ressalva dentro do JSON. Abrir um campo livre para terceiros escreverem
exatamente o que nós nos proibimos de escrever desfaz esse trabalho pela porta dos fundos.

**Notas da comunidade não funcionam sem escala.** O modelo do X não é "qualquer um comenta":
é um algoritmo de consenso entre avaliadores que costumam discordar, rodando sobre milhares
deles. Com cem usuários, uma nota é um comentário com etapas a mais — e o primeiro grupo
organizado que chegar decide o que aparece.

**Caixa vazia piora o produto.** Antes de haver tráfego, um espaço de discussão com zero
comentários faz o site parecer abandonado. É a única categoria de recurso que **tira**
valor quando chega cedo.

**A responsabilidade jurídica mudou e precisa de parecer.** O regime do art. 19 do Marco
Civil da Internet — que condicionava a responsabilidade do provedor por conteúdo de terceiro
a ordem judicial — foi revisto pelo STF em 2025. O alcance exato dessa revisão para um
produto como este é pergunta para advogado, não para desenvolvedor, e precisa estar
respondida **antes** de existir o primeiro campo de texto livre.

## O que fazemos no lugar

**A resposta oficial já existe, e ainda não está na tela.** O domínio tem `Contestacao`,
com o texto do gabinete, anexo, e as situações *Publicada* e *Retirada* — e o produto,
por decisão já registrada, não modera resposta oficial. Nenhuma página a exibe hoje.

Mostrar a contestação do gabinete **ao lado do achado a que ela responde** entrega a parte
mais valiosa das duas ideias — o contraditório — com o menor risco possível: quem escreve é
parte identificada e interessada, não um anônimo, e o texto é sobre o próprio gabinete, não
sobre terceiros.

Isso entra no [plano de execução](../plano-de-execucao.md) **na Fase 1**, junto do score
explicável — aprovado pelo Gustavo em 2026-09-24 —, e não depende desta reavaliação.

## Gatilhos para reavaliar

Todos os quatro, não qualquer um deles:

| Gatilho | Por quê |
|---|---|
| **Audiência sustentada** — 50 mil visitantes únicos por mês, três meses seguidos | Abaixo disso, a caixa fica vazia e o consenso não se forma. Número fixado pelo Gustavo em 2026-09-24 |
| **Moderação com dono e prazo** — quem responde, em quanto tempo | Sem isso, o primeiro conteúdo difamatório fica no ar até alguém notar |
| **Parecer jurídico atualizado** sobre a responsabilidade por conteúdo de terceiro depois da revisão do art. 19 | É a pergunta que decide se o recurso é viável, e não é de engenharia |
| **Mecanismo contra ação coordenada** — no mínimo, peso por antiguidade de conta | Sem isso, a votação mede quem se organizou, não o que é verdade |

## Se um dia entrar, o que não pode entrar junto

- Nota ou comentário que afirme fato sobre **pessoa privada** sem link para fonte primária.
- Votação que altere o índice de risco ou a gravidade de um achado. O índice é saída de um
  motor determinístico; opinião agregada é outra coisa e fica visualmente separada dela.
- Vocabulário que o ADR-0004 proíbe no produto — ele passa a valer para o conteúdo de
  terceiro exibido pelo produto.
