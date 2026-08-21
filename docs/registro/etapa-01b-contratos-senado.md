# Etapa 01b — Spike de contratos do Senado

**Data:** 2026-08-21 · **Situação:** ✅ Concluída

---

## 1. Por que esta etapa existe

O [ADR-0011](../decisoes/ADR-0011-v1-e-o-plano-inteiro.md) trouxe o Senado para dentro do V1,
desfazendo o recorte "somente Câmara" do ADR-0002. Só que o Senado entrou **sem nenhum
contrato verificado** — a Etapa 01 tinha registrado, na tabela de pendências, "nada verificado
sobre o Senado".

Esta etapa faz para o Senado o que a Etapa 01 fez para a Câmara: bate nos endpoints reais,
baixa dado de verdade e mede, em vez de supor.

O resultado completo está em [`docs/contratos/senado.md`](../contratos/senado.md). Aqui fica o
que muda de decisão.

## 2. O achado que mais muda planejamento

**A coleta do Senado inteiro leva ~36 minutos. A da Câmara leva ~12,6 horas.**

| | Câmara | Senado |
|---|---|---|
| Requisições de despesa | 513 deputados × páginas | **1 por ano** |
| Consultas de CNPJ | ~68.000 | 3.580 |
| Tempo | ~12,6 h | **~36 min** |

Um ano inteiro de CEAPS vem numa única requisição, sem paginação: 23.615 registros e 10 MB em
5,2 segundos. O Senado inteiro custa menos que três deputados.

Isso inverte uma intuição que estava implícita no plano — a de que o Senado seria "mais do
mesmo, com o dobro de trabalho". Ele é a casa **barata** de coletar.

E o histórico vai mais fundo: 2019 responde com o mesmo esquema. A legislatura 57 da Câmara só
começa em 2023.

## 3. O que o Senado tem de melhor

- **98,6% dos fornecedores vêm com CNPJ**, e os **3.580 CNPJ distintos passam todos no dígito
  verificador**. Zero reprovações. As regras de KYB, que na Câmara se abstêm com frequência,
  aqui têm base quase completa.
- **`detalhamento`**: texto livre em 38,8% dos registros, descrevendo a despesa em palavras. A
  Câmara não tem nada equivalente. É insumo direto da Camada 2.
- **Escritórios de apoio com endereço completo** (78 de 83 com CEP). Isso **destrava
  `REG_NET_003`**, que na Câmara está marcada como sem fonte desde a Etapa 01.
- **`chefeGabinete`** nominal, onde a Câmara devolve 405 para qualquer rota de servidores.

## 4. O que o Senado tem de pior, e é grave para o produto

**Não existe link para o comprovante.** Nenhum dos treze campos é URL — verificado.

Na página da Câmara, "ver a nota" é o que transforma um indicador numa coisa que o leitor
confere sozinho. É a peça que sustenta a postura do
[ADR-0004](../decisoes/ADR-0004-nomenclatura-tecnica.md): o produto não acusa, ele mostra o
documento e deixa o leitor concluir. **No Senado essa peça não existe.**

Isso é uma decisão de produto pendente, não um detalhe técnico. A página de um senador vai ter
de dizer, de forma visível, que o comprovante não é publicado pela origem — senão a ausência
do link vai parecer falha nossa.

**E não existe glosa.** A Câmara entrega três valores (pedido, pago, cortado); o Senado
entrega um. O sinal da glosa, que aparece em ~1 de cada 100 notas da Câmara, simplesmente não
existe do lado de lá.

## 5. Armadilhas medidas

| Armadilha | Extensão | Consequência |
|---|---|---|
| Datas corrompidas na origem | 321 registros (1,36%), incluindo o ano **202** | Contamina `REG_TX_004` e qualquer série histórica |
| `data` diverge da competência | 2.971 (12,6%) | Provavelmente legítimo, mas confirma que competência ≠ data do documento |
| CNPJ malformado | 20 registros, 11 distintos | **Zero à esquerda não conserta**: só 1 dos 11 passa no DV depois do padding |
| `documento` não numérico | 16% | `REG_KYB_005` precisa se abster, não adivinhar |
| `tipoDespesa` nulo | 76 (0,3%) | `REG_KYB_001` sem base de comparação |
| Timeouts intermitentes | vários durante o levantamento | Nenhum 429. A infra falha e volta; exige retry com recuo |

