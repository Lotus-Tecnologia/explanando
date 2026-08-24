# ADR-0008 — O repositório permanece privado

**Data:** 2026-08-19 · **Situação:** Aceita

## Contexto

O repositório contém a lógica de detecção de inconsistências, as tabelas curadas que a
calibram e fixtures com dado real de fornecedores identificados por CNPJ.

Abrir o código de um projeto de transparência tem apelo óbvio. Mas aqui ele traz dois riscos
concretos: expõe exatamente quais padrões o motor procura, para quem tenha interesse em
evitá-los; e um repositório aberto por engano é praticamente impossível de fechar de volta,
porque forks, caches de busca e espelhos sobrevivem ao fechamento.

## Decisão

**O repositório é privado e permanece privado.** A decisão de abri-lo, se um dia for tomada,
é deliberada e não pode acontecer por descuido de configuração.

Para que não aconteça por descuido, o CI tem um job dedicado — `visibilidade` — que **falha o
pipeline** se `github.event.repository.private` deixar de ser verdadeiro. Não impede a
mudança, mas garante que ela apareça em vermelho no próximo push em vez de passar despercebida.

## Atualização de 2026-08-22 — o repositório pertence à Lotus Tecnologia

O repositório saiu da conta pessoal (`GustavoCriado01`) para a organização
**`Lotus-Tecnologia`**. A decisão de visibilidade acima não muda: ele continua privado, e o
job `visibilidade` lê `github.event.repository.private`, que independe de quem é o dono.

O motivo é o mesmo que sustenta este ADR — blindagem —, aplicado à titularidade em vez da
visibilidade. A [Análise de Lacunas](../especificacao/Explanando_Analise_Lacunas_B2B.md) §1
separa as operações para *"blindar os contratos corporativos de eventuais polêmicas geradas
pelos usuários"*: a API B2B é comercializada sob a marca Lotustech, por uma pessoa jurídica.
**Código com a propriedade intelectual numa conta pessoal enquanto o contrato é assinado pela
PJ é uma incoerência que não aparece hoje e aparece na due diligence de fornecedor de um
banco** — que pergunta, por escrito, quem detém o motor de regras.

Somam-se três razões práticas: a organização sobrevive à pessoa, os minutos de Actions são
despesa do CNPJ, e o plano Free da organização tem repositório privado ilimitado, então a
mudança não custa nada.

**A hora foi escolhida.** A conexão do Vercel com o Git é por dono: importar da conta pessoal e
transferir depois quebraria a conexão e exigiria reimportar, perdendo histórico de deploys. E
não havia nenhum segredo de Actions configurado, o que fez deste o momento mais barato que
existiria.

**O monorepo continua um.** Separar Explanando (B2C) de Lotustech (B2B) em dois repositórios
brigaria com o [ADR-0003](ADR-0003-monorepo-com-git.md) sem ganho: a separação de marca vive na
superfície da API e nos documentos, não na fronteira do repositório.

Verificado depois da transferência: `Lotus-Tecnologia/explanando`, privado, `main` como padrão,
workflow de CI ativo, e o endereço antigo redirecionando.

> Uma perda a registrar: **regras de branch (rulesets) exigem GitHub Pro em repositório
> privado**, e a organização está no plano Free. Não havia nenhuma configurada antes, então
> nada se perdeu — mas proteção de `main` não é uma opção disponível hoje.

## Consequências

- Minutos de GitHub Actions passam a ser consumidos da cota (repositórios privados não têm
  Actions gratuito ilimitado). Por isso o pipeline ignora mudanças que tocam apenas `docs/`
  e arquivos markdown.
- Se o projeto for aberto no futuro, este ADR precisa ser substituído por outro, e o job
  `visibilidade` removido no mesmo commit — deixando o registro de que foi decisão, não
  acidente.
