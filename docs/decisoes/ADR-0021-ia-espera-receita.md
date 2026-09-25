# ADR-0021 — A narrativa por IA espera a receita pagar por ela

**Data:** 2026-09-24
**Situação:** Adiada — entra quando o gatilho financeiro da seção final for cumprido
**Contexto anterior:** [ADR-0004](ADR-0004-nomenclatura-tecnica.md) ·
[ADR-0010 §3](ADR-0010-resolucoes-produto-infraestrutura.md) ·
[plano de execução](../plano-de-execucao.md)

---

## O que foi proposto

Um texto em prosa gerado por modelo de linguagem, a partir dos achados de um parlamentar. O
exemplo trazido: *"Em 2025, o deputado concentrou 78% dos gastos em apenas 3 fornecedores;
dois desses fornecedores apresentam vínculos societários indiretos entre si."*

É o recurso com o retorno mais direto da lista — é o que o "Relatório Cidadão" de R$ 2,90,
previsto no ADR-0010, venderia. E é também o de risco mais alto e o único com **custo que
cresce com o uso**.

## Por que esperar

**É o único recurso com custo recorrente por uso.** Cada relatório gerado é uma chamada paga
ao modelo. Os outros sete itens do plano custam trabalho uma vez e rodam de graça depois;
este custa a cada leitor. Sem receita, é despesa sem teto — e o ADR-0010 registrou a
diretriz de preservar caixa antes de existir faturamento.

**Hoje não há receita nenhuma.** O produto não está no ar, a cobrança de R$ 2,90 não foi
implementada, e a API B2B não tem cliente. Não há contra o que medir o custo.

**O risco não diminui com o tempo, mas a capacidade de absorvê-lo aumenta.** Um texto gerado
sobre pessoa nomeada é o lugar mais provável de o produto publicar algo falso com a própria
assinatura. Com receita, há como pagar a revisão jurídica, a moderação e o eventual custo de
um erro. Sem receita, o primeiro erro pode ser o último.

## Gatilho para implementar

**Receita recorrente líquida que cubra o custo projetado da IA com folga, por tempo bastante
para não ser acaso.**

**Receita mensal líquida de pelo menos três vezes o custo projetado da IA, por três meses
seguidos.** O custo projetado é o custo por relatório vezes o volume de relatórios esperado,
medido antes de ligar o recurso — não estimado depois.

"Considerável" foi a palavra usada na decisão; o critério acima é a tradução dela em algo que
se verifica numa planilha. Proposto por mim e **fixado pelo Gustavo em 2026-09-24**.

## As condições que viajam junto, quando entrar

Estas não são negociáveis e valem no dia em que o gatilho for cumprido:

1. **A IA formata fatos que nós calculamos; ela não descobre.** O texto é gerado a partir de
   números e ligações já verificados pelo motor e pelas consultas — nunca a partir de dado
   bruto que o modelo interpreta. O exemplo acima é ótimo **se for verdade**; se o modelo
   inventar o vínculo societário, publicamos difamação assinada.
2. **O validador `VocabularioDeCompliance`** do ADR-0010 §3 rejeita o texto que usar as
   palavras proibidas, e a geração é refeita.
3. **"Relatório Cidadão de Achados Técnicos"**, nunca "denúncia", com o checkbox de autoria
   do ADR-0010 §3.
4. **Toda afirmação do texto tem link para o achado de onde saiu.** Frase sem origem
   rastreável não é publicada.
5. **A escolha do modelo é feita no dia**, e não agora. O que se fixa aqui é o critério; o
   fornecedor e a versão mudam rápido demais para valer a pena decidir com meses de
   antecedência.
