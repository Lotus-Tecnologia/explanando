# Etapa 16 — O servidor empacotado, o Senado na base, a segunda auditoria e o card

**Data:** 2026-09-25 e 2026-09-26
**Decisões:** [ADR-0019](../decisoes/ADR-0019-tudo-numa-vps.md) (executado, com duas correções — §6)
**Roteiro:** [desdobramento.md](../desdobramento.md), reescrito

A VPS da Oracle deu "Out of capacity" e ficou para a madrugada. Esta etapa fez tudo o que não
dependia dela, na ordem combinada: empacotar o servidor, carregar o Senado, uma segunda
auditoria (código e tela a tela), o card de compartilhamento e a revisão de segurança.

---

## 1. O servidor empacotado

| | |
|---|---|
| `deploy/compose.yml` | Postgres 17 afinado, `migracoes` → `api` → `web`, `cloudflared`, e as tarefas `worker` e `backup` |
| Imagens | API e worker compilam **cruzado** para arm64 (`$BUILDPLATFORM` + `-a $TARGETARCH`): 80 MB e 71 MB. O frontend constrói na própria VPS |
| Next | `output: "standalone"`; saíram `@opennextjs/cloudflare`, `wrangler`, `open-next.config.ts`, `public/_headers` e o workflow de deploy no Workers |
| Entrada | **Cloudflare Tunnel** — nenhuma porta aberta na VPS |
| Backup | `pg_dump` + contagens no mesmo instante → R2; `conferir.sh` restaura e exige as mesmas linhas; `restaurar.sh` com duas travas |
| Agenda | Timers do systemd: ciclo, cadeiras, Estado, backup, conferência |
| Worker | Comando novo `migrar` (só o esquema), para a API subir depois dele |

**Testado de ponta a ponta nesta máquina**, com o compose de produção (projeto
`explanando-teste`) contra a base completa:

| Medida | Valor |
|---|---|
| Dump comprimido | 72–75 MB |
| Restauração do dump | 5 s |
| `backup.sh` (S3 local no lugar do R2) | 11 s |
| `conferir.sh` | 1.271.567 linhas em 25 tabelas, idênticas ao instante do dump, 8 s |
| Build do frontend | 982 páginas, 2 min 33 s |
| `sitemap.xml` | 976 endereços |
| Memória da pilha ociosa | ~500 MB (Postgres 69, API 320, Next 93) |

### As armadilhas que só o teste pegou

1. **O segredo de build do compose lê o ambiente do processo, não o `.env`.** Sem exportar, o
   build saiu sem a chave interna e quebrou com 429 na página 245 de 982. O `atualizar.sh`
   exporta.
2. **O build pede mais do que o balde interno da API** (2.400/min) em menos de 20 s — mesmo com
   a chave. O cliente do frontend passou a esperar o `Retry-After` e repetir (até 5 vezes, teto
   de 60 s: o balde repõe tudo de uma vez a cada minuto, e com teto de 15 s ainda falhavam 49
   páginas).
3. **`.wrangler/` (417 MB) entrava no contexto de build** — sobra do adaptador; excluída.
4. **`public/` vazia some do git**, e o `COPY` do estágio final quebrava.
5. **O `bash` do painel de preview é o do WSL**, e o Git Bash converte `/tmp` em caminho do
   Windows — só afetam quem roda o teste local; anotados no roteiro.

### A lacuna que o empacotamento revelou

**O `ciclo-mensal` só coletava a Câmara.** A `ServicoDeColeta` injetada era a dela; a do Senado
só existia montada à mão dentro de `coletar-senado`. Com o agendador da VPS disparando só o
ciclo, a despesa nova dos senadores nunca entraria sozinha. Agora `ColetasDoCiclo` traz uma
coleta por casa, e há teste que resolve o ciclo pelo contêiner e conta as duas.

## 2. O Senado na base local

`coletar-senado`: **118 senadores, 78.507 despesas, R$ 121.516.838,78, em 38 min**, sem rejeição
nem falha. Depois `importar-tse` (agora **540** candidaturas ligadas — 30 senadores), `varrer` e
`consolidar`. A base foi a **458 MB**, 790.303 despesas, 25.117 flags.

