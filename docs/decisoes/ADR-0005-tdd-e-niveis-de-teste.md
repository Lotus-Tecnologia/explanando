# ADR-0005 — TDD obrigatório, quatro níveis de teste

**Data:** 2026-08-18 · **Situação:** Aceita

## Contexto

O núcleo do produto é um motor de regras que emite juízo público sobre pessoas reais. Um falso
positivo aqui não é um bug de tela: é uma acusação indevida a um parlamentar identificado pelo
nome, numa página indexada. A correção do motor é requisito jurídico, não só técnico.

Além disso, boa parte da lógica depende de dado externo que não controlamos — a Câmara pode
mudar o contrato da API, a BrasilAPI pode devolver campo vazio, o CNPJ pode vir sem QSA.

## Decisão

**TDD é obrigatório: o teste que falha vem antes da implementação.** Nenhuma etapa é dada por
concluída sem cobertura nos níveis que lhe cabem.

| Nível | Prova que | Ferramenta |
|---|---|---|
| **Unitário** | a regra de negócio está correta, isolada de banco e rede | xUnit + Shouldly |
| **Integração** | EF Core, clientes HTTP e fila funcionam contra dependências reais | Testcontainers |
| **E2E** | o fluxo inteiro entrega o resultado, da ingestão ao endpoint | WebApplicationFactory |
| **Arquitetura** | as camadas continuam separadas | leitura dos `.csproj` |

Cada nível prova uma coisa diferente e nenhum substitui outro. Um motor de regras com 100% de
cobertura unitária ainda pode não gravar nada no banco por um mapeamento errado — é o teste de
integração que pega isso.

**Nenhum teste entra verde sem antes ter sido visto vermelho.** Um teste que nunca falhou não
provou que testa alguma coisa.

## Alternativas descartadas

**Testes depois da implementação** — mais rápido no curto prazo, mas o teste escrito depois
tende a confirmar o que o código faz, em vez de exigir o que ele deveria fazer. Num motor de
regras, essa diferença é a diferença entre validar a regra e validar o bug.

**Só unitários** — baratos e rápidos, mas cegos justamente onde este projeto é frágil: nas
bordas com dado externo.

## Consequências

- As respostas reais das APIs externas são congeladas em `docs/contratos/` e viram fixture de
  teste. Quando a Câmara mudar o contrato, o teste avisa.
- `TreatWarningsAsErrors` está ligado: build com aviso é build vermelho.
- Testes têm nome em `snake_case` porque o nome do método é a frase que descreve a regra. O
  analisador CA1707 fica desligado só na pasta `tests/`, via `.editorconfig`.
