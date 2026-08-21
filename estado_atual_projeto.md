# Estado atual do projeto — handoff

**Gerado em:** 2026-08-21 · **Último commit:** `c9b5726` · **Branch:** `main` (sincronizada)

Este documento existe para que outra sessão retome sem reler o histórico. O que está aqui foi
**verificado contra o banco e o código**, não escrito de memória.

---

## 1. Onde estamos

O **Explanando** audita gastos da cota parlamentar (CEAP/CEAPS) cruzando dados públicos para
apontar inconsistências financeiras. As etapas 00 a 05 estão concluídas e a **06 (frontend)
está em andamento**.

O pipeline inteiro roda de ponta a ponta sobre dado real: coleta → ingestão → motor de regras
→ varredura → consolidação → API → interface.

| Etapa | Situação |
|---|---|
| 00 Fundação · 01 Contratos Câmara · 01b Contratos Senado | ✅ |
| 02 Domínio e EF Core · 03 Ingestão | ✅ |
| 04 Motor · 04b Calibragem · 04c Armazenamento · 04d Coleta completa | ✅ |
| 05 API pública de leitura | ✅ |
| **06 Frontend Next.js** | 🚧 **em andamento — ver §4** |
| 07 Observabilidade e deploy | ⬜ |

**Testes:** 666 no backend (417 domínio, 54 aplicação, 163 integração, 10 arquitetura, 22
ponta a ponta) + 17 no frontend.

---

## 2. Regras de negócio ativas

### 2.1 O ADR-0004 é a espinha dorsal — e é executável

O produto **descreve inconsistências, não imputa conduta**. Isso não é diretriz de redação: é
invariante que o domínio recusa violar.

`VocabularioDeCompliance.GarantirVocabularioTecnico` **lança exceção** se a descrição de uma
flag contiver termo acusatório. Os termos proibidos:

```
fraude, fraudulenta, fraudulento, crime, crimes, criminoso, criminosa,
corrupcao, corrupto, corrupta, lavagem, desvio, desviou, desviado,
roubo, roubou, propina, caixa dois
```

Três detalhes que já custaram bug e não podem ser desfeitos:

1. **Comparação por palavra inteira.** "descrição" contém "cri"; "discriminado" contém "crim".
2. **`ExpressoesPermitidas = ["desvio padrao"]`.** "Desvio Padrão" é termo *preferido* que
   contém termo *proibido*.
3. **Trecho entre aspas simples não é examinado.** Três lava-jatos —
   `MOREIRA LAVAGEM E POLIMENTOS`, `ESQUILOS LAVAGEM AUTOMOTIVA`,
   `RODO SERVICE LAVAGEM E CONSERVACAO` — derrubavam `REG_KYB_001`, porque a descrição
   interpola a razão social. **Custava 181 flags legítimas**, já que uma exceção descarta
   todas as flags daquele parlamentar. O filtro limita o que o *produto* afirma; nome de
   empresa que existe no mundo não é escolha de palavra nossa.

### 2.2 Postura permissiva (ADR-0007) — na dúvida, silêncio

Falta de dado **nunca** vira suspeita. `ResultadoDeCompatibilidade` tem três estados
(`Compativel`, `Incompativel`, `SemBaseParaAvaliar`) porque booleano forçaria escolher entre
acusar e inocentar quando a resposta honesta é "não sei".

### 2.3 A distinção que sustenta o produto inteiro

**Zero por falta de dado ≠ zero por nada encontrado.**

Cada regra declara em `IRegraAnomalia.Exige` o dado externo de que depende, e o motor mede
quantas despesas o têm. `CoberturaDoParlamentar.SilencioEhConclusivo` é o campo que a API
expõe e a interface consulta **antes de desenhar** — nunca a contagem de flags.

Na base real:

```
    REG_TX_001  SemAchados  flags=0   ← avaliou e está limpo
 >> REG_TX_002  SemDado     flags=0   ← não teve como avaliar
```

### 2.4 Dois números, nunca um (ADR-0009)

