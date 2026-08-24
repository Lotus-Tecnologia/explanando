# ADR-0015 — O frontend roda no Cloudflare Workers, não na Vercel

**Data:** 2026-08-22 · **Situação:** Aceita
**Atualiza:** [ADR-0001](ADR-0001-frontend-nextjs.md) e o
[documento de Infraestrutura](../especificacao/Explanando_Infraestrutura_Stack.md) §1

## Contexto

O documento de Infraestrutura previa **Vercel ou Netlify** para o frontend, e o
[ADR-0001](ADR-0001-frontend-nextjs.md) apostou a premissa de custo numa frase: *"é a CDN, e
não o Redis, que absorve o pico viral"*. A Vercel era o caminho óbvio — é a casa do Next.js.

Duas regras da Vercel, verificadas na documentação e não supostas, fecharam esse caminho:

1. **O plano Hobby não deploya repositório de organização.** O repo passou a pertencer à
   `Lotus-Tecnologia` ([ADR-0008](ADR-0008-repositorio-privado.md), atualização de 2026-08-22),
   e deployar de organização é recurso de time Pro.
2. **O Hobby é restrito a uso não comercial**, e a definição é ampla: *"any method of
   requesting or processing payment from visitors of the site"* — pedir doação já conta. O
   Explanando vai cobrar R$ 2,90 por varredura e vender API B2B.

A segunda regra é a que decide: **o Hobby estaria fora deste produto mesmo se o repositório
tivesse ficado na conta pessoal.** O Pro custa US$ 20 por assento/mês — despesa recorrente
antes de existir receita, contra a diretriz de preservar caixa que o
[ADR-0010](ADR-0010-resolucoes-produto-infraestrutura.md) registrou.

## Decisão

**O frontend roda no Cloudflare Workers**, com o adaptador
[`@opennextjs/cloudflare`](https://opennext.js.org/cloudflare).

Isso na verdade **aproxima** do que o ADR-0001 escreveu, em vez de se afastar: a Cloudflare
deixa de ser só a CDN na frente de outra plataforma e passa a ser onde o frontend vive. O
plano gratuito permite uso comercial e não se importa com quem é dono do repositório.

### Foi medido, não adotado por otimismo

Rodando no `workerd` — o runtime real do Cloudflare, não uma emulação — contra a base de 513
parlamentares:

| | |
|---|---|
| Home | **513** linhas estáticas |
| `/ranking/indice` | **498** — a exclusão do [ADR-0009](ADR-0009-dois-numeros-nao-um.md) sobrevive |
| Perfil | SSG, com série mensal e data da varredura |
| `/despesas` com filtro | 68 de 3.271, correto |
| `/despesas/{id}` | dinâmica, título por despesa |
| Sitemap | **517** endereços |
| 404 | 404 |
| **`x-nextjs-cache`** | **HIT** |

### Cache incremental em R2, não em KV

Sem cache incremental, `revalidate` não tem onde guardar a página regenerada — num Worker não
existe disco entre invocações — e cada acesso depois da janela de 5 minutos volta a bater na
API. Seria a premissa de custo do ADR-0001 se perdendo pela porta dos fundos.

**R2 e não KV** porque o KV é eventualmente consistente, com janela de até 60 segundos entre
regiões. Uma página de gasto público que aparece diferente conforme o nó que atendeu é o tipo
de inconsistência que ninguém sabe explicar depois — e este é um produto cuja credibilidade é o
ativo. O R2 é fortemente consistente e tem egresso zero, que é o motivo de ele já estar no
plano de infraestrutura.

Balde: `explanando-cache-incremental`.

### Imagem sem otimização, por escolha

Otimizar imagem remota no Cloudflare exige o produto **Cloudflare Images**, que é pago. Não
vale aqui: são retratos oficiais de 72×96 no perfil, JPEG pequeno que a própria Câmara já
serve. Pagar por mês para economizar bytes que não pesam é troca ruim.

De quebra some a rota de otimização, que era um proxy de saída hospedado no nosso domínio —
uma superfície a menos.

## Consequências

**O build do Cloudflare não roda no Windows.** O adaptador avisa que *"could encounter
unpredictable failures during runtime"*, e foi o que aconteceu: a primeira execução completou
inteira, as seguintes penduraram em toda rota, inclusive `robots.txt`. O build precisa de Linux
— CI ou o build do próprio Cloudflare. Por isso o CI ganhou um job de frontend, que é também o
primeiro a rodar os 106 testes, o lint e o `tsc` fora da máquina de quem os escreveu.

**O `next build` normal continua funcionando** (522 páginas). A porta para a Vercel fica
aberta se a decisão mudar — por exemplo, quando houver receita que pague o Pro.

**A Vercel sai da lista de contas necessárias.** Uma relação de fornecedor a menos.

**O cache do build precisa ser populado no R2 no deploy.** As 513 páginas não saem como HTML
estático: saem como 1.557 arquivos de cache incremental. Sem populá-los, toda página é *miss* na
primeira visita — funciona, mas desperdiça a pré-renderização inteira.

## Alternativas descartadas

**Vercel Pro, US$ 20/mês.** Menor atrito e melhor suporte a Next.js. Descartada por ora: é
despesa recorrente antes de receita. Continua sendo a escolha certa se o atrito do Cloudflare
se mostrar caro em tempo de desenvolvimento.

**Voltar o repositório para a conta pessoal e usar o Hobby.** Trocaria a titularidade da
propriedade intelectual — que é o que a due diligence de um banco pergunta — por uma economia
que expiraria no dia em que o pagamento entrasse no ar, por causa da cláusula comercial.

**Deploy pela CLI da Vercel a partir do GitHub Actions**, que contorna a restrição de
organização. Descartada: não resolve a cláusula comercial, e a mesma página de *fair use* diz
que *"circumventing or otherwise misusing Vercel's limits is a violation"*.

**Cloud Run também para o frontend.** Consolidaria numa plataforma só. Descartada por ora: ISR
exigiria cache compartilhado montado à mão, e a partida a frio cairia justamente na página que
o pico atinge.
