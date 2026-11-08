import type { VotacaoDoParlamentar as Votacao } from "@/lib/contrato";
import {
  formatarNomeDeLugar,
  formatarNumero,
  formatarPorcentagem,
  formatarReais,
} from "@/lib/formato";

/**
 * De onde vieram os votos — o TSE no perfil (Fase 7 do plano de execução).
 *
 * Duas coisas numa caixa, porque uma dá contexto à outra: os municípios que elegeram o
 * parlamentar, e quanto da cota foi para empresas sediadas neles. A "base eleitoral" é
 * aritmética — os municípios que somam metade dos votos —, e a caixa diz isso, para que ninguém
 * leia nela um juízo nosso sobre reduto.
 *
 * Sem cor: gastar com fornecedor da própria base não é achado. É onde o dinheiro chegou.
 */
export function VotacaoDoParlamentar({ votacao }: { votacao: Votacao }) {
  if (!votacao.temCandidatura) {
    return (
      <section aria-labelledby="titulo-votacao" className="rounded-cartao border border-borda-suave bg-surface p-5">
        <h2 id="titulo-votacao" className="font-display text-xl font-bold text-titulo">
          De onde vieram os votos
        </h2>
        <p className="mt-1 text-sm text-texto-suave">
          Não há candidatura deste parlamentar ligada à base do TSE de 2022. Acontece com senador
          eleito em 2018 e com quem o TSE registra com outro nome civil — a ligação é feita por
          igualdade de nome e não por semelhança, então, na dúvida, ela não é feita.
        </p>
      </section>
    );
  }

  const maior = votacao.principais[0]?.votos ?? 0;
  const fatiaDaBase =
    votacao.pagoAFornecedoresComEndereco > 0
      ? votacao.pagoAFornecedoresDaBase / votacao.pagoAFornecedoresComEndereco
      : null;

  return (
    <section aria-labelledby="titulo-votacao" className="rounded-cartao border border-borda-suave bg-surface p-5">
      <h2 id="titulo-votacao" className="font-display text-xl font-bold text-titulo">
        De onde vieram os votos
      </h2>
      <p className="mt-1 text-sm text-texto-suave">
        Eleição de {votacao.ano}, como <strong className="text-texto">{votacao.nomeDeUrna}</strong>{" "}
        ({votacao.siglaPartido}) — {votacao.situacao?.toLocaleLowerCase("pt-BR")}, com{" "}
        <strong className="numeros text-texto">{formatarNumero(votacao.totalDeVotos)}</strong> votos
        em {formatarNumero(votacao.municipiosComVoto)}{" "}
        {votacao.municipiosComVoto === 1 ? "município" : "municípios"}.
      </p>

      <div className="mt-4 grid gap-6 md:grid-cols-2">
        <ol className="space-y-2" aria-label="Municípios com mais votos">
          {votacao.principais.map((municipio) => (
            <li key={`${municipio.nome}-${municipio.uf}`}>
              <div className="flex items-baseline justify-between gap-3 text-sm">
                <span className="min-w-0 truncate text-titulo">
                  {formatarNomeDeLugar(municipio.nome)}
                </span>
                <span className="numeros shrink-0 text-xs text-texto-suave">
                  {formatarNumero(municipio.votos)} · {formatarPorcentagem(municipio.fatia)}
                </span>
              </div>
              <div aria-hidden="true" className="mt-1 h-1.5 w-full overflow-hidden rounded-full bg-surface">
                <div
                  className="h-full rounded-full bg-titulo"
                  style={{ width: maior > 0 ? `max(2px, ${(municipio.votos / maior) * 100}%)` : "0" }}
                />
              </div>
            </li>
          ))}
        </ol>

        <div className="text-sm leading-relaxed text-texto">
          <p>
            Metade dos votos veio de{" "}
            <strong className="numeros">{formatarNumero(votacao.municipiosDaMetadeDosVotos)}</strong>{" "}
            {votacao.municipiosDaMetadeDosVotos === 1 ? "município" : "municípios"} — somados do
            maior para o menor. É a base eleitoral nesta página: uma conta que qualquer um refaz com
            o arquivo do TSE.
          </p>

          {fatiaDaBase !== null ? (
            <p className="mt-3">
              Empresas sediadas nesses municípios receberam{" "}
              <strong className="numeros">{formatarReais(votacao.pagoAFornecedoresDaBase)}</strong> da
              cota —{" "}
              <strong className="numeros">{formatarPorcentagem(fatiaDaBase)}</strong> do que foi pago a
              fornecedores com endereço conhecido.
            </p>
          ) : null}

          <p className="mt-3 text-xs text-texto-suave">
            A sede é o endereço da empresa na Receita, e não onde o serviço foi prestado. Pagar
            fornecedor da própria base não é achado: é onde o dinheiro chegou.
          </p>
        </div>
      </div>
    </section>
  );
}