**Só 30 dos 118 senadores ligam ao TSE de 2022**: os eleitos em 2018 estão em outro lote, e os
suplentes em exercício concorreram como suplentes. A tela trata isso como ausência (cobertura
`SemDado` em `REG_NET_001`), não como zero.

## 3. A segunda auditoria

Quatro frentes (motor e leituras; segurança e ingestão; telas da cota; comparador, feeds e
Estado), com navegador e consultas ao banco. **41 achados.** A primeira tentativa, com onze
agentes em paralelo, estourou o limite de uso duas vezes antes de devolver qualquer coisa; a
que funcionou teve quatro, dois de cada vez.

### O que foi corrigido — backend

| # | Achado | Correção |
|---|---|---|
| 10 | **O mapa da rede publicava o CPF inteiro** de fornecedor pessoa física (um passava no critério: 2 gabinetes, R$ 83 mil) | `Documento.Formatado`, com teste |
| — | **O CPF do dono do MEI vinha no nome**: a Receita grava "NOME 12345678901" e a Câmara repete. 641 fornecedores, 2.885 despesas, e o número ia para tela, achados e RSS | `CpfEmTexto` mascara na criação da despesa e do fornecedor; migração `MascararCpfNosNomes` corrige o que já estava gravado (inclusive as descrições dos achados). Achado nosso, ao conferir o #10 |
| 1 | `REG_GEO_003` dizia "sem achados" para os 118 senadores sem examinar nenhuma despesa (a rubrica do Senado junta hotel e combustível) | Novo dado exigido `NaturezaDaDespesa` |
| 2 | `REG_TX_001` e `REG_TX_003` descartavam as 184.280 passagens da Câmara sem documento do fornecedor e declaravam cobertura total | Novo dado exigido `DocumentoDoFornecedor` |
| — | `REG_KYB_005` só olha fornecedor cadastrado e declarava não precisar de nada | Passa a exigir `CadastroDoFornecedor` |
| 5 | Abaixo do piso, as parcelas do índice reconstruíam na API o índice que o perfil se recusa a publicar | `PontosNoIndice` nulo |
| 7, 8, 9 | Página enorme e ano inválido davam 500; despesas de parlamentar inexistente davam 200 vazio | Teto de página, 400 para o ano, 404 |
| 13 | Nenhum cabeçalho de segurança na API | nosniff, `DENY`, `no-referrer`, CSP `default-src 'none'` |
| 15 | A importação do TSE apagava o ano e regravava sem transação | Uma transação só, dentro da estratégia de retentativa |
| 17 | Sem string de conexão, a API subia contra localhost com a senha de dev | Fora de Development, não sobe |

Depois das mudanças de cobertura, a base foi re-varrida: **as mesmas 25.117 flags**, e a
cobertura passou a dizer a verdade — `REG_TX_001` saiu de "sem achados" para "cobertura
parcial" em 361 deputados; `REG_GEO_003`, em 104 senadores.

### O que foi corrigido — frontend

Dezoito achados, entre eles: contraste do tema escuro (1,52:1 → 11,7:1); percentuais de
gravidade escritos à mão que não batiam com a base; "regra violada" no ranking (ADR-0004); busca
que nunca mostrava senador; plural quebrado ("Os 1 achado"); a ilha de mudanças do Estado, que
chamaria `http://localhost:5199` em produção (agora pede ao próprio site); abas do mapa de poder
com "0" onde não há dado; listas cortadas a 375 px; 334 links focáveis dentro de um SVG com
`role=img`; título de despesa com 239 caracteres; sitemap com `lastmod` inventado. E o "(N
vagas)" do Senado, que contava pessoas: virou "(N com despesa)".

### O que foi levado ao Gustavo

Ver [pendências §3](../pendencias.md): ociosidade da Oracle, CPF do fornecedor PF gravado
inteiro, `REG_TX_001` em passagens do Senado, `REG_KYB_001` em aluguel pago a holding,
denominador do índice, "Alerta de compliance" nas descrições, "prevenção à fraude" na política,
o lote do TSE de 2018, o grafo no celular.

## 4. O card de compartilhamento

