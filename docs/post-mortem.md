# Post-mortem do Explanando

**Encerrado em 07/11/2026.** De agosto a novembro de 2026, 17 etapas e 21 decisões de
arquitetura registradas.

## A tese

A cota parlamentar é dinheiro público com prestação de contas pública, e quase ninguém lê.
Os dados estão lá (cada nota, cada fornecedor, cada CNPJ), mas espalhados por APIs de
Câmara, Senado e Receita que não conversam entre si. Eu trabalhei em banco e conheço o que
compliance faz com transação: KYB do fornecedor, padrão de valor, concentração, rede. A ideia
era aplicar essa mesma régua ao gasto do parlamentar, de forma determinística e auditável, e
publicar o resultado sem acusar ninguém.

Havia duas frentes. A do eleitor (B2C) era um site aberto, com ranking e perfil. A das
empresas (B2B) era a mesma inteligência vendida como due diligence de fornecedor e de pessoa
politicamente exposta.

## O que foi construído

- Ingestão das duas casas, da BrasilAPI (CNPJ e QSA), do TSE (doações), do SIORG (estrutura
  do Executivo) e do Portal da Transparência (quadro de pessoal). Cada fonte tem contrato
  gravado em fixture e teste contra ele.
- Um motor de 12 regras, versionado. Cada mudança de regra sobe a versão e re-varre a base
  inteira, com o antes e o depois registrados.
- Uma API de leitura com cache e limite de taxa, e um site em Next.js com ranking por casa,
  perfil com a composição do índice, comparador, sócios em comum e o mapa do Estado.
- Mais de 1.400 testes, arquitetura verificada por teste e CI.
- Empacotamento para uma VPS só, com backup no R2. O site chegou a ficar pronto para subir.

## O que funcionou

- **Regra determinística em vez de modelo.** Cada achado tem uma frase, uma regra e um dado
  de origem. Nada é "o modelo achou". Na revisão final deu para conferir cada número da tela
  contra uma consulta SQL independente, com zero divergência.
- **Dizer "não sei" em vez de zero.** Cada regra declara de que dado depende, e a interface
  mostra "sem dado público" quando falta. Foi a decisão que mais protegeu o projeto contra
  acusar injustamente.
- **Linguagem jurídica antes da estética.** Nada de "suspeito" ou "irregular" na tela, e o
  vermelho só para gravidade alta. Isso foi posição jurídica, decidida cedo
  ([ADR-0004](decisoes/ADR-0004-nomenclatura-tecnica.md),
  [ADR-0013](decisoes/ADR-0013-identidade-visual-e-taxonomia-de-risco.md)).
- **TDD e registro de cada etapa.** Retomar o projeto depois de semanas parado nunca custou
  mais do que ler o último registro.

## O que não funcionou

- **O B2C não encontrou o eleitor.** A eleição de 2026 mostrou um eleitor polarizado, que
  escolhe pela identidade e não pela prestação de contas. Gasto de cota não muda voto. O site
  respondia uma pergunta que pouca gente estava fazendo.
- **O B2B ficou genérico demais.** Due diligence de fornecedor e de PEP é um mercado com
  players consolidados (Neoway, BigDataCorp e outros), com mais fontes, mais dados e
  relacionamento comercial. Um produto que cobre só a cota parlamentar não se sustenta contra
  eles, e ampliar o escopo era virar outra empresa.
- **Os dados públicos são instáveis.** As fontes mudam de formato sem aviso. Na revisão final,
  a Câmara tinha passado a publicar o CNPJ das companhias aéreas nas passagens, e uma nova
  coleta duplicaria 184 mil despesas. Manter a base certa exigiria vigilância constante.
- **Escopo cresceu antes da validação.** Grafo do Estado, quadro de pessoal e mapa de poder
  são bons de construir e de mostrar, mas vieram antes de alguém pagar ou voltar ao site.

## O que eu faria diferente

1. Validar a demanda antes do motor: dez conversas com compliance de verdade e uma página
   simples com um ranking, medindo retorno, antes de 12 regras e cinco fontes.
2. Escolher uma frente só. Atender eleitor e empresa ao mesmo tempo dividiu a interface, a
   linguagem e o tempo.
3. Começar por um nicho onde a régua de compliance é obrigação e não curiosidade, como
   fornecedores de prefeituras ou contratos públicos de um setor, em vez de mirar o público
   geral.
4. Tratar a instabilidade das fontes como parte do produto desde o começo, com alerta de
   mudança de contrato, e não como incidente.

## Como fica

O site não está no ar e o domínio expira em 2027. O código, os testes, as 21 decisões de
arquitetura e o registro das 17 etapas ficam abertos, sob licença MIT. Os dados são de agosto
de 2026 e não são atualizados.
