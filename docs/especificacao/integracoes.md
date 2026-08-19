# Integrações Técnicas e Endpoints - Explanando

Este documento detalha as APIs, endpoints específicos e os métodos de autenticação necessários para construir o fluxo de dados e monetização do projeto Explanando.

---

## 1. Fontes Governamentais e Transparência Pública

### 1.1. Câmara dos Deputados (Dados Abertos)
Fornece os dados cadastrais dos deputados e as notas fiscais referentes à Cota para o Exercício da Atividade Parlamentar (CEAP).

*   **Endpoint de Listagem:** `GET https://dadosabertos.camara.leg.br/api/v2/deputados`
    *   *Uso:* Obter o ID, nome e foto atualizada de todos os deputados em exercício.
*   **Endpoint de Despesas:** `GET https://dadosabertos.camara.leg.br/api/v2/deputados/{id}/despesas`
    *   *Uso:* Retorna a lista de gastos de um deputado específico, contendo CNPJ do fornecedor, valor, data, tipo de despesa e link para a nota fiscal.
*   **Autenticação:** Não requer autenticação (Pública).
*   **Limites:** Sujeito a rate-limit por IP.

### 1.2. Senado Federal (Dados Abertos)
Fornece os dados de senadores e suas respectivas cotas (CEAPS). O Senado utiliza respostas majoritariamente em XML/JSON.

*   **Endpoint de Listagem:** `GET https://legis.senado.leg.br/dadosabertos/senador/lista/atual`
    *   *Uso:* Obter código, nome e UF dos senadores atuais.
*   **Endpoint de Despesas:** `GET https://legis.senado.leg.br/dadosabertos/senador/{id}/ceaps`
    *   *Uso:* Extrato de gastos reembolsados pela cota parlamentar.
*   **Autenticação:** Não requer autenticação (Pública).

### 1.3. Tribunal Superior Eleitoral (TSE) - Doadores e Campanhas
O TSE possui a API do sistema *DivulgaCandContas*, mas para cruzamento de dados em massa (verificar se o sócio da empresa doou para o político), a melhor prática é baixar a base bruta de prestação de contas eleitorais e rodar no seu próprio banco.

*   **Método Recomendado (Bulk):** Download dos arquivos CSV de "Receitas" e "Despesas" diretamente no **Portal de Dados Abertos do TSE** e importação para seu banco de dados na Camada 1.
*   **Endpoint API Rest (DivulgaCandContas):** `GET https://divulgacandcontas.tse.jus.br/divulga/rest/v1/prestador/consulta/{eleicao}/{idCandidato}/doadores` *(Atenção: Uso instável para grandes volumes).*
*   **Autenticação:** Nenhuma.

---

## 2. Dados de Pessoas Jurídicas (CNPJ e QSA)
Para cruzar o CNPJ que emitiu a nota fiscal com o seu Quadro de Sócios (QSA) e atividade econômica (CNAE).

### 2.1. BrasilAPI (Comunidade / Gratuita)
*   **Endpoint de CNPJ:** `GET https://brasilapi.com.br/api/cnpj/v1/{cnpj}`
    *   *Uso:* Retorna razão social, nome fantasia, CNAE principal/secundários, situação cadastral e, crucialmente, o Quadro de Sócios e Administradores (QSA) com nome e CPF mascarado dos sócios.
*   **Autenticação:** Não requer autenticação.
*   **Limites:** Limitação de requisições por IP (rate-limit).

---

## 3. Inteligência Artificial (LLM)
Usada na Camada 2 (Varredura profunda sob demanda) para ler os contextos gerados, avaliar pertinência de despesas e emitir pareceres semânticos. **Recomendação: Google Gemini Flash** pelo alto limite de tokens e baixo custo.

### 3.1. Google Gemini API
*   **Endpoint de Geração:** `POST https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent`
*   **Autenticação:** Necessário enviar a API Key no cabeçalho ou na query string (`?key=YOUR_API_KEY`).
*   **Como conseguir o token:** Acessar o portal do [Google AI Studio](https://aistudio.google.com/), criar um projeto e gerar uma chave de API na aba "Get API Key". O billing deve estar ativado para uso em produção.

---

## 4. Pagamentos (Checkout de Varreduras)
Usado para gerar o PIX da "varredura profunda" e receber o *webhook* de confirmação quando o usuário pagar.

### 4.1. Mercado Pago (Checkout Transparente via PIX)
*   **Endpoint de Criação de Pagamento:** `POST https://api.mercadopago.com/v1/payments`
    *   *Uso:* Enviar o valor (ex: R$ 2,90), `payment_method_id: "pix"` e o e-mail do pagador. A API retorna a string "Pix Copia e Cola" e o QR Code.
*   **Autenticação:** Token *Bearer* no cabeçalho (`Authorization: Bearer YOUR_ACCESS_TOKEN`).
*   **Como conseguir o token:** Criar uma conta no Mercado Pago, ir ao "Painel do Desenvolvedor", criar uma aplicação e gerar as credenciais de Produção (Production Access Token).

---

## 5. Serviço de Mensageria e Alertas (E-mail)
Envio de e-mail automatizado para os usuários que favoritaram os deputados.

### 5.1. Resend
*   **Endpoint de Envio:** `POST https://api.resend.com/emails`
    *   *Payload:* `{ "from": "alertas@explanando.com.br", "to": ["usuario@email.com"], "subject": "Nova Varredura!", "html": "..." }`
*   **Autenticação:** Token *Bearer* no cabeçalho (`Authorization: Bearer YOUR_API_KEY`).
*   **Como conseguir o token:** Criar conta no painel do Resend, registrar/verificar o seu domínio web e criar uma API Key.

---

## Documentação Oficial (Referências)
*   **Câmara dos Deputados (Swagger):** https://dadosabertos.camara.leg.br/swagger/api.html
*   **Senado Federal (Docs):** https://legis.senado.leg.br/dadosabertos/docs/
*   **TSE Dados Abertos:** https://dadosabertos.tse.jus.br/
*   **BrasilAPI (Docs):** https://brasilapi.com.br/docs
*   **Google Gemini API:** https://ai.google.dev/docs
*   **Mercado Pago (PIX API):** https://www.mercadopago.com.br/developers/pt/reference/payments/_payments/post
*   **Resend (Docs):** https://resend.com/docs/api-reference/emails/send-email
