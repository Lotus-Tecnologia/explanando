# Etapa 05 — API pública de leitura

**Data:** 2026-08-21 · **Situação:** ✅ Concluída

---

## 1. O que existe

Quatro endpoints `GET`, sobre a base real de 513 parlamentares e 711.796 despesas.

| Endpoint | O quê |
|---|---|
| `GET /api/v1/parlamentares?ordenar=volume\|indice\|gravidade` | Ranking, paginado |
| `GET /api/v1/parlamentares/{id}` | Perfil, com série mensal e cobertura |
| `GET /api/v1/parlamentares/{id}/despesas` | Despesas com achados, filtradas |
| `GET /api/v1/despesas/{id}` | Uma despesa e seus achados |
| `GET /saude` | Sonda |

**Só leitura.** Favoritar, contestar e solicitar varredura existem como entidades no domínio
mas não têm superfície HTTP: são outro ciclo de vida, exigem autenticação e pagamento, e
misturá-las faria a API pública carregar preocupações que não são dela.

Suíte em **666 testes**, dos quais **22 são de ponta a ponta** — o
`Explanando.E2E.Tests` estava vazio desde a Etapa 00.

## 2. Três ordenações, porque são três perguntas

| Ordenação | Ordena por | Inclui quem está abaixo do piso? |
|---|---|---|
| `volume` | Soma em reais | **Sim** — gasto não depende de amostra |
| `indice` | Densidade de indicadores por 100 despesas | Não |
| `gravidade` | Densidade **só das flags altas** | Não |

A terceira resolve a **questão 5**, e o efeito aparece no dado real:

| Ordenação | 1º lugar | Altas |
|---|---|---|
| `volume` | Pompeo de Mattos — R$ 2.287.735 | — |
| `gravidade` | **Célia Xakriabá** | **176** |

Um analista de *due diligence* quer os 176 achados graves no topo da fila; o leitor do ranking
público quer saber quem gastou mais. A escolha ficou com quem consome, e não com um palpite
nosso sobre a preferência do mercado.

**498 de 513** aparecem nas ordenações por densidade — os 15 restantes saem porque estão
abaixo do piso do [ADR-0009](../decisoes/ADR-0009-dois-numeros-nao-um.md), e continuam no
ranking de volume. Quem sai traz `indiceDeRisco: null`, **não zero**: zero diria que a
densidade foi medida e deu nada.

## 3. A cobertura viajou para a API, e foi preciso persisti-la

A `MetadadosDeCobertura` nascia em memória durante a varredura e morria com ela. O
[ADR-0013](../decisoes/ADR-0013-identidade-visual-e-taxonomia-de-risco.md) §2 proíbe a
interface de renderizar regra abstida como aprovada — e sem persistir, a API não teria o que
expor.

`CoberturaDoParlamentar` grava uma linha por (parlamentar, regra): **5.643 na base real**,
exatamente 11 × 513.

O payload de um perfil real mostra a distinção que o produto inteiro existe para fazer:

```
    REG_TX_001     SemAchados         flags=0     ← avaliou e está limpo
 >> REG_TX_002     SemDado            flags=0     ← não teve como avaliar
```

**Ambos zero. Significados opostos.** E cada abstenção diz por quê, em português:

> *Nenhuma despesa pôde ser avaliada: não há teto de cota publicado para o período.*

Essa frase é gerada na API, e não deixada ao consumidor. Se cada cliente redigisse a própria
explicação, "nada irregular encontrado" apareceria em algum lugar mais cedo ou mais tarde.

## 4. Decisões de contrato

**Modelos de leitura projetados campo a campo, nunca entidade de domínio.** A razão que
importa não é desempenho: projetar campo a campo **obriga a decidir o que é publicado**. Uma
entidade serializada inteira vaza o que for acrescentado a ela depois, sem ninguém revisar.

**O documento do fornecedor sai formatado pelo objeto de valor**, não pela borda. CNPJ sai
inteiro — é dado de pessoa jurídica e público; CPF sai sempre mascarado. A borda não tem como
contornar isso, porque não é ela que decide.

