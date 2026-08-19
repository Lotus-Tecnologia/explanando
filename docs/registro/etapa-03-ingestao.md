# Etapa 03 — Ingestão (Câmara + BrasilAPI)

**Data:** 2026-08-19 · **Situação:** ✅ Concluída

---

## 1. Objetivo

Construir a coleta que popula o banco: clientes HTTP resilientes para a Câmara e a BrasilAPI,
tradução para o domínio, resolução de fornecedor e persistência idempotente — com checkpoint
para que uma queda no meio dos 513 deputados não obrigue a recomeçar.

Tudo por TDD, com as respostas reais da Etapa 01 servindo de fixture no lugar da rede.

## 2. Definido

### A assinatura torna impossível esquecer a legislatura

`ListarDespesasAsync(idExterno, legislatura, ...)` recebe a legislatura como **parâmetro
obrigatório**, não opcional. A descoberta da Etapa 01 — sem `idLegislatura` a API responde
`200` com lista vazia — deixa de depender de alguém lembrar.

### Coleta vazia para deputado em exercício é alarme, não resultado

`ResultadoDaIngestao.AlertasDeSanidade` recebe uma mensagem explícita quando a fonte devolve
zero despesas para quem está em exercício. Suplente que nunca assumiu **não** gera alarme —
ruído ensina a ignorar alarme.

### Nota malformada vira rejeição contada, não exceção

Uma despesa com valores inconsistentes é recusada pelo domínio. O orquestrador captura,
registra em `Rejeitadas` com o motivo, e segue. Uma nota ruim não pode custar as outras 1.199
do mesmo deputado.

### Cache de fornecedor por execução

Um deputado repete os mesmos fornecedores dezenas de vezes. Consultar a BrasilAPI a cada nota
castigaria um serviço comunitário sem necessidade. O cache vive dentro da coleta de um
deputado; o banco cobre entre execuções.

### Checkpoint existe por tempo, não por correção

A idempotência já vem do `HashUnico`: reprocessar tudo não duplica nada. O checkpoint apenas
evita refazer ~6.000 requisições. Duas regras:

- **Checkpoint com alarme nunca vale.** Ele registra uma coleta que provavelmente falhou;
  tratá-lo como válido perpetuaria o buraco no banco.
- **Deputado que falhou não ganha checkpoint**, para que a próxima passada tente de novo.

### Resiliência calibrada por medição, não por hábito

Os números de `ServicosDeIntegracao` vieram da Etapa 01: nenhum `429` foi observado em
nenhuma das fontes, e o ganho de 16× veio do pool de conexões do `IHttpClientFactory`, não de
política de retry. A concorrência contida em 6 é **cortesia com serviço público e gratuito**,
não necessidade técnica. A BrasilAPI recebe menos tentativas e espera maior por ser um serviço
comunitário.

## 3. Executado

```
Application/Ingestao/
  FontesDeDados.cs      portas + DTOs da fonte (sem HTTP no vocabulário)
  Tradutor.cs           fonte -> domínio
  ServicoDeIngestao.cs  orquestra um deputado
  ServicoDeColeta.cs    percorre todos, com checkpoint

Domain/Ingestao/
  CheckpointDeIngestao.cs

Infrastructure/Integracoes/
  Camara/ClienteCamara.cs + ContratosDaCamara.cs
  BrasilApi/ClienteBrasilApi.cs
  ServicosDeIntegracao.cs   HttpClientFactory + Polly

Infrastructure/Persistencia/Repositorios/
  RepositorioDeIngestao.cs
  RepositorioDeCheckpoints.cs

Infrastructure/ServicosDaInfraestrutura.cs   ponto único de composição
```

Migration `CheckpointsDeIngestao` gerada, com índice único por `(parlamentar, legislatura)`
e índice em `com_alerta` para a consulta operacional "quais coletas terminaram com alarme?".

Os DTOs de ambos os clientes **omitem deliberadamente** os campos de dado pessoal que as
fontes entregam — `cpf`, `dataNascimento`, `municipioNascimento`, `redeSocial` e
`faixa_etaria` do sócio. O que não é desserializado não vaza para dentro do sistema por
descuido. Há teste por reflexão garantindo que `DeputadoDaFonte` e `SocioDaFonte` não ganhem
essas propriedades.

## 4. Testes

**221 testes, todos verdes. Build com 0 avisos.**

| Nível | Projeto | Testes |
|---|---|---|
| Unitário | `Domain.Tests` | 132 |
| Unitário | `Application.Tests` | 38 |
| Integração + E2E | `Integration.Tests` | 41 |
| Arquitetura | `Architecture.Tests` | 10 |

