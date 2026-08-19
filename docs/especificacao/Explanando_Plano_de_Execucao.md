# Plano de Execução Master: Explanando & API Lotustech B2B

## Fase 1: Setup Base e Infraestrutura
Configuração inicial do ecossistema, garantindo a fundação técnica para a arquitetura C#/.NET no back-end e Next.js no front-end.

- [ ] Inicializar repositório monorepo (ou repositórios separados para Front/Back/Workers).
- [ ] Configurar a solution `.NET` com arquitetura em camadas (Domain, Application, Infrastructure, API).
- [ ] Configurar o projeto Next.js (TypeScript, Tailwind, estruturação de rotas).
- [ ] Provisionar banco de dados PostgreSQL (ex: Neon.tech) e configurar as strings de conexão seguras.
- [ ] Configurar provedor de cache e filas (ex: Redis/Upstash e AWS SQS/RabbitMQ).
- [ ] Estabelecer CI/CD básico (GitHub Actions) para automação de testes e deploys.

## Fase 2: Modelagem de Dados e ORM
Tradução do diagrama Entidade-Relacionamento (ER) para o Entity Framework Core.

- [ ] Criar entidades de domínio (Parlamentares, Despesas, Mandatos, Fornecedores).
- [ ] Criar entidades de auditoria e risco (FlagsAnomalias, ScoresRisco, HistoricoMonitoramento).
- [ ] Criar entidades de monetização e usuários (Usuarios, SolicitacoesVarreduras, Favoritos).
- [ ] Criar entidade de Direito de Resposta (`ContestacaoDespesa`).
- [ ] Configurar mapeamentos fluentes (`IEntityTypeConfiguration`) garantindo PKs, FKs, UKs e índices (principalmente no Hash de idempotência das despesas).
- [ ] Gerar e aplicar a primeira *Migration* no PostgreSQL.

## Fase 3: Ingestão de Dados e Workers (Back-end)
Construção do motor que busca, processa e armazena os dados públicos.

- [ ] Desenvolver clientes HTTP para consumo das APIs da Câmara, Senado, TSE e Portal da Transparência.
- [ ] Implementar fila de processamento assíncrono para lidar com o volume de despesas sem travar a API principal.
- [ ] Implementar camada de "Crawlers de Contingência" (parser de CSV via FTP) para garantir operação caso as APIs do governo falhem.
- [ ] Criar o Motor Determinístico de Regras (AML) para cruzar dados e gerar as `FlagsAnomalias`.
- [ ] Configurar rotina de rotatividade (Cron Job/Worker) para gerar "Snapshots" periódicos e alimentar o histórico de Score de Risco.

## Fase 4: Proteção Jurídica e Compliance (Back-end & Front-end)
Implementação das blindagens legais e fluxos de contestação.

- [ ] Garantir que o motor e a API nunca retornem termos como "Crime" ou "Lavagem", substituindo rigorosamente por "Inconsistência Financeira" ou "Alerta de Compliance".
- [ ] Implementar rotina de ofuscação de LGPD no back-end, garantindo que CPFs de sócios (QSA) sejam enviados ao front-end mascarados (`***.123.456-**`).
- [ ] Criar API restrita de autenticação validando exclusivamente e-mails `@camara.leg.br` ou `@senado.leg.br`.
- [ ] Desenvolver no front-end o painel da "Área do Parlamentar" para envio de contestações textuais e anexos.
- [ ] Atualizar a UI pública da despesa para exibir a "Resposta Oficial do Gabinete" lado a lado com a anomalia detectada.

## Fase 5: Experiência B2C e Engrenagens Virais (Front-end)
Construção da interface pública voltada para o cidadão.

- [ ] Desenvolver página inicial com busca de parlamentares e ranking de riscos.
- [ ] Desenvolver página de perfil do parlamentar com renderização de gráficos de linha (tendência temporal) consumindo o histórico de logs.
- [ ] Desenvolver listagem de despesas, destacando visualmente as anomalias e as contestações oficiais.
- [ ] Implementar botão "Compartilhar Denúncia" nas despesas com flag vermelha.
- [ ] Integrar biblioteca client-side (`html2canvas`) para gerar o "Card de Risco" (imagem PNG com foto, valor, regra quebrada e QR Code dinâmico) direto no navegador do usuário.
- [ ] Integrar Gateway de Pagamento (ex: Stripe/Mercado Pago) para o fluxo de "Varredura sob Demanda" (Pay-per-Scan).

## Fase 6: Monetização B2B e SaaS Pro (Lotustech)
Construção das engrenagens corporativas para rentabilizar os dados sob a marca guarda-chuva Lotustech.

- [ ] Isolar a documentação e os endpoints da API corporativa sob a marca **Lotustech** para blindar contratos B2B de riscos de imagem do portal B2C.
- [ ] Desenvolver endpoints seguros (REST/GraphQL) focados em *Due Diligence* de Pessoas Politicamente Expostas (PEPs) para departamentos de compliance e bancos.
- [ ] Implementar autenticação via API Keys com controle de rate limit e bilhetagem (cobrança por chamada ou tier).
- [ ] Desenvolver o Painel SaaS Pro (área logada premium) para jornalistas e pesquisadores.
- [ ] Implementar recursos Pro: Exportação massiva de dados (CSV) e criação de Alertas Avançados customizados.

## Fase 7: Lançamento e Tração Inicial (Cold Start)
Estratégia de Go-to-Market para rodar no dia 1.

- [ ] Executar varreduras completas no ambiente de produção para os parlamentares mais midiáticos.
- [ ] Gerar os "Dossiês Iniciais" consolidados a partir do banco de dados populado.
- [ ] Configurar rotina para envio ou disponibilização em acesso antecipado destes relatórios para perfis influentes e jornalistas parceiros para gerar o tráfego inicial orgânico.
- [ ] Monitorar logs de infraestrutura e performance da AWS/GCP durante o pico de acessos do lançamento.