**Enum serializa por nome.** `"situacao": 4` obriga o consumidor a conhecer a ordem de um
`enum` C# que ele não vê — e a ordem pode mudar. `"situacao": "SemDado"` é auto-descritivo, e
é o que separa contrato público de vazamento de detalhe interno.

**Teto de 200 itens por página.** A paginação é a superfície mais fácil de abusar numa API
pública sem autenticação.

**Filtro inválido devolve 400, nunca resultado silencioso.** Mês sem ano e mês fora da faixa
são recusados: aceitar em silêncio devolveria a lista inteira e o leitor acharia que filtrou.
Ordenação desconhecida também — devolver o ranking por volume faria o consumidor achar que
estava ordenando por risco.

## 5. Cache: só o ranking

`HybridCache` com janela de 5 minutos, **apenas sobre o ranking**. É decisão de projeto:

- O ranking é a página que um link viral atinge — milhares de requisições idênticas à mesma
  primeira página, cada uma agregando 513 linhas.
- Perfil e despesas são consultas **dispersas**: cada leitor abre um parlamentar diferente, e
  cachear tudo encheria a memória com entradas de uso único.

Medido na base real: **8,7 ms → 1,8 ms** na segunda chamada.

`HybridCache` em vez de `StackExchange.Redis` cru porque ele dá memória **e** distribuído pela
mesma interface, com proteção contra estouro de rebanho. Sem `EXPLANANDO_REDIS` configurado
roda só em memória — que é o certo para desenvolvimento — e ganha o Redis por registro, sem
que nenhuma consulta mude.

Implementado como **decorador**: quem lê o SQL não precisa saber que existe cache, e um teste
da consulta continua medindo a consulta.

## 6. O que a base real pegou e a semente não

A API subiu contra os 513 e o perfil devolveu **HTTP 500**. Os 22 testes E2E passavam.

A causa: `coberturas_regra` não existia no banco de desenvolvimento — a migration foi criada
mas só chegou aos contêineres dos testes. **A API não roda migrations na subida**, e isso é
proposital: API de leitura não deve mexer em esquema.

Não é defeito de código, mas é lição de processo: teste com semente própria não substitui
apontar para a base real. O mesmo vale para o enum, que continuou saindo numérico depois da
correção porque o processo antigo ainda estava servindo — só o reinício mostrou.

## 7. Uma vulnerabilidade fechada no caminho

O audit do NuGet quebrou o build: `Microsoft.OpenApi` 2.0.0, puxado transitivamente pelo
`AspNetCore.OpenApi`, tem advisory de **severidade alta** (GHSA-v5pm-xwqc-g5wc).

Pinado na 2.12.2 em vez de suprimido. `CentralPackageTransitivePinningEnabled` existe
exatamente para corrigir dependência indireta sem esperar o pacote de cima atualizar.

## 8. Pendências

| Pendência | Impacto | Observação |
|---|---|---|
| Limites da CEAP de 2023–2025 | **Alto** | `REG_TX_002` responde `SemDado` para toda a base |
| Escritas (favoritar, contestar, varredura paga) | Médio | Entidades existem; falta autenticação e pagamento |
| Cliente do Senado | Médio | Conversor e testes prontos; falta decidir a interface |
| Limitação de taxa na API | Médio | Sem autenticação, só o teto de página protege hoje |
| Cabeçalhos de cache HTTP | Baixo | O CDN do [ADR-0001](../decisoes/ADR-0001-frontend-nextjs.md) é quem absorve o pico viral, e ele precisa de `Cache-Control` |

## 9. Próxima etapa

**Etapa 06 — Frontend Next.js**, sobre o contrato desta API e a taxonomia do ADR-0013. Os
quatro componentes obrigatórios: tag de gravidade nos três níveis, tag de abstenção, tag de
comprovante ausente e o bloco de cobertura do perfil.