| | Volume (B2C) | Índice (B2B) |
|---|---|---|
| O quê | Soma em reais, estornos abatidos | Indicadores por 100 despesas |
| Piso amostral | Nenhum | **100 despesas** |
| Abaixo do piso | Continua no ranking | Sai, e o valor vem **`null`, não zero** |

A API tem **três** ordenações: `volume`, `indice` e `gravidade` — a última ordena só pelos
achados de gravidade alta, porque pelo total quem tem 14 achados graves fica abaixo de quem
tem zero. A escolha ficou com quem consome (questão 5, resolvida).

### 2.5 Assimetria Câmara × Senado (ADR-0012)

- **Glosa desconhecida é `null`, nunca zero.** Zero afirmaria que houve conferência e nada foi
  cortado. A interface omite a quebra de valores quando `GlosaConhecida` é falso.
- **O Senado não publica comprovante.** Tag inativa atribuindo a ausência à fonte, mais nota
  técnica no rodapé do Relatório Cidadão.
- **`REG_NET_004` roda sobre as duas casas juntas** — o Senado tem 82 parlamentares e o piso é
  100.
- **`LimiteCota` tem `Casa`**: CEAP e CEAPS são cotas distintas com valores próprios por UF.

### 2.6 Taxonomia visual (ADR-0013) — a cor é texto

Quatro pesos, cada um com **cor, forma e rótulo**. Contraste calculado, não estimado:

| Nível | % da base | Fundo | Texto | Ícone | Contraste |
|---|---|---|---|---|---|
| Baixa | 43,3% | `#F8FAFC` | `#334155` | `i` círculo | 9,90 AAA |
| Média | 53,4% | `#FEF3C7` | `#78350F` | `!` triângulo | 8,15 AAA |
| Alta | **3,3%** | `#FEE2E2` | `#991B1B` | `×` octógono | 6,80 AA |
| Não avaliado | — | `#F1F5F9` | `#475569` | `–` tracejado | 6,92 AA |

**O vermelho fica estritamente com os 3,3%.** Sem `uppercase` — caixa alta é ênfase, e ênfase
sobre achado descritivo é imputação pela tipografia. Botão de ação usa `#064E3B` (9,72); o
verde `#059669` reprova para texto (3,77) e serve só para elemento não textual.

> **Regra permanente:** toda cor nova passa pelo cálculo de contraste antes de entrar. Seis
> valores reprovaram na revisão, **três deles propostos depois de o problema já ter sido
> apontado nos outros três**.

### 2.7 As 12 regras

`REG_KYB_001` a `005`, `REG_TX_001` a `004`, `REG_GEO_002`, `REG_GEO_003` (motor por
parlamentar) e `REG_NET_004` (passada de rede, cruza parlamentares).

**Sem fonte de dado, não implementadas:** `REG_NET_001` (precisa do TSE), `REG_NET_002` (405
em quatro rotas), `REG_NET_003` (só o Senado tem endereço de escritório), `REG_GEO_001` (a
despesa não traz local nem hora).

---

## 3. Infraestrutura e banco

### 3.1 Stack

- **.NET 10** (SDK 10.0.302), Clean Architecture verificada por teste (ADR-0006)
- **PostgreSQL 17** + EF Core 10 + Npgsql, snake_case
- **Next.js 16 / React 19 / Tailwind v4** — Tailwind v4 configura por **CSS**, não pelo
  `tailwind.config.js` que o documento de identidade descrevia
- **HybridCache** (memória; ganha Redis via `EXPLANANDO_REDIS`)
- Repositório **privado** (ADR-0008)

### 3.2 Docker

`docker-compose.yml` na raiz sobe `explanando-postgres`, `explanando-redis` e
`explanando-localstack`. **Os três precisam estar no ar** para os testes de integração.

### 3.3 Banco — estado verificado

| Tabela | Linhas |
|---|---|
| `parlamentares` | **513** |
| `despesas` | **711.796** |
| `fornecedores` | 40.774 |
| `flags_anomalias` | 21.067 |
| `coberturas_regra` | 5.643 (11 regras × 513) |
| `consolidados_mensais` | 20.097 |
| **Tamanho** | **315 MB** |

