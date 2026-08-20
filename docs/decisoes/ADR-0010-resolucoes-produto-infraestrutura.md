# ADR-0010 — Otimização de Infraestrutura, Blindagem Jurídica e Mecânica B2C

**Data:** 2026-08-20 · **Situação:** Aceita

> Renumerado de ADR-0005 para ADR-0010: o número 0005 já pertencia ao ADR de TDD
> e níveis de teste. Ver [analise-do-adr-de-resolucoes.md](analise-do-adr-de-resolucoes.md)
> para a avaliação deste documento — há um item de §1 que a medição desmente.
**Contexto:** Resolução das pontas soltas de negócio e infraestrutura identificadas após a análise de viabilidade do plano gratuito do Neon, implicações da LGPD e modelagem da monetização B2C via LLM.

## 1. Otimização Estrutural do Banco de Dados (Infraestrutura)
Para viabilizar o uso do plano gratuito do Neon (0,5 GB) para a legislatura atual sem perder granularidade de dados (mantendo o rastreio de todas as despesas):
*   **Armazenamento de Hash:** O `hash_unico` das despesas não será salvo como `char(64)` em texto. Será convertido e salvo como vetor de bytes (`bytea` no PostgreSQL), reduzindo drasticamente o tamanho da tabela e dos índices.
*   **Derivação de URL:** O campo `url_documento` será removido da tabela física. A URL será reconstruída dinamicamente no front-end em tempo de execução utilizando o `codigo_documento`.
*   **Normalização de Strings:** Campos com texto repetitivo, como `tipo_despesa`, serão normalizados utilizando numerais inteiros (`smallint` mapeados para Enums no C#).
*   *Nota:* O uso de *Tiered Storage* (envio de dados frios para o R2) fica postergado para o início da próxima legislatura (2027).

## 2. Blindagem Jurídica e LGPD (Compliance)
Para proteger a plataforma e o modelo B2B de passivos legais e sanções da ANPD:
*   **Bases Legais Distintas:** A finalidade B2C (Portal da Transparência) opera sob a **Lei de Acesso à Informação (LAI)** e exercício do controle social. A finalidade B2B (API corporativa Lotustech) opera sob o **Legítimo Interesse** (Art. 7º, IX da LGPD) focado em prevenção à fraude.
*   **Encarregado de Dados:** O rodapé da página inicial deve identificar explicitamente o Encarregado de Dados e um e-mail válido para solicitações (ex: `Encarregado de Dados: [Nome] - privacidade@explanando.com.br`).
*   **Disclaimer no Card Viral:** O aviso legal de que os achados são gerados por um motor determinístico e o link de contestação devem ser renderizados **dentro da imagem .png** gerada para compartilhamento, garantindo que o contexto viaje com a denúncia.

## 3. Contenção de Risco do LLM (Produto / Camada 2)
Para garantir que a geração de dossiês pagos via IA não fira a diretriz de neutralidade do ADR-0004:
*   **Validação C#:** Todo texto gerado pelo LLM deve passar obrigatoriamente pelo método validador `VocabularioDeCompliance` na camada de domínio. Se a IA utilizar palavras proibidas (ex: "fraude", "crime", "desvio"), o sistema rejeitará o output e exigirá regeneração.
*   **Relatório Cidadão:** O PDF exportável não será chamado de "Denúncia". O título será "Relatório Cidadão de Achados Técnicos", contendo apenas fatos, regras, valores e links para a fonte.
*   **Termo de Autoria:** O usuário pagante deverá marcar um *checkbox* assumindo a autoria e a responsabilidade pela representação antes de habilitar o download do documento.

## 4. Ajustes no Motor B2C e Gamificação (Regras de Negócio)
Para garantir a lisura do ranking público e evitar cobranças indevidas:
*   **Varredura Global Mensal:** O *Worker* rodará uma atualização completa, silenciosa e gratuita de todos os 513 parlamentares uma vez por mês, garantindo que o ranking não sofra viés de desatualização caso um deputado não receba varreduras pagas.
*   **Gatilho de Notificação Preciso:** A tabela de anomalias ganhará a coluna `VersaoMotor`. As notificações enviadas aos usuários que favoritaram um político só serão disparadas se as flags forem oriundas de **novas despesas**, evitando alertas falsos causados apenas pela atualização das regras de software.
*   **Carência de Cobrança (Cooldown):** Se um deputado recebeu uma varredura paga (Scan) nas últimas 24 horas, o botão de compra de R$ 2,90 exibirá o dossiê recém-gerado gratuitamente para os próximos usuários, evitando cobrança por dados já frescos.
