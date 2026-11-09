# ADR-0022 — Encerramento: repositório aberto, sem hospedagem

**Data:** 2026-11-08 · **Situação:** Aceita · **Supera:** [ADR-0008](ADR-0008-repositorio-privado.md)

## Contexto

Em 07/11/2026 as frentes B2C e B2B foram encerradas (ver o [post-mortem](../post-mortem.md)).
Não haverá hospedagem: nem a VPS do [ADR-0019](ADR-0019-tudo-numa-vps.md) nem outra. O
domínio expira sozinho em 2027.

O ADR-0008 mantinha o repositório privado por dois motivos: a lógica de detecção tinha valor
comercial, e as fixtures carregavam dado real. Com o fim do produto, o primeiro motivo
deixou de existir. O segundo foi resolvido na Etapa 3 da última versão: CPFs reais viraram
sintéticos, sócios ganharam nomes fictícios, e um teste de arquitetura impede a volta de CPF
real versionado.

## Decisão

1. **O repositório passa a ser público**, como portfólio da Lotus Tecnologia, sob licença MIT.
   A abertura só acontece depois de o histórico do git ser reescrito para tirar os dados
   pessoais antigos.
2. **O job `visibilidade` sai do CI.** Ele falharia o pipeline justamente quando a decisão
   acima for executada.
3. **Os documentos de estratégia comercial saem do repositório público** (canvas de negócio,
   análise de lacunas do B2B, questões de negócio, as respostas e a análise delas). Uma cópia fica guardada
   fora do git. O post-mortem conta essa história em público.
4. **Nada mais é coletado.** A base congela em agosto de 2026, e o README diz isso.

## Consequências

- Os ADRs 0008, 0009, 0014 e 0019 e o registro da Etapa 00 citam documentos de negócio que não
  estão mais no repositório. Os links ficam quebrados de propósito, como registro do que
  existiu.
- Quem quiser rodar o projeto precisa fazer a própria coleta, que leva horas. Não há dump da
  base no repositório: ele carregaria nomes e CPFs mascarados de fornecedores pessoa física.