`app/parlamentares/[id]/opengraph-image.tsx`: 1200×630, foto oficial (ou iniciais), volume com
o número de despesas, índice (ou "não calculado" abaixo do piso — nunca zero), achados de alta
relevância, e **a ressalva na própria imagem**: "Achados técnicos sobre dados públicos. Não são
juízo de conduta." O texto vem de `lib/cartao.ts`, testado. A rota sai dinâmica, com
`s-maxage=3600` para a Cloudflare guardar.

## 5. Segurança

| | |
|---|---|
| Cabeçalhos no site | CSP (com `'unsafe-inline'` em script — o nonce obrigaria toda página a ser dinâmica), `DENY`, nosniff, Referrer-Policy, Permissions-Policy, HSTS, sem `X-Powered-By` |
| Dependências | **Next 16.3.2 tinha dois avisos críticos** de execução remota — em servidor Windows e na otimização de imagem com AVIF; nenhum dos dois nos atingia (Linux, `unoptimized: true`), mas foi a 16.3.6. `sharp` junto. `npm audit`: 0. NuGet: nenhum pacote vulnerável |
| Rede | Túnel: nenhuma porta aberta; Postgres sem porta publicada, nem em localhost |
| Balde interno | Qualquer visitante pode gastá-lo pelas rotas de espaço aberto (comparador, filtros de despesa). Mitigação: regra de rate limiting da Cloudflare — no roteiro §6.2 |

## 6. Duas correções ao ADR-0019

- **§5 (memória e ociosidade) não se sustenta.** O Postgres só ocupa a memória das páginas que
  leu; a pilha ociosa usa ~4% de 12 GB. A proteção contra a recuperação da instância tem de vir
  de outro lugar — decisão no roteiro §9.
- **"Abrir 80/443" deixou de ser passo.** Com o túnel, não há porta a abrir.

## 7. Números

| | |
|---|---|
| Testes no backend | **1.041** (582 domínio, 132 aplicação, 10 arquitetura, 222 integração, 95 ponta a ponta) |
| Testes no frontend | **306** |
| Parlamentares | 631 (513 + 118) |
| Despesas | 790.303 · R$ 923.924.734,56 |
| Flags | 25.117 |
| Base | 458 MB |

## 8. Depois do relatório: as decisões de 26/09

O Gustavo seguiu a recomendação em todas as decisões, menos a ociosidade da Oracle, que ficou
para avaliar depois. Tudo entrou no mesmo dia, com a base re-varrida (**motor versão 6**):

| Decisão | Medido depois |
|---|---|
| **CPF do fornecedor PF guardado mascarado** (`***NNNNNN**`), validado com os 11 dígitos só na leitura | 5.748 despesas, 325 CPFs → 325 máscaras distintas. O CPF entra no hash de deduplicação, então a migração o recalcula; antes, a fórmula em SQL foi conferida contra o hash do C# nas **790.303** despesas — 790.303 iguais — e os hashes novos continuam todos únicos |
| `REG_TX_001` não olha passagem | 2.009 → **942** flags |
| `REG_KYB_001` aceita holding (6462/6463) no aluguel do Senado | 6.927 → **6.854**; o topo do ranking de índice do Senado mudou |
| **Cobertura de fornecedor** ao lado do índice; piso de 50% para o ranking por índice | Câmara: **406 de 498** no ranking por índice (os outros seguem no de volume, com a fração na tabela); Senado: 86 de 86 |
| Descrições abrem com o fato | Zero "Alerta de compliance" / "Fator de risco" |
| Política: "transparência e controle social" | Conferido na lei: "prevenção à fraude" só existe no art. 11, II, "g" (dado sensível). Base legal inalterada. Adendo no ADR-0014 |
| Grafo no celular | Abre na lista abaixo de `sm` |

Total de flags: 25.117 → **23.977**. Testes: backend **1.055**, frontend **312**.

**A instância que saiu em 26/09 foi uma `VM.Standard.E2.1.Micro`** (1/8 OCPU, 1 GB, x86) — não
serve para a produção (roteiro §3.1), e foi criada com outra chave SSH. A A1 segue sendo tentada.

