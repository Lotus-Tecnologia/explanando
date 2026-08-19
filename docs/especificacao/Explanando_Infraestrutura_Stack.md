# Explanando - Stack de Infraestrutura e Fornecedores

Este documento oficializa a seleção de tecnologias e provedores para a infraestrutura do projeto, baseada no princípio de **"Custo Fixo Zero, Escala Infinita"**.

## 1. Frontend: React (Next.js)
* **Papel:** Interface pública do usuário, exibição do ranking e perfis dos deputados.
* **Por que:** Otimização máxima para SEO e tempo de carregamento no mobile através de Server-Side Rendering (SSR) e Static Site Generation (SSG).
* **Hospedagem Recomendada:** Vercel ou Netlify (ambos oferecem *free tiers* robustos e distribuição global via CDN nativa).

## 2. Mensageria e Filas: AWS SQS (Simple Queue Service)
* **Papel:** Receber os eventos de "Nova Varredura" e enfileirar as requisições para os *workers* no backend, protegendo o banco de dados de picos de acessos.
* **Por que:** É um serviço *Serverless* puro. Extremamente resiliente e possui uma integração nativa e simples com ecossistemas .NET.
* **Custo/Free Tier:** 1 milhão de requisições gratuitas por mês (eterno). Custo zero com servidores ociosos.

## 3. Backend (Compute): Google Cloud Run
* **Papel:** Hospedar as APIs e os *workers* (construídos em .NET/C#) que executam o motor de regras e a integração com o LLM.
* **Por que:** Permite empacotar a aplicação em contêineres Docker. Possui a funcionalidade de "Scale to Zero", ou seja, se não houver tráfego, o contêiner é desligado e nada é cobrado. Se o projeto viralizar, ele clona o contêiner automaticamente.
* **Custo/Free Tier:** 2 milhões de requisições gratuitas por mês.

## 4. Banco de Dados: Neon.tech (PostgreSQL Serverless)
* **Papel:** Armazenamento relacional para as regras, dados dos deputados, usuários (favoritos) e histórico de varreduras.
* **Por que:** Separa a camada de computação da camada de armazenamento. Ele "dorme" quando não está em uso, mantendo a performance de um Postgres tradicional quando acionado.
* **Custo/Free Tier:** Plano gratuito generoso que suporta perfeitamente o início do projeto, sem o custo fixo pesado de um RDS tradicional.

## 5. Cache: Upstash (Redis Serverless)
* **Papel:** Armazenar os dados mais acessados, como o "Ranking de Mamadores" e consultas frequentes aos perfis, evitando consultas repetitivas ao banco de dados (Neon).
* **Por que:** Redis gerenciado que cobra apenas pelo consumo (por requisição), eliminando a necessidade de manter uma instância de cache ligada 24/7.
* **Custo/Free Tier:** Plano gratuito até 10.000 requisições por dia.

## 6. CDN e Proteção Edge: Cloudflare
* **Papel:** Proxy reverso, cache global estático, bloqueio de ataques DDoS (WAF) e roteamento de tráfego.
* **Por que:** O escudo primário do sistema. Segura grande parte do tráfego viral servindo páginas em cache antes mesmo da requisição bater no Cloud Run.
* **Custo/Free Tier:** Plano gratuito cobre todas as necessidades de segurança e cache inicial.

## 7. Storage de Imagens: Cloudflare R2
* **Avaliando a Escolha:** Sim, esta é, de longe, **a melhor opção**. 
* **Papel:** Armazenar e servir as fotos dos parlamentares, gráficos estáticos e eventuais relatórios ou dossiês em PDF.
* **Por que o R2 é superior ao AWS S3 neste cenário:** O grande custo oculto do AWS S3 é a taxa de transferência de saída (egress fee). Se uma foto de deputado viralizar, a AWS cobra por cada megabyte baixado. O **Cloudflare R2 possui Egress Zero** (taxa de saída gratuita). Como ele tem compatibilidade nativa com a API do S3, você usa as mesmas bibliotecas no backend, mas elimina o risco de falência por tráfego.
* **Custo/Free Tier:** 10 GB de armazenamento gratuito, 1 milhão de operações de gravação e 10 milhões de operações de leitura gratuitas por mês.
