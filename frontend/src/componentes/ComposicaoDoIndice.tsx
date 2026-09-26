import type { CoberturaDeRegra, ParcelaDoIndice } from "@/lib/contrato";
import { fraseDosAchadosAbaixo } from "@/lib/frases";
import { formatarDensidade, formatarNumero, porcentagensInteiras } from "@/lib/formato";
import { TagDeAbstencao } from "./Abstencao";

/** O piso do ADR-0009, o mesmo `PisoAmostralParaIndiceDeRisco` do motor. */
const PISO_AMOSTRAL = 100;

/**
 * "Índice 4,10 — e é daqui que ele vem." A Fase 1 do plano de execução.
 *
 * O índice é uma **densidade**: o peso dos achados dividido pelas despesas analisadas, por
 * 100. Então a decomposição honesta é a **fatia do peso** de cada regra, e não "+30 por
 * concentração" — isso seria uma aritmética que o motor não faz.
 *
 * Sem cor por regra, de propósito. Doze regras pediriam doze cores, e o ADR-0013 guarda a
 * cor para a gravidade; a barra aqui mede tamanho, e tamanho se lê pelo comprimento.
 */
export function ComposicaoDoIndice({
  composicao,
  indice,
  despesasAvaliadas,
  cobertura,
}: {
  composicao: ParcelaDoIndice[];
  indice: number | null;
  despesasAvaliadas: number;
  cobertura: CoberturaDeRegra[];
}) {
  const achados = composicao.reduce((soma, parcela) => soma + parcela.quantidade, 0);

  // Índice positivo sem parcela nenhuma é contrato incompleto — uma API anterior à Fase 1,
  // ou um cache velho. Dizer "o índice é zero" ao lado de um 4,10 seria afirmar o oposto do
  // número; melhor não desenhar o bloco.
  if (achados === 0 && indice !== null && indice > 0) {
    return null;
  }

  // Só as que não avaliaram NADA. As que avaliaram parte já entram na conta com o que
  // tiveram, e o bloco "O que foi analisado" diz quanto ficou de fora.
  const naoAvaliadas = cobertura.filter(
    (regra) => regra.despesasComDado === 0 && !regra.silencioEhConclusivo,
  );

  const semAmostra = indice === null;
  const porcentagens = porcentagensInteiras(composicao.map((parcela) => parcela.fatia));

  return (
    <section
      id="de-onde-vem-o-indice"
      aria-labelledby="titulo-composicao"
      className="scroll-mt-24 border border-borda-suave bg-fundo p-4 sm:p-5"
    >
      <h2 id="titulo-composicao" className="text-base font-bold text-titulo">
        De onde vem o índice
      </h2>

      <Explicacao
        indice={indice}
        achados={achados}
        despesasAvaliadas={despesasAvaliadas}
      />

      {composicao.length > 0 ? (
        <ol className="mt-4 space-y-3" aria-label="Regras que compõem o índice">
          {composicao.map((parcela, indiceDaLinha) => (
            <LinhaDaParcela
              key={parcela.codigo}
              parcela={parcela}
              porcentagem={semAmostra ? null : porcentagens[indiceDaLinha]}
            />
          ))}
        </ol>
      ) : null}

      {naoAvaliadas.length > 0 ? (
        <div className="mt-5 border-t border-borda-suave pt-4">
          <p className="text-sm font-semibold text-titulo">
            {/*
              "Nenhuma despesa", e não só "fora da conta": o bloco "O que foi analisado", logo
              abaixo, conta também as regras aplicadas EM PARTE, e as duas frases sem esse
              recorte pareciam se contradizer ("uma regra" aqui, "2 de 12" lá — Giovani
              Cherini, auditoria de 25/09).
            */}
            {naoAvaliadas.length === 1
              ? "Uma regra ficou fora da conta: não pôde ser aplicada a nenhuma despesa, por falta de dado público"
              : `${naoAvaliadas.length} regras ficaram fora da conta: não puderam ser aplicadas a nenhuma despesa, por falta de dado público`}
          </p>
          <p className="mt-1 text-xs text-texto-suave">
            Ficar fora da conta não é o mesmo que não ter achado: a regra não teve como olhar.
          </p>
          <ul className="mt-2 space-y-2">
            {naoAvaliadas.map((regra) => (
              <li key={regra.codigo} className="flex flex-wrap items-start justify-between gap-2">
                <span className="flex-1 text-sm text-texto">
                  {regra.descricao ?? regra.codigo}
                </span>
                <TagDeAbstencao motivo={regra.motivoDaAbstencao} />
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </section>
  );
}

function Explicacao({
  indice,
  achados,
  despesasAvaliadas,
}: {
  indice: number | null;
  achados: number;
  despesasAvaliadas: number;
}) {
  if (indice === null) {
    return (
      <p className="mt-2 text-sm leading-relaxed text-texto">
        O índice deste parlamentar não é publicado: são{" "}
        <strong className="numeros">{formatarNumero(despesasAvaliadas)}</strong> despesas, abaixo
        do piso de <span className="numeros">{PISO_AMOSTRAL}</span>. Com tão pouco, uma densidade
        seria ruído — um único achado pareceria um padrão.{" "}
        {achados > 0 ? (
fraseDosAchadosAbaixo(achados)
        ) : (
          "E nenhuma regra produziu achado."
        )}
      </p>
    );
  }

  if (achados === 0) {
    return (
      <p className="mt-2 text-sm leading-relaxed text-texto">
        Nenhuma das regras que puderam ser aplicadas produziu achado nas{" "}
        <strong className="numeros">{formatarNumero(despesasAvaliadas)}</strong> despesas, e por
        isso o índice é zero. <strong>Isso não é atestado de regularidade</strong> — veja abaixo
        o que ficou sem ser analisado.
      </p>
    );
  }

  return (
    <p className="mt-2 text-sm leading-relaxed text-texto">
      O índice de <strong className="numeros">{formatarDensidade(indice)}</strong> é o peso dos{" "}
      <strong className="numeros">{formatarNumero(achados)}</strong>{" "}
      {achados === 1 ? "achado" : "achados"} dividido pelas{" "}
      <strong className="numeros">{formatarNumero(despesasAvaliadas)}</strong> despesas
      analisadas, a cada 100. Cada achado pesa pela relevância — alta vale 3, média 2, baixa 1 —,
      vezes a confiança da regra que o produziu.
    </p>
  );
}

function LinhaDaParcela({
  parcela,
  porcentagem,
}: {
  parcela: ParcelaDoIndice;
  porcentagem: number | null;
}) {
  const gravidades = [
    parcela.altas > 0 ? { n: parcela.altas, nome: "alta" } : null,
    parcela.medias > 0 ? { n: parcela.medias, nome: "média" } : null,
    parcela.baixas > 0 ? { n: parcela.baixas, nome: "baixa" } : null,
  ].filter((g): g is { n: number; nome: string } => g !== null);

  // "29 achados de média relevância" quando é uma só; a quebra só quando há mistura.
  const relevancia =
    gravidades.length === 1
      ? ` de ${gravidades[0].nome} relevância`
      : gravidades.length > 1
        ? ` (${gravidades.map((g) => `${formatarNumero(g.n)} de ${g.nome}`).join(", ")} relevância)`
        : "";

  return (
    <li>
      <div className="flex items-start justify-between gap-3">
        <p className="min-w-0 flex-1 text-sm text-titulo">{parcela.descricao ?? parcela.codigo}</p>
        {porcentagem !== null ? (
          <p className="numeros shrink-0 text-base font-bold text-titulo">{porcentagem}%</p>
        ) : null}
      </div>

      {porcentagem !== null ? (
        <div
          aria-hidden="true"
          className="mt-1.5 h-2 w-full overflow-hidden rounded-full bg-surface"
        >
          {/* O mínimo de 2px mantém visível a fatia que arredonda para 0%. */}
          <div
            className="h-full rounded-full bg-titulo"
            style={{ width: `max(2px, ${parcela.fatia * 100}%)` }}
          />
        </div>
      ) : null}

      <p className="numeros mt-1 text-xs text-texto-suave">
        {formatarNumero(parcela.quantidade)} {parcela.quantidade === 1 ? "achado" : "achados"}
        {relevancia}
        {porcentagem !== null && parcela.pontosNoIndice !== null
          ? ` · ${formatarDensidade(parcela.pontosNoIndice)} ${
              parcela.pontosNoIndice === 1 ? "ponto" : "pontos"
            } do índice`
          : null}
      </p>
    </li>
  );
}
