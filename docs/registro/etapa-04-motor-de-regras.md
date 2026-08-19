# Etapa 04 — Motor de regras determinísticas

**Início:** 2026-08-19 · **Situação:** ⏳ Em andamento

---

## 1. Objetivo

Encadear as perguntas que o domínio já sabe responder — `PossuiCnae`, `EstavaIrregularEm`,
`DiasDeAtividadeAte`, `EhMei` — num pipeline de regras, uma classe por `REG_*`, e produzir
as `FlagAnomalia` que alimentam o score e o ranking público.

## 2. Definido

### Postura permissiva ([ADR-0007](../decisoes/ADR-0007-postura-permissiva-do-motor.md))

**Na dúvida, silêncio.** Falta de dado nunca vira suspeita. O resultado de compatibilidade
tem três estados — `Compativel`, `Incompativel`, `SemBaseParaAvaliar` — porque booleano
forçaria a escolher entre acusar e inocentar quando a resposta honesta é "não sei".

O motor vai achar menos do que acharia com postura restritiva. Isso é o desenho: cada falso
positivo é uma imputação indevida numa página pública indexada, com nome e foto.

### Repositório permanece privado ([ADR-0008](../decisoes/ADR-0008-repositorio-privado.md))

Com trava no CI que falha o pipeline se a visibilidade mudar.

## 3. Executado até agora

### As duas tabelas curadas

Eram a pendência que bloqueava `REG_KYB_001` e `REG_TX_002`. Ambas resolvidas.

**CNAE × tipo de despesa** (`Domain/Cotas/CompatibilidadeCnae.cs`) — cobre os 24 tipos que a
API publica. Compara por **prefixo** de CNAE (2 a 5 dígitos), não por código exato de 7: casar
a família inteira dispensa acertar a subclasse, que muda com o tempo. Basta **um** CNAE bater,
principal ou secundário — exigir o principal marcaria empresas de atividade mista, como o
posto da amostra real que tem "bares" entre os secundários.

**Limites da CEAP** (`Infrastructure/Persistencia/Semente/SementeDeLimitesCota.cs`) — os 27
valores por UF, semeados de forma idempotente.

### CI no GitHub Actions

Build, suíte completa com Testcontainers, verificação de modelo × migrations, e a trava de
visibilidade. Mudança só em `docs/` não dispara o pipeline, para não consumir cota de Actions
à toa.

## 4. Testes

**250 testes verdes**, e o pipeline passa no CI — inclusive os testes de integração, que
sobem Postgres em contêiner no runner.

A tabela de limites é testada **como dado**, não só como mecanismo: faixa plausível de valores
(guarda contra erro de ordem de grandeza), cobertura das 27 UFs sem repetição, e coerência
geográfica — RR maior que SP, AC maior que MG, DF o menor de todos. Se alguém editar a tabela
e inverter essa lógica, o teste reprova.

## 5. Descobertas

### 5.1 A tabela da CEAP não existe em nenhuma API

Não está nos dados abertos, e o mapa do portal de transparência a renderiza por JavaScript
sem expor os valores no HTML nem em chamada de rede alguma. Foi preciso encontrá-la no Guia
para Jornalistas do próprio portal da Câmara.

**Conferência:** os 27 valores foram extraídos duas vezes de forma independente, com
resultados idênticos, e os extremos batem com intervalo publicado por fonte jornalística
separada. A ordem também faz sentido físico — DF tem o menor valor por dispensar voo, RR o
maior por ser o mais distante.

### 5.2 A vigência dos valores não é publicada — e a suposição erra para o lado certo

A fonte traz os valores vigentes, mas não desde quando, nem o histórico. Assumimos
**2026-01-01**.

A consequência é assimétrica, e é por isso que a suposição é aceitável: se o reajuste ocorreu
depois de janeiro, as despesas anteriores serão comparadas a um teto **mais alto** que o real,
e `REG_TX_002` deixará de sinalizar alguns casos. Erra para menos, nunca para mais.

Despesas de 2023 a 2025 ficam **sem teto configurado**, e a regra se abstém sobre elas.

### 5.3 O CI acusou o erro errado

A primeira versão do job de migrations não rodava `restore`, então o comando falhava por
assets ausentes. Pior que a falha: a mensagem culpava o modelo em qualquer saída diferente de
zero — o log dizia "o modelo mudou sem migration" quando a causa era outra, mandando
investigar a coisa errada.

Corrigido inspecionando a saída do comando para distinguir os dois casos. A verificação também
mudou de job próprio para dentro do job de build, reaproveitando restore e build: em
repositório privado os minutos de Actions são cota.

### 5.4 A chave da tabela CNAE tinha ponto final e nunca seria encontrada

`Normalizar` remove ponto final do tipo de despesa, mas uma das 24 chaves foi escrita com ele.
A entrada existia e era inalcançável — `REG_KYB_001` se absteria em silêncio para
"aquisição ou loc. de software". Pego pelo teste que exige prefixo não vazio para todo tipo
coberto.

## 6. Pendências

| Pendência | Impacto | Observação |
|---|---|---|
| As 12 regras `REG_*` ainda não existem | — | É o corpo desta etapa |
| `REG_KYB_005` precisa filtrar por tipo de documento | Alto | Achado da Etapa 03: bilhete aéreo tem 13 dígitos e não é nota sequencial |
| Limites da CEAP de 2023 a 2025 | Médio | `REG_TX_002` só alcança 2026 até que sejam levantados nos Atos da Mesa |
| Confirmar a vigência assumida (2026-01-01) | Baixo | A suposição erra para menos; ver 5.2 |
| Calendário de recesso legislativo | Médio | Necessário para `REG_GEO_003` |
| Medir e exibir a taxa de abstenção do motor | Médio | Se `REG_KYB_001` se abstiver em 80% das despesas, a tabela está pobre e isso tem de ser visível |
| Nenhuma coleta real executada contra os 513 deputados | Médio | Depois do motor, para não popular o banco duas vezes |

## 7. A seguir

Implementar as regras em ordem de valor: primeiro as que já têm todo o dado
(`REG_KYB_002/003`, `REG_TX_001/003/004`, `REG_GEO_002`), depois as que dependem das tabelas
recém-curadas (`REG_KYB_001`, `REG_TX_002`), e por fim `REG_KYB_004` e `REG_NET_004`.

Cada regra entra por TDD, com casos construídos a partir de despesas reais das fixtures.