Volume total: **R$ 802.407.895,78**. Taxa de sinalização: **2,96%**.

**Cabe no tier gratuito do Neon (512 MB) com 38% de folga.** Sem as otimizações da Etapa 04c a
base fecharia em ~520 MB — acima do limite. Progressão: 624 → 450 → **376 bytes por despesa**.

**Migration mais recente:** `20260821193814_CoberturaPersistida`.

> ⚠️ **A API não roda migrations na subida** — de propósito, API de leitura não mexe em
> esquema. Depois de criar migration, rodar:
> ```bash
> dotnet ef database update --project src/Explanando.Infrastructure --startup-project src/Explanando.Api
> ```
> Esquecer isso já causou um HTTP 500 que os 22 testes E2E não pegaram.

### 3.4 Comandos

```bash
# backend (a partir de backend/)
dotnet run --project src/Explanando.Worker -- coletar --legislatura 57   # ~12,6 h
dotnet run --project src/Explanando.Worker -- varrer                     # motor + rede
dotnet run --project src/Explanando.Worker -- consolidar [--meses N]     # sem --meses não apaga nada
dotnet run --project src/Explanando.Api --urls "http://localhost:5199"

# frontend (a partir de frontend/)
npm run dev      # espera a API em http://localhost:5199 (NEXT_PUBLIC_API sobrescreve)
npm test         # 17 testes
```

### 3.5 Armadilhas do ambiente que voltam a morder

1. **Heredoc do bash come barras invertidas**, mesmo com delimitador entre aspas. `\d` vira
   `d` em `GeneratedRegex`. **Use a ferramenta Write** para conteúdo com barra.
2. **`InvariantGlobalization=true`** faz `String.Normalize(FormD)` virar no-op silencioso. O
   `VocabularioDeCompliance` dobra acento por tabela explícita de caracteres.
3. **Value converter bloqueia LINQ.** `Competencia` vira `int` AAAAMM; `d.Competencia.Ano`
   não traduz. Compare o objeto de valor inteiro, ou use SQL direto.
4. **`ArtifactsPath` fora do OneDrive** — o Smart App Control bloqueava DLLs não assinadas.
5. **`byte[]` compara por referência** em C#. `HashUnico` precisa de `ComparadorDeHash`.
6. **Medir tamanho de tabela exige `VACUUM FULL`.** `DROP COLUMN` não devolve espaço.

---

## 4. Onde paramos exatamente

**Último commit:** `c9b5726 — Etapa 06: o frontend, com a taxonomia do ADR-0013 em componente
testado`. Árvore limpa, tudo empurrado.

### O que já existe no frontend

```
frontend/src/
  app/
    layout.tsx                     link "pular para o conteúdo", rodapé com ressalva
    page.tsx                       ranking com as três ordenações
    parlamentares/[id]/page.tsx    perfil + cobertura + despesas com achados
    globals.css                    tokens do ADR-0013 em @theme
  componentes/
    TagDeGravidade.tsx             três níveis, com forma e aria-label
    Abstencao.tsx                  TagDeAbstencao + BlocoDeCobertura
    Comprovante.tsx                botão/tag + ValoresDaDespesa + formatarReais
    componentes.test.tsx           17 testes
  lib/
    contrato.ts                    espelho tipado da API
    api.ts                         cliente com revalidação de 300 s
```

**Verificado contra a base real**: o perfil renderiza literalmente *"3 de 11 regras não
puderam ser aplicadas integralmente por ausência de dado público. A ausência de indicador nelas
não significa ausência de achado."*, e as tags "Não avaliado" e "Sem indicadores" convivem na
mesma lista.

### Os três próximos passos, na ordem em que eu ia codar

**1. `generateStaticParams` para ISR nas 513 páginas de parlamentar** — *o mais importante*