Os clientes HTTP são testados contra as respostas reais de `docs/contratos/`, servidas por um
`HttpMessageHandler` de teste. Determinístico, offline e fiel ao contrato de verdade.

`IngestaoPontaAPontaTests` fecha o circuito: HTTP → tradução → domínio → **Postgres real**,
provando que as peças encaixam. É onde aparecem erro de mapeamento e restrição de banco que
nenhum dublê em memória pega.

## 5. Descobertas

### 5.1 Um fake ingênuo derrubou o processo de teste

As fixtures reais trazem link `rel=next`. O `HttpMessageHandler` de teste devolvia sempre o
mesmo corpo, então o cliente paginava para sempre, acumulando resultados até o host de teste
cair — sem mensagem de erro útil, só "Falha no processo do host de teste".

Duas correções, e a segunda importa mais:

1. O fake passou a servir a fixture uma vez e página vazia depois.
2. **O cliente ganhou guarda contra laço**: guarda os caminhos já visitados e para se a
   resposta apontar para uma página anterior. Um serviço externo não precisa ser malicioso
   para causar isso — basta estar com defeito. Coberto por teste próprio.

### 5.2 Data sem fuso é armadilha de idempotência

O contrato traz `"2026-07-06T00:00:00"`, sem offset. Deixar o parser adotar o fuso da máquina
faria a mesma nota produzir `DataDocumento` diferente em servidores diferentes — e, como a
data entra no `HashUnico`, **a mesma despesa entraria duas vezes no banco** ao ser
reprocessada em outro lugar. O cliente fixa em UTC explicitamente, com teste.

### 5.3 Dois padrões novos de dado sujo, além do sentinela

Na amostra de 3 despesas reais apareceram:

| Padrão | Exemplo | Efeito |
|---|---|---|
| `cnpjCpfFornecedor` **vazio** | Passagem aérea, fornecedor "LATAM Airlines Brasil" | Despesa gravada com o nome, sem vínculo. O gasto continua contabilizado; as regras de KYB se abstêm |
| `codDocumento` **não numérico** | `BCEE1FC1-4CE1-4280-85B5-0C64EED1AD43` (SIGEPA) | `varchar(50)` já cobre; mas o campo não pode ser tratado como número |

### 5.4 `numDocumento` nem sempre é número de nota fiscal

Na mesma amostra: `8887`, `7308` e **`9572293573464`**. O terceiro é um bilhete aéreo, não
uma nota fiscal sequencial.

**Consequência direta para a Etapa 04:** `REG_KYB_005` procura numeração inicial baixa e
sequencial. Aplicá-la a passagem aérea produziria ruído — ou pior, silêncio, se a média for
distorcida por números de 13 dígitos. **A regra precisa se limitar a documento fiscal**,
filtrando por `codTipoDocumento`.

Isso não estava previsto na especificação e só apareceu ao olhar três despesas reais.

## 6. Pendências

| Pendência | Impacto | Quando resolver |
|---|---|---|
| `limites_cota` vazia — 27 valores precisam de curadoria | Alto para `REG_TX_002` | Etapa 04 — **decisão sua** |
| Tabela CNAE × tipo de despesa (24 linhas) | Alto para `REG_KYB_001` | Etapa 04 — **decisão sua** |
| `REG_KYB_005` precisa filtrar por tipo de documento (achado 5.4) | Alto | Etapa 04 |
| Calendário de recesso legislativo | Médio para `REG_GEO_003` | Etapa 04 |
| Worker ainda não dispara a coleta — o serviço existe e está registrado no container, mas o `BackgroundService` continua ocioso | Médio | Quando houver agendamento definido |
| Nenhuma coleta real executada contra os 513 deputados | Médio | Depois do motor de regras, para não popular o banco duas vezes |
| CI (GitHub Actions) | Médio | Próximo commit |
| `E2E.Tests` ainda vazio | Nenhum | Etapa 05, quando existir API HTTP |

## 7. Próxima etapa

**Etapa 04 — Motor de regras determinísticas.**

O domínio já responde as perguntas que o motor precisa fazer — `PossuiCnae`,
`EstavaIrregularEm`, `DiasDeAtividadeAte`, `EhMei`. A etapa encadeia essas perguntas num
pipeline de `IRegraAnomalia`, uma classe por regra, testável isoladamente.

Das 16 regras, 12 são viáveis com o dado que a ingestão traz. Duas delas dependem de tabelas
que **nenhuma API publica** e que precisam ser curadas antes: limite CEAP por UF e
compatibilidade CNAE × tipo de despesa.

Toda descrição de flag passa pelo `VocabularioDeCompliance`, que já recusa vocabulário
acusatório — o ADR-0004 é invariante de domínio desde a Etapa 02.
