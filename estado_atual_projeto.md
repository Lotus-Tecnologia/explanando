# Estado atual do projeto — handoff

**Atualizado em:** 2026-09-25 · **Branch:** `main` · **Último commit:** `c008a35`

Este documento existe para que outra sessão retome sem reler o histórico. O que está aqui foi
**verificado contra o banco e o código**, não escrito de memória. Detalhe de cada etapa está em
[`docs/registro/`](docs/registro/README.md); o porquê de cada decisão, em
[`docs/decisoes/`](docs/decisoes/README.md).

---

## 1. O que é o produto

O **Explanando** audita os gastos da cota parlamentar (CEAP da Câmara, CEAPS do Senado)
cruzando dados públicos — Câmara, Senado, Receita (via BrasilAPI), TSE e SIORG — e aponta
**inconsistências, sem imputar conduta**. Tem duas frentes:

1. **A cota:** ranking por casa, perfil do parlamentar, despesas com os achados de um motor de
   regras determinístico, comparador, feed RSS, sócios em comum, votação.
2. **O mapa do Estado** (adaptação do [CivLab](https://graph.civlab.org/us) ao Brasil): 96.658
   unidades da administração federal em grafo radial, quadro de pessoal em contagens, e o
   **mapa de poder** — 140 cadeiras de comando (Presidente, Vice, 38 ministros, Mesas,
   lideranças, presidências de comissão), de quatro fontes oficiais.

Repositório `Lotus-Tecnologia/explanando`, **privado** (ADR-0008). Dono: Gustavo Lira Criado.

---

## 2. Onde paramos

### 2.1 Todas as etapas e fases

| | Situação |
|---|---|
| Etapas 00 a 14 (fundação → mapa de poder) | ✅ |
| **Etapa 15 — plano de execução, fases 1 a 7, e auditoria** | ✅ ([registro](docs/registro/etapa-15-plano-de-execucao.md)) |
| Fase 8 — alertas por e-mail e Telegram | ⏸ depende da VPS e de provedor de e-mail |
| **Deploy** | ⏸ **a VPS da Oracle ainda não foi criada** (ver §4) |

O [plano de execução](docs/plano-de-execucao.md) traz cada fase com o que entrou e o que foi
medido. Resumo do que existe na tela hoje:

| Fase | Onde se vê |
|---|---|
| 1 · "De onde vem o índice" (fatia do peso por regra) + resposta do gabinete ao lado do achado | Perfil, página da despesa |
| 2 · Comparador (mesma casa, URL canônica, não indexado) | `/comparar/{a}/{b}`, "Comparar com…" no perfil |
| 3 · RSS por parlamentar e geral (data = entrada da despesa, lida do UUID v7) | `/parlamentares/{id}/feed.xml`, `/feed.xml` |
| 4 · Quem mais recebeu, ano a ano (ano parcial marcado **por parlamentar**) | Perfil |
| 5 · Sócios em comum (nome do sócio publicado desde 25/09; CPF nunca) | Perfil |
| 6 · Investigação do TSE | [`docs/contratos/tse.md`](docs/contratos/tse.md) |
| 7 · Eleição de 2022 na base + **REG_NET_001** + "De onde vieram os votos" | Perfil, índice, RSS |

### 2.2 Números da base local (Docker), 2026-09-25

| | |
|---|---|
| Parlamentares | **513 deputados** (só a Câmara — o Senado não está na base local) |
| Despesas | 711.796 · R$ 802.407.895,78 |
| Flags | 21.121 · motor **versão 5**, 12 regras + REG_NET_004 na passada de rede |
| REG_NET_001 | **73 achados em 40 deputados** |
| Candidaturas 2022 ligadas | **510 de 513** (3 com nome civil diferente no TSE ficaram de fora) |
| Votos por município / doações | 152.859 / 15.461 (12.845 a parlamentares e 2.767 de sócios; 151 são as duas coisas) |
| Grafo do Estado | 96.658 unidades · 759.351 vínculos de pessoal · 140 cadeiras |
| Tamanho | 397 MB — **não cabe mais na Neon gratuita (512 MB)**, que é o motivo do ADR-0019 |

> **A Neon tem o Senado e a base local não.** A Neon (produção antiga) guarda 631
> parlamentares — 513 + **118 senadores** —, mas nada do grafo, das cadeiras nem do TSE. Ao
> subir a VPS, o Senado vem de uma coleta nova (`coletar-senado`) ou de um dump da Neon.

**Testes:** backend **1.015** (567 domínio, 130 aplicação, 10 arquitetura, 220 integração,
88 ponta a ponta) + frontend **272**. Build de produção do Next verde (513 perfis, 336
unidades do Estado, feeds, comparador sob demanda).

### 2.3 Rodando local

```bash
# Docker: postgres, redis, localstack
docker compose up -d

# API na 5199 (a partir de backend/). A chave interna vem do frontend/.env.local.
PORT=5199 ASPNETCORE_ENVIRONMENT=Development RateLimit__ChaveInterna=<chave> dotnet run --no-launch-profile --project src/Explanando.Api

# Frontend na 3000 (a partir de frontend/) — o .env.local aponta para a API de produção,
# então a variável tem de vir da linha de comando:
NEXT_PUBLIC_API=http://localhost:5199 npx next dev -p 3000
```

Comandos do worker (a partir de `backend/`, `dotnet run --project src/Explanando.Worker -- <cmd>`):
`coletar`, `coletar-senado`, `varrer`, `consolidar`, `ciclo-mensal`, `coletar-estado`,
`coletar-pessoal --ano A --mes M`, `coletar-cadeiras [--se-vencido]`,
**`importar-tse --ano 2022 --pasta <caminho ABSOLUTO>`** (o `dotnet run` troca a pasta de
trabalho; caminho relativo falha). O worker aplica as migrations ao subir; a API não.

**Arquivos do TSE:** `node ferramentas/baixar-tse.mjs` baixa ~1 GB para `dados/tse/` (fora do
git). O CDN do TSE (Akamai) recusa curl, PowerShell, Python e HttpClient; **só o `fetch` do
Node passa**.

---

## 3. Regras de negócio que não se negociam

### 3.1 ADR-0004 — descreve, não imputa. E é executável.

`VocabularioDeCompliance.GarantirVocabularioTecnico` **lança exceção** se a descrição de uma
flag tiver termo acusatório (fraude, crime, corrupção, lavagem, desvio, roubo, propina, caixa
dois e variações). Comparação por palavra inteira; "desvio padrão" é permitido; **trecho entre
aspas simples não é examinado** (nome de empresa como "LAVAGEM E POLIMENTOS" derrubava 181
flags legítimas).

### 3.2 Ausência não é fato (ADR-0007, ADR-0009)

- **Zero por falta de dado ≠ zero por nada encontrado.** Cada regra declara em `Exige` o dado
  de que depende; `CoberturaDoParlamentar.SilencioEhConclusivo` é o que a tela consulta
  **antes** de desenhar. Nulo e vazio são coisas diferentes em todo o código (ex.: sem
  campanha ligada, `DoacoesDaCampanha` é `null`; campanha sem doação, lista vazia).
- **Dois números, nunca um:** volume (reais, sem piso) e índice (densidade **ponderada**: peso
  dos achados — alta 3, média 2, baixa 1, vezes a confiança — a cada 100 despesas; piso de 100
  despesas, abaixo disso `null`, nunca zero).

### 3.3 Casamento literal, nunca aproximado

Onde houver ligação entre cadastros, a chave é exata ou a ligação não existe. **Distância de
edição não entra em lugar nenhum.** `NomeNormalizado` só tira acento, caixa, hífen e espaço
duplo. Sócio ↔ doador é **compatibilidade** (nome idêntico + os 6 dígitos de CPF que a Receita
publica), confiança máxima 0,85, e a tela diz "compatível", nunca "é".

### 3.4 Dado pessoal

- CPF de sócio chega mascarado da Receita (`***NNNNNN**`). O do doador, que o TSE publica
  **inteiro**, é mascarado **na leitura** (`CpfParcial.DeCpfCompleto`) e nunca é gravado.
- **Não coletamos o CPF do parlamentar** (a política de privacidade promete).
- Nome de sócio aparece **só** no bloco "Sócios em comum"; nome de doador, **só** quando ele é
  compatível com sócio de fornecedor pago pelo mesmo parlamentar.

### 3.5 As casas não se comparam (ADR-0012)

Câmara e Senado têm rankings, pisos e cotas separados. Glosa desconhecida é `null`, nunca zero.
O Senado não publica comprovante.

### 3.6 A cor é texto (ADR-0013)

Vermelho só para gravidade **alta**. Toda gravidade tem cor + forma + rótulo. Barras de
composição, escada e votação **não têm cor de alerta** — crescer ou gastar na base não é achado.

### 3.7 As regras do motor (versão 5)

Por parlamentar: `REG_KYB_001` a `005`, `REG_TX_001` a `004`, `REG_GEO_002`, `REG_GEO_003`,
**`REG_NET_001`** (sócio do fornecedor ↔ doador da campanha, uma flag por fornecedor).
Passada de rede: `REG_NET_004`. **A varredura individual apaga só os códigos das próprias
regras** e o score conta as flags de rede; a passada de rede devolve os afetados, que são
re-varridos (defeito corrigido em 24/09 — 17 deputados tinham índice menor que a página).

---

## 4. O próximo passo: a VPS

[ADR-0019](docs/decisoes/ADR-0019-tudo-numa-vps.md): tudo numa **VPS Always Free da Oracle**
(Ampere A1, ARM, 2 OCPUs e 12 GB), Postgres local no lugar da Neon, **Cloudflare como CDN**
(não mais como runtime — sai o `@opennextjs/cloudflare`), backup diário **fora da máquina**
(R2) com teste de restauração.

**Situação:** o Gustavo criou a conta na Oracle e a chave SSH (`$HOME\.ssh\oracle_explanando`).
A criação da instância deu **"Out of capacity for shape VM.Standard.A1.Flex in AD-1"** —
São Paulo sem ARM livre. Ele vai tentar de madrugada, eventualmente com 1 OCPU / 6 GB (dá para
crescer depois) ou convertendo a conta para Pay As You Go. Lembretes para quando a máquina
existir: abrir 80/443 na Security List **e** no iptables da imagem Ubuntu da Oracle; reservar IP
público; a regra de ociosidade (7 dias abaixo de 20% em CPU, rede e memória).

### O que dá para adiantar sem a VPS (proposto ao Gustavo, aguardando o "vai")

1. **Empacotar o servidor:** compose de produção (Postgres 17 afinado para 12 GB, API, worker,
   Next), Next em `output: standalone` sem o adaptador do Cloudflare, imagens multi-arquitetura
   (arm64), script de backup para o R2 com teste de restauração, testar tudo local, e reescrever
   [`docs/desdobramento.md`](docs/desdobramento.md) (hoje descreve Cloud Run + Neon) como roteiro
   da VPS.
2. **Carregar o Senado na base local** (`coletar-senado`) para testar as duas casas juntas.
3. **Card de compartilhamento** por parlamentar (imagem para WhatsApp/X).
4. **Revisão de segurança** antes do público (cabeçalhos, rate limit atrás do proxy).

---

## 5. Decisões e pendências

### Tomadas pelo Gustavo em 24–25/09
- ADR-0020: discussão pública e notas da comunidade **adiadas** — gatilho de 50 mil
  visitantes/mês por 3 meses, entre outros.
- ADR-0021: narrativa por IA **só com receita** ≥ 3× o custo projetado, por 3 meses.
- Publicar o nome do sócio em comum. Baixar e carregar o TSE.

### Abertas, dele
- Alerta de "gasto em reduto eleitoral" (o dado existe; pagar fornecedor da base não é achado).
- `REG_TX_001` no Senado; limites da CEAP 2023–2025 e da CEAPS por UF (`REG_TX_002` segue
  `SemDado`); avaliação de legítimo interesse; revisão jurídica da política.
- DNS no Cloudflare, bucket do R2, a VPS. INLABS (nomeações no DOU).

### Pequenas, minhas
Escritórios de apoio do Senado (`REG_NET_003`); casa na chave de `checkpoints_ingestao`; as 62
despesas rejeitadas por glosa; senadores de 2018 (outro lote do TSE). Lista completa em
[`docs/pendencias.md`](docs/pendencias.md).

---

## 6. Armadilhas do ambiente que voltam a morder

1. **Processo .NET rodando trava o build** (`MSB3027`): matar a API antes de testar.
2. **`InvariantGlobalization=true`**: `string.Normalize(FormD)` não tira acento. Use
   `NomeNormalizado`.
3. **Heredoc do bash** come barra invertida e às vezes falha no parse com texto grande; para
   arquivo novo, prefira a ferramenta de escrita.
4. **Value converter bloqueia LINQ** (`Competencia` vira `int` AAAAMM) — agrupar em memória.
5. **Cache de dados do Next em dev** (`frontend/.next/dev/cache/fetch-cache`) mostra dado velho
   depois de mudar a API; apagar a pasta ou reiniciar o `next dev`.
6. **`next/script` com `beforeInteractive` quebra o script de tema** — o `<script>` cru no
   layout fica, e o aviso de "script tag" no 404 é só de desenvolvimento.
7. **gov.br responde 404 a cliente sem cabeçalhos de navegador**; o TSE responde 403 a tudo que
   não seja navegador ou Node.
8. **`dotnet ef`** precisa de `--startup-project src/Explanando.Api`.
9. **Faixa de `IdExterno` repetida** entre testes de integração derruba a suíte inteira.
10. **Datas**: data de documento chega como meia-noite UTC — usar `formatarDataDoDocumento`
    (UTC); instantes usam `formatarMomento` (Brasília).

---

## 7. Como o trabalho é feito aqui

- **Papel:** o Gustavo pede que eu atue como **sócio e diretor/desenvolvedor** — avaliação
  técnica e de negócio, recomendação clara, não lista de opções.
- **Português**, commits sem acento, corpo detalhado, terminando em
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Medir antes de afirmar.** Quase todo defeito sério foi achado rodando contra dado real. Não
  escrever nome de ocupante de cargo de memória: tem de vir de leitura ao vivo.
- **TDD** nos quatro níveis (ADR-0005); arquitetura verificada por teste (ADR-0006).
- **Comentário explica o porquê**, com a medição que motivou — o código é denso nisso de
  propósito.
- **Registro por etapa** em `docs/registro/`; ADR para decisão estrutural.
- **Não crio conta nem digito credencial** (INLABS, Oracle, Cloudflare, Resend são dele).
  A pasta `ref/` não é versionada. `backend/.env.neon` e `frontend/.env.local` são segredos locais.
