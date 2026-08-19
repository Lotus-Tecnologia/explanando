# Explanando - Documento de Visão e Escopo (V1)

## 1. Visão Geral
O **Explanando** é uma plataforma de utilidade pública focada em auditar, simplificar e expor os gastos de deputados e senadores utilizando dados de transparência pública. Através de um motor de cruzamento de dados (com lógicas de rastreamento de anomalias financeiras) e Inteligência Artificial, o sistema identifica suspeitas de desvios, conflitos de interesse e irregularidades no uso de verbas indenizatórias.

## 2. Proposta de Valor e Modelo de Funcionamento
* **Ranking de Gastos:** Exibição pública e gratuita do ranking dos parlamentares que mais consumiram recursos no período (Top "Mamadores").
* **Varredura sob Demanda (Micro-financiamento):** Qualquer cidadão pode solicitar uma "varredura profunda" no perfil de um parlamentar, financiando o custo computacional e de IA através de um micropagamento (ex: PIX de baixo valor).
* **Transparência Compartilhada:** Uma vez processada, a varredura atualiza a página pública do político, beneficiando toda a base de usuários. A data da "Última Varredura" fica em destaque.
* **Sistema de Alertas:** Usuários podem favoritar políticos para receber notificações automatizadas (email/push) assim que novas anomalias forem detectadas ou novas varreduras forem concluídas pela rede.

## 3. Motor de Processamento e Cruzamento de Dados
O coração do sistema atua em duas camadas de validação para otimização de custos e alta precisão:

### Camada 1: Motor de Regras Determinísticas (Triage Rápida)
Execução de regras de negócio em C# para detecção de "red flags" imediatas sem custo de token:
* **Regra da Primeira Nota:** Identificação de notas fiscais iniciais (ex: série 000001) emitidas diretamente para o parlamentar.
* **Incompatibilidade de Atividade:** Empresa registrada com um CNAE divergente da natureza do gasto (ex: consultoria de TI vendendo papelaria).
* **Doação Cruzada e Favorecimento:** Cruzamento do Quadro de Sócios (QSA) da empresa prestadora com a base de doadores de campanha do político.
* **Anomalias de Volume/Preço:** Gastos desproporcionais, limites estourados ou repetição idêntica de valores mensais.

### Camada 2: Análise Semântica (Motor LLM)
Processamento profundo acionado exclusivamente durante a "Varredura sob Demanda":
* Análise de fracionamento de despesas.
* Avaliação de pertinência do gasto versus a atuação do parlamentar (comissões, estado de origem).
* Geração do dossiê final simplificado e traduzido para a população geral.

## 4. Arquitetura Técnica (Macro)
A infraestrutura será baseada em uma arquitetura distribuída para suportar concorrência, processamento assíncrono e componentização fluida:
* **Interface (Frontend):** Blazor (WebAssembly ou Server) garantindo reatividade na exibição dos cards e gráficos.
* **Backend & Microsserviços:** .NET / C#.
* **Comunicação Interna:** gRPC para tráfego rápido e eficiente de dados entre o extrator e o motor de regras.
* **Mensageria (Workers):** Mensageria (ex: RabbitMQ, Service Bus) para enfileirar e orquestrar as requisições de varredura sem travar a interface de quem solicitou.
* **Observabilidade:** OpenTelemetry implementado para monitorar todo o ciclo de vida da varredura, desde o clique na tela até o retorno do LLM.

## 5. Fontes de Dados (Integrações Primitivas)
* **APIs Governamentais (Câmara/Senado):** Despesas (CEAP), emissão de notas fiscais, frequência em plenário.
* **APIs de CNPJ (Receita Federal / BrasilAPI):** Extração de natureza jurídica, CNAEs, situação cadastral e Quadro de Sócios e Administradores (QSA).
* **Tribunal Superior Eleitoral (TSE):** Base de dados aberta de financiamento eleitoral e doadores de campanha.
