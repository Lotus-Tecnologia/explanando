import Link from "next/link";
import type { RespostaDoGabinete } from "@/lib/contrato";
import { formatarMomento } from "@/lib/formato";

/**
 * A resposta oficial do gabinete, ao lado do achado a que ela responde.
 *
 * É o que sobra de valioso das ideias de discussão pública e notas da comunidade — o
 * contraditório — com o menor risco possível (ADR-0020): quem escreve é parte identificada,
 * e o texto é sobre o próprio gabinete, não sobre terceiros.
 *
 * **Publicada sem edição.** O produto não modera a resposta oficial: moderar o mérito faria
 * da plataforma árbitro da disputa. Por isso o texto sai inteiro, com a origem declarada.
 */
export function RespostasDoGabinete({
  respostas,
  compacta = false,
}: {
  respostas: RespostaDoGabinete[];

  /** Na lista de despesas: só a existência e o começo do texto. A íntegra fica na página. */
  compacta?: boolean;
}) {
  if (respostas.length === 0) {
    return null;
  }

  return (
    <div className="space-y-2">
      {respostas.map((resposta) => (
        <blockquote
          key={resposta.enviadaEm}
          className="border-l-4 border-titulo bg-surface px-3 py-2"
        >
          <p className="text-xs font-semibold text-titulo">
            Resposta do gabinete{" "}
            <span className="numeros font-normal text-texto-suave">
              · enviada em {formatarMomento(resposta.enviadaEm)}
            </span>
          </p>
          <p
            className={`mt-1 whitespace-pre-line text-sm text-texto ${
              compacta ? "line-clamp-2" : ""
            }`}
          >
            {resposta.texto}
          </p>
          {!compacta && resposta.urlDoAnexo ? (
            <a
              href={resposta.urlDoAnexo}
              target="_blank"
              rel="noopener noreferrer"
              className="mt-1 inline-block text-xs font-semibold text-titulo underline underline-offset-2"
            >
              Ver o anexo enviado pelo gabinete ↗
            </a>
          ) : null}
        </blockquote>
      ))}
    </div>
  );
}

/**
 * O lugar da resposta quando ainda não há nenhuma — só na página da despesa.
 *
 * Não é caixa vazia: diz que o espaço existe e como o gabinete o usa. Uma lacuna sem
 * explicação seria lida como "o gabinete foi procurado e não respondeu", o que ninguém sabe.
 */
export function SemRespostaDoGabinete() {
  return (
    <p className="text-sm text-texto-suave">
      Não há resposta do gabinete a esta despesa até agora. Quando houver, ela aparece aqui, ao
      lado do achado, sem edição.{" "}
      <Link
        href="/privacidade#contestacao"
        className="font-semibold text-titulo underline underline-offset-2"
      >
        Como o gabinete responde
      </Link>
    </p>
  );
}
