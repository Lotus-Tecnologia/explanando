# Etapa 15 — O plano de execução, fases 1 a 6, e a auditoria tela a tela

**Data:** 2026-09-24 e 2026-09-25
**Plano:** [plano-de-execucao.md](../plano-de-execucao.md)
**Decisões:** [ADR-0020](../decisoes/ADR-0020-participacao-publica-adiada.md) ·
[ADR-0021](../decisoes/ADR-0021-ia-espera-receita.md)

---

## 1. O que entrou

| Fase | Entrega | Onde se vê |
|---|---|---|
| 1 · Score explicável | "De onde vem o índice", regra a regra, em fatia do peso | Perfil |
| 1 · Junto | A resposta do gabinete ao lado do achado | Despesa e listas |
| 2 · Comparador | Dois parlamentares da mesma casa, lado a lado | `/comparar/{a}/{b}` |
| 3 · Alertas por RSS | Um feed por parlamentar e um geral | `/parlamentares/{id}/feed.xml`, `/feed.xml` |
| 4 · Timeline de pagamentos | Os maiores fornecedores, ano a ano, com ano parcial marcado | Perfil |
| 5 · Quem beneficia quem | Sócio em comum entre fornecedores de parlamentares diferentes | Perfil |
| 6 · Investigação do TSE | As três perguntas respondidas, sim para as quatro ideias | [contratos/tse.md](../contratos/tse.md) |

**A Fase 7 espera** os lotes do TSE, que precisam ser baixados à mão (o CDN bloqueia cliente
automatizado). **A Fase 8 espera** a VPS.

## 2. O defeito que a Fase 1 encontrou antes de existir

**O índice publicado nunca contava a regra de rede.** A varredura individual apagava *toda*
flag das despesas do deputado — inclusive o `REG_NET_004`, que a passada de rede tinha
gravado. O ciclo roda individual → rede, então a flag de rede reaparecia na lista de achados,
mas o score já tinha sido calculado sem ela.

Medido na base local: **17 deputados** com índice abaixo do que a própria página listava. A
decomposição por regra teria exposto isso na tela — as parcelas não fechariam com o total.

A correção: a varredura individual apaga só os códigos das próprias regras e soma ao score as
flags de outras passadas; a passada de rede devolve quem ganhou ou perdeu achado, e o ciclo
re-varre esses. Depois de re-varrer a base: **zero divergências**.

## 3. Três medições que mudaram o plano

- **"Parcial" ficou por parlamentar.** O plano supunha que 2023 começava em fevereiro. Na
  base, 2023 tem os doze meses — há despesa de janeiro da legislatura anterior. O ano parcial
  passou a ser o que *aquele* parlamentar não teve despesa nos doze meses.
- **O corte da Fase 5 vem da distribuição.** Sem corte, 18.327 pares de empresas ligados por
  sócio, com até 271 por deputado. Com R$ 50 mil em cada ponta e sócio de alcance 2 a 5,
  sobram 128 cadeias — e a primeira delas é exatamente a que a medição em SQL puro achou.
- **O TSE mudou em um dia.** Em 24/09 o portal respondia `200` com cabeçalhos de navegador; em
  25/09, `403` da Akamai. Só o navegador real passa.

## 4. A data do alerta mora no id

A varredura recria as flags a cada passada, então a data da flag não diz quando um achado é
novo. As despesas não tinham coluna de data de entrada — mas os ids são **UUID v7**, e os 48
primeiros bits são os milissegundos da criação. `Entidade.MomentoDaCriacao` lê essa data, e é
ela que o feed RSS publica. Um achado de regra alterada sobre despesa antiga fica com a data
antiga; o que sobe no feed é despesa nova.

## 5. A auditoria

Percorrida tela a tela, no claro, no escuro e a 375 px. Os defeitos, em três famílias:

**Números que a tela inventava ou errava.**
- Sem API, a home mostrava "631", "790.000+" e "R$ 800M+" escritos à mão.
- "3,3% das despesas recebem gravidade alta" — era 3,3% dos *achados*; das despesas, 0,1%.
- As abas de densidade somavam o volume só de quem tem amostra: R$ 799,9 mi numa aba, R$ 802,4
  mi na outra.
- O índice é ponderado, mas três lugares diziam "indicadores por 100 despesas".
- A data da nota voltava um dia: meia-noite UTC formatada no fuso de Brasília.
- A descrição do ranking do Senado dizia "Dados públicos da Câmara dos Deputados".

**Layout e leitura.** Valor que estourava o cartão, rótulo de ponta-cabeça depois do giro do
grafo, competência crua ("2026-07"), rótulos cortados, cabeçalho em três linhas no celular,
listagem vazia no Senado, CSV que o Excel em português lia como texto.

**Acessibilidade.** O rótulo da busca da home apontava para si mesmo; o campo não tinha nome
para leitor de tela.

**E uma lacuna do pedido original:** o mapa de poder não tinha o Vice-Presidente. Agora tem —
140 cadeiras.

## 6. Números

| | |
|---|---|
| Testes no backend | 979 (551 domínio, 115 aplicação, 10 arquitetura, 217 integração, 86 ponta a ponta) |
| Testes no frontend | 270 |
| Build de produção | verde |
| Cadeiras de comando | 140 |
| Divergências score × flags | 0 (eram 17) |

## 7. Decisões que ficaram com o Gustavo

1. **Mostrar o nome do sócio em comum** (Fase 5) — ver [pendências §5b](../pendencias.md).
2. **Baixar os lotes do TSE** (Fase 7) — ver [contratos/tse.md](../contratos/tse.md).

## 8. Depois do relatório: o nome do sócio e a carga do TSE (2026-09-25)

As duas decisões da seção 7 foram tomadas no mesmo dia: **mostrar o nome do sócio** e
**baixar os arquivos do TSE**.

- O nome do sócio sai no bloco "Sócios em comum", como o QSA o publica; o CPF, nem mascarado.
- Os três lotes do TSE (quase 1 GB) foram baixados pelo `fetch` do Node — o único cliente que
  o CDN aceita — e carregados por `importar-tse`: **510 de 513** deputados ligados à candidatura
  de 2022, 152.859 municípios com voto, 12.845 doações a parlamentares e 2.767 de sócios de
  fornecedor. O CPF do doador é mascarado na leitura, e o completo não é gravado.
- **REG_NET_001 entrou no motor** (versão 5): 73 achados em 40 deputados.
- O perfil ganhou "De onde vieram os votos", e as cadeias de sócio ganharam as doações.

Os números e as duas medições que mudaram o modelo estão em
[contratos/tse.md](../contratos/tse.md).