Hoje `/parlamentares/[id]` é `ƒ (Dynamic)`, renderizada sob demanda. O ADR-0001 diz que
**páginas indexadas no Google são o motor de crescimento** e que é a **CDN, não o Redis, que
absorve o pico viral**. Sem pré-renderização estática, a premissa de custo do projeto não se
sustenta num pico.

Em `frontend/src/app/parlamentares/[id]/page.tsx`:

```tsx
export async function generateStaticParams() {
  const pagina = await ranquear("volume", 1, 513);
  return pagina.itens.map((p) => ({ id: p.id }));
}
```

Cuidado: o build passa a exigir a API no ar. Vale um fallback que devolva `[]` quando a API não
responder, para o build não quebrar em CI sem backend.

Falta também `metadata` por parlamentar (`generateMetadata`) — título e descrição próprios são
o que torna a página indexável de verdade.

**2. Listagem completa de despesas com filtros laterais e paginação**

Hoje o perfil mostra só as 25 primeiras despesas **com achado**. Falta:

- Rota `/parlamentares/[id]/despesas`
- Sidebar fixa à esquerda (padrão Portal da Transparência): ano, tipo de despesa, com/sem
  indicador
- Paginação — a API já devolve `total` e `totalDePaginas`
- A API já aceita `?ano=&mes=&tipo=&comFlag=&pagina=&itens=`; **nada de backend é necessário**

**3. Cabeçalhos de cache HTTP na API**

`Cache-Control` nos endpoints de leitura. A CDN do ADR-0001 só absorve o pico se a resposta
disser que pode ser cacheada — hoje não diz. Fica em `EndpointsPublicos.cs`.

### Pendências maiores, fora da Etapa 06

| Pendência | Impacto | Nota |
|---|---|---|
| **Limites da CEAP 2023–2025** | **Alto** | `REG_TX_002` responde `SemDado` para toda a base. Estão nos Atos da Mesa; não vêm em API. **Precisa de curadoria humana** |
| **Tabela de limites da CEAPS** | Alto para o Senado | Idem. Você disse que ia montar |
| **Cliente do Senado** | Médio | Conversor e 68 testes prontos ([Etapa 01b](docs/registro/etapa-01b-contratos-senado.md)). Falta decidir a interface: `IFonteDeParlamentares` é por parlamentar, mas a CEAPS é **por ano, para todos de uma vez** |
| Catalogar os 4 tipos que apareceram | Baixo | Grafia exata registrada na [Etapa 04c](docs/registro/etapa-04c-armazenamento.md) §3.5 |
| Escritas (favoritar, contestar, varredura paga) | Médio | Entidades existem; falta autenticação e pagamento |
| Limitação de taxa na API | Médio | Sem autenticação, só o teto de 200 itens/página protege |
| `REG_NET_004` recalibrar | Médio | Os três limiares nunca foram confrontados com base grande |

---

## 5. Como o trabalho vem sendo feito

Cinco combinados que valem manter:

1. **Planejar antes de executar, e pedir autorização.** Instrução permanente do Gustavo.
2. **TDD obrigatório** (ADR-0005). Teste antes do código, nos quatro níveis.
3. **Registro por etapa** em `docs/registro/`. É o contexto que sobrevive entre sessões.
4. **Medir, não estimar.** Praticamente todo defeito sério desta base foi achado rodando
   contra dado real — a coleta expôs quatro que nenhum teste com dado inventado acharia.
5. **Comentário explica o *porquê*, não o *o quê*.** O código é denso em justificativa
   histórica: por que a glosa é nula, por que o vermelho é só dos 3,3%, por que o `last`
   ausente é suspeito. Isso é intencional e deve continuar.

### Documentos de referência

- **13 ADRs** em `docs/decisoes/` — quando um ADR contradiz `docs/especificacao/`, **o ADR
  vence**
- **12 registros** em `docs/registro/`, incluindo o
  [incidente de 2026-08-21](docs/registro/incidente-2026-08-21-despesas-vazia.md)
- **Contratos verificados** em `docs/contratos/` — Câmara, Senado e BrasilAPI, com fixtures
  escolhidas *pelas armadilhas*, não pelo caminho feliz