A dos CNPJ malformados é a que quase me pegou: a leitura natural de um número de 13 dígitos é
"suprimiram o zero à esquerda". Testei os onze — **um** passa no dígito verificador depois do
padding. Os outros dez são corrupção real, e preenchê-los inventaria um CNPJ que existe e
pertence a outra empresa.

## 6. Duas consequências para o motor

### 6.1 `REG_NET_004` não roda no Senado sozinho

São 82 senadores, e a regra declara piso de 100 parlamentares. Ou a passada de rede roda sobre
**Câmara e Senado juntos** — 594 parlamentares, folgadamente acima do piso — ou a regra nunca
fala sobre o Senado.

Rodar junto é melhor e é de graça: a regra agrupa por fornecedor e nada nela exige que os
parlamentares sejam da mesma casa. Um fornecedor recém-aberto atendendo deputados **e**
senadores do mesmo partido é, se alguma coisa, mais notável que o caso de uma casa só.

### 6.2 Duas tabelas curadas novas

As oito categorias de despesa do Senado são **outras**, não as dezesseis da Câmara — e mais
grossas: a maior delas junta "locomoção, hospedagem, alimentação, combustíveis e
lubrificantes" numa categoria só. Isso enfraquece `REG_KYB_001` por construção, porque quase
qualquer CNAE é compatível com uma categoria tão ampla.

Faltam, e nenhuma das duas vem em API:

1. **CNAE × tipo de despesa do Senado**, com as 8 categorias;
2. **Limites da CEAPS por UF** — sem ela `REG_TX_002` não roda, exatamente como aconteceu na
   Câmara antes da curadoria da Etapa 03.

## 7. O que a ingestão vai precisar

- Cliente próprio: **dois hosts**, e envelopes de resposta inconsistentes entre endpoints do
  mesmo serviço (`despesas_ceaps` devolve array cru; `escritorios-apoio` devolve
  `{statusCode, msg, data}`).
- O cadastro legislativo responde **XML por padrão** — JSON só com sufixo `.json`.
- `codSenador` da CEAPS **é** o `CodigoParlamentar` do serviço legislativo: 72 de 72 conferem,
  zero divergências. A junção está resolvida.
- **Fonte histórica de senadores.** 10 dos 82 que gastaram em 2025 não estão na lista de "em
  exercício" — suplentes que assumiram e saíram. Sem isso, 12% de quem gastou fica sem nome e
  sem UF.
- O modelo de `Despesa` acomoda o Senado sem mudança: `ValorDocumento` e `ValorLiquido` ambos
  recebem `valorReembolsado`, e `ValorGlosa` fica zero. `TemDocumentoPublicado` fica sempre
  falso.

## 8. Pendências

| Pendência | Impacto | Observação |
|---|---|---|
| Tabela CNAE × tipo de despesa do Senado (8 categorias) | Alto para `REG_KYB_001` | **Precisa de decisão sua**, como foi na Câmara |
| Tabela de limites da CEAPS por UF | Alto para `REG_TX_002` | Não vem em API. Idem |
| Como a página comunica a ausência de comprovante | **Alto, de produto** | Sem isso, a falta do link parece falha nossa |
| Fonte histórica de senadores | Médio | 12% de quem gastou em 2025 |
| Passada de rede unificada Câmara + Senado | Médio | Destrava `REG_NET_004` para as duas casas |

## 9. Próxima etapa

A ingestão do Senado é uma etapa própria — cliente, tradutor, e as duas tabelas curadas. Mas
ela **não bloqueia** nada: a Câmara já está de pé, e a coleta completa dela é independente.
