# Motor de Regras Determinísticas - Explanando (V1 Final)

Este documento centraliza todas as regras hardcoded (Camada 1) do motor de processamento. Estas regras operam de forma determinística, atuando como um sistema de prevenção à fraude (semelhante a motores de Anti-Money Laundering - AML). Cada nota ou lote de despesas passará por esses validadores antes de qualquer acionamento de IA.

## 1. Topologia de Fornecedores (KYB - Know Your Business)
Focado em identificar empresas de fachada, laranjas ou em situação irregular.

* **REG_KYB_001 (CNAE Divergente):** O CNAE principal e os secundários da empresa não são compatíveis com o tipo de despesa declarada (ex: Cota de "Consultoria Técnica" para uma empresa cujo CNAE é "Comércio Varejista de Bebidas").
* **REG_KYB_002 (Baixa/Irregularidade Cadastral):** A data de emissão da nota fiscal é posterior à data de encerramento, suspensão ou inaptidão do CNPJ na Receita Federal.
* **REG_KYB_003 (Empresa Recém-Nascida):** A empresa foi aberta (Data de Abertura no CNPJ) a menos de 90 dias antes de começar a faturar altos valores para o parlamentar.
* **REG_KYB_004 (Excesso de Faturamento MEI):** Fornecedor é MEI (Microempreendedor Individual), mas o volume financeiro pago pelo deputado no ano excede o teto legal de faturamento do MEI (R$ 81.000/ano).
* **REG_KYB_005 (Nota Fiscal Inicial ou Monocliente):** As notas fiscais emitidas para o deputado possuem numeração muito baixa e sequencial (ex: 000001, 000002, 000003), indicando que o parlamentar pode ser o único cliente da empresa.

## 2. Topologia de Rede e Conflito de Interesses
Focado em rastrear o fluxo do dinheiro de volta para o político ou seus aliados.

* **REG_NET_001 (Doação Cruzada/Toma-lá-dá-cá):** Pelo menos um CPF listado no Quadro de Sócios e Administradores (QSA) da empresa fornecedora realizou doação para a campanha eleitoral do deputado.
* **REG_NET_002 (Vínculo Empregatício/Gabinete):** CPF no QSA da empresa bate com a lista de secretários parlamentares (funcionários de gabinete) ativos ou exonerados recentemente do próprio deputado.
* **REG_NET_003 (Mesmo Endereço Físico):** O endereço registrado no CNPJ do fornecedor é idêntico ao endereço do escritório de apoio político do deputado no seu estado de origem.
* **REG_NET_004 (Concentração Partidária):** Uma mesma empresa recém-criada concentra o fornecimento de serviços (ex: aluguel de carros, marketing) exclusivamente para vários deputados de um mesmo partido.

## 3. Topologia de Transações e Limites (Velocity & Thresholds)
Focado em identificar padrões de "smurfing" (fracionamento) e esgotamento de cotas.

* **REG_TX_001 (Fracionamento de Despesa / Smurfing):** Múltiplas notas fiscais de valores menores emitidas no mesmo mês para a mesma empresa, somando um valor que, se fosse único, exigiria outro tipo de contratação ou chamaria atenção.
* **REG_TX_002 (Gasto "Cravado" no Limite):** O valor da despesa bate exatamente (ou mais de 99%) com o teto máximo permitido para aquele tipo de cota no mês.
* **REG_TX_003 (Padrão Mensal Idêntico):** Emissão de notas fiscais com valores exatos (até os centavos) para a mesma empresa de consultoria ou locação de veículos por meses consecutivos, sugerindo um contrato de repasse fixo sem variação de serviço.
* **REG_TX_004 (Spike de Consumo / Picos Súbitos):** O gasto médio histórico do deputado com um tipo de serviço (ex: Combustível) era de R$ 2.000/mês, e subitamente salta para R$ 12.000 em um mês específico.

## 4. Topologia Geográfica e de Calendário
Focado em atestar a viabilidade física do gasto.

* **REG_GEO_001 (Teletransporte):** Duas despesas presenciais (ex: abastecimento de veículo ou refeição) ocorrem em locais distantes (ex: São Paulo e Brasília) em um intervalo de tempo impossível de ser percorrido fisicamente.
* **REG_GEO_002 (Gasto Fora da Base Eleitoral):** Volume anormal de despesas (como locação de imóveis, de veículos ou segurança) em um estado que não é nem Brasília (DF) nem o estado pelo qual o deputado foi eleito.
* **REG_GEO_003 (Despesas no Recesso/Feriado):** Gasto expressivo com alimentação ou hospedagem cara em dias de recesso parlamentar, fins de semana prolongados ou feriados sem justificativa de agenda oficial.

## Como o Sistema Processa (O Pipeline)
1. Extração dos dados brutos das APIs Governamentais.
2. O lote de gastos passa pelo `ValidatorPipeline` no C#.
3. Se qualquer `REG_*` for violada, o gasto ganha uma "Flag Vermelha" com peso específico.
4. O *Score de Risco* do deputado sobe.
5. Tudo fica salvo no banco, pronto para exibição pública ou envio para a IA em caso de varredura detalhada.
