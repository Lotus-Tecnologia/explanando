# Incidente 2026-08-21 — `/despesas` da Câmara respondeu 200 com lista vazia

**Detectado:** 2026-08-21, 01:02 · **Situação:** fonte ainda fora no momento do registro

---

## O que aconteceu

A primeira tentativa de coleta completa dos 513 deputados terminou em **26 minutos** em vez
das ~12,6 horas previstas. O resumo dizia `Falhas: 0` e `Deputados visitados: 508`.

**497 desses 508 voltaram com zero despesas.**

| | |
|---|---|
| Deputados visitados | 508 (5 pulados por checkpoint válido) |
| Com dado real | **16** |
| Com alerta de sanidade | **497** |
| Despesas novas | 17.680 (praticamente todas dos 16) |
| Falhas reportadas pela fonte | **0** |

## Diagnóstico

A rota `/despesas` respondia **HTTP 200 em ~0,1 s** com o corpo:

```json
{"dados":[],"links":[{"rel":"self","href":"..."}]}
```

Sem `next`, sem `last` — só `self`. Uma resposta de sucesso, vazia, rápida.

Três verificações fecharam o diagnóstico:

1. **Não é parâmetro.** Testado com `idLegislatura=57`, sem `idLegislatura`, com `ano=2024` e
   com `ano=2025`. Todos devolvem zero.
2. **Não é deputado específico.** O deputado 136811, que trouxe dado **nesta mesma coleta**,
   passou a devolver zero minutos depois.
3. **Não é a API inteira.** `/deputados` devolve 3 registros, `/deputados/{id}` devolve o
   objeto, `/referencias/deputados/tipoDespesa` devolve 23. Só `/despesas` está mudo.

Houve também um `504 Gateway Timeout` isolado durante o diagnóstico, sugerindo que o backend
de despesas está degradado, não desligado.

## O que segurou

Duas defesas construídas antes funcionaram, e vale registrar porque foram desenhadas
exatamente para isto.

**O alerta de sanidade da ingestão gritou 497 vezes:**

> Deputado X está em exercício mas a fonte devolveu zero despesas na legislatura 57. Isso é
> quase sempre falha de coleta, não ausência de gasto.

Sem ele, a base teria 497 deputados aparentando gasto zero, e o ranking público mostraria
quase todo mundo zerado sem que nada parecesse errado.

**`CheckpointDeIngestao.AindaVale` é `!ComAlerta && ...`**, então checkpoint com alerta não
blinda o deputado. Os 497 serão retentados sozinhos na próxima passada.

**Não houve perda de dado e não há limpeza a fazer.** Os 16 deputados com dado real, e suas
23.523 despesas, permanecem.

## O que faltava, e foi corrigido

Faltava **parar**. A coleta marchou por 508 deputados enquanto a fonte devolvia vazio para
todos — desperdiçando 26 minutos e martelando uma API já degradada.

`ServicoDeColeta.MaximoDeAlertasSeguidos = 10` derruba a passada quando dez deputados
**seguidos** voltam em exercício e sem despesa. Um deputado recém-empossado sem gasto é
plausível; dois em sequência são possíveis; dez seguidos não acontecem por acaso.

Detalhes que os testes travam:

- O contador **zera** a cada deputado que traz dado, então alerta esparso não interrompe nada.
- Deputado **fora de exercício** não conta: ele pode legitimamente não ter gasto, e contar
  isso derrubaria a coleta por dado correto.
- Após a interrupção, **todo checkpoint gravado tem de ser retentável** — é o que torna parar
  seguro.

O worker sai com código 2 e diz explicitamente que nada foi perdido.

Com esse limite, o episódio teria parado em ~30 segundos.

## O que isso ensina sobre a arquitetura

O `Falhas: 0` do resumo estava **tecnicamente correto e completamente enganoso**. A fonte não
falhou: ela respondeu com sucesso e mentiu. Nenhuma camada de resiliência — retry, circuit
breaker de HTTP, timeout — pega isso, porque não há erro para pegar.

A única defesa possível é semântica: saber o que o dado **deveria** parecer e desconfiar
quando não parece. É a mesma disciplina do `MetadadosDeCobertura` no motor de regras —
distinguir "não achei nada" de "não consegui olhar".

## Estado no momento do registro

- Base: 16 deputados com 23.523 despesas; 497 aguardando retentativa
- Suíte: 557 testes
- Um monitor está de vigia sobre `/despesas`, sondando dois deputados de controle a cada 10
  minutos. Ele exige **duas** respostas boas seguidas antes de declarar recuperação, para não
  reagir a um repique.

## Pendência

| Pendência | Impacto | Observação |
|---|---|---|
| A coleta completa ainda não rodou | Alto | Depende da recuperação da fonte |
| Considerar espaçar as chamadas à Câmara | Médio | Hoje só a BrasilAPI tem limitador. Não há prova de que nosso ritmo causou a degradação, mas 508 deputados em 26 min é carga concentrada |
| `REG_NET_004` segue sem execução acima do piso | Alto | Precisa da base completa |
