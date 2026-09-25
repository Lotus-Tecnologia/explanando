import Link from "next/link";
import { caminhoDaComparacao } from "@/lib/comparacao";
import type { CadeiaDeSocio, EmpresaNaCadeia, QuemBeneficiaQuem } from "@/lib/contrato";
import { formatarMoedaCompacta, formatarNumero, formatarReais } from "@/lib/formato";

/**
 * Quem beneficia quem — a Fase 5 do plano de execução.
 *
 * Parlamentar → fornecedor → sócio em comum → outra empresa → outro parlamentar. A cadeia é
 * desenhada da esquerda para a direita, e em pé no celular, sempre na mesma ordem: é uma
 * sequência de fato, e a ordem é o conteúdo.
 *
 * **O sócio aparece pelo nome**, como o quadro societário da Receita o publica — decisão de
 * 2026-09-25, para a ligação ser verificável num clique. É pessoa privada, e por isso o nome
 * vem sem adjetivo e sem CPF, ao lado dos dois CNPJs em que ele pode ser conferido.
 */
export function SociosEmComum({
  resultado,
  parlamentarId,
  nome,
}: {
  resultado: QuemBeneficiaQuem;
  parlamentarId: string;
  nome: string;
}) {
  if (resultado.totalDeCadeias === 0) {
    return null;
  }

  return (
    <section aria-labelledby="titulo-socios" className="border border-borda-suave bg-fundo p-4 sm:p-5">
      <h2 id="titulo-socios" className="text-base font-bold text-titulo">
        Sócios em comum com fornecedores de outros parlamentares
      </h2>
      <p className="mt-1 text-sm text-texto-suave">
        {resultado.totalDeCadeias === 1
          ? "Uma empresa paga por este gabinete tem sócio em comum com uma empresa paga por outro."
          : `${formatarNumero(resultado.totalDeCadeias)} ligações entre empresas pagas por este gabinete e empresas pagas por outros, pelo mesmo sócio.`}{" "}
        Só entram ligações com pelo menos{" "}
        {formatarMoedaCompacta(resultado.valorMinimoPorPonta)} em cada ponta, e sócio que alcança
        até {resultado.parlamentaresMaximosPorSocio} parlamentares.
      </p>

      <p className="mt-3 border-l-4 border-titulo bg-surface p-3 text-sm text-texto">
        {resultado.ressalva}
      </p>

      <ol className="mt-4 space-y-3">
        {resultado.cadeias.map((cadeia) => (
          <Cadeia
            key={`${cadeia.fornecedor.documento}-${cadeia.outraEmpresa.documento}`}
            cadeia={cadeia}
            parlamentarId={parlamentarId}
            nome={nome}
          />
        ))}
      </ol>

      {resultado.totalDeCadeias > resultado.cadeias.length ? (
        <p className="mt-3 text-xs text-texto-suave">
          As {formatarNumero(resultado.cadeias.length)} de maior valor, de{" "}
          {formatarNumero(resultado.totalDeCadeias)}.
        </p>
      ) : null}
    </section>
  );
}

function Cadeia({
  cadeia,
  parlamentarId,
  nome,
}: {
  cadeia: CadeiaDeSocio;
  parlamentarId: string;
  nome: string;
}) {
  const socios =
    cadeia.sociosEmComum === 1 ? "1 sócio em comum" : `${cadeia.sociosEmComum} sócios em comum`;

  return (
    <li className="grid gap-2 border border-borda-suave p-3 md:grid-cols-[minmax(0,1fr)_11rem_minmax(0,1fr)] md:items-stretch md:gap-3">
      <Empresa empresa={cadeia.fornecedor} rotulo={`${nome} pagou`} />

      <div className="flex flex-col items-center justify-center px-2 text-center text-xs text-texto-suave">
        <span aria-hidden="true" className="hidden text-lg leading-none text-titulo md:block">
          ↔
        </span>
        <span aria-hidden="true" className="text-lg leading-none text-titulo md:hidden">
          ↕
        </span>
        <span className="mt-1 font-semibold text-titulo">{socios}</span>
        {cadeia.socios.map((socio) => (
          <span key={socio.nome} className="mt-1">
            <span className="block font-semibold text-texto">{socio.nome}</span>
            {socio.qualificacao}
            {socio.doacoes.length > 0 ? (
              <span className="mt-1 block text-texto">
                {/*
                  "Compatível", e não "é": nome idêntico e seis dígitos de CPF batem, mas cinco
                  dígitos seguem ocultos dos dois lados.
                */}
                Doador compatível na eleição de {socio.doacoes[0].ano}:{" "}
                {socio.doacoes
                  .map(
                    (doacao) =>
                      `${formatarMoedaCompacta(doacao.valor)} a ${doacao.destinatario}` +
                      (doacao.cargo ? ` (${doacao.siglaPartido}-${doacao.uf})` : ""),
                  )
                  .join("; ")}
              </span>
            ) : null}
          </span>
        ))}
      </div>

      <div className="min-w-0">
        <Empresa empresa={cadeia.outraEmpresa} rotulo="A outra empresa" />
        <p className="mt-2 text-xs text-texto-suave">recebeu de</p>
        <ul className="mt-0.5 space-y-1">
          {cadeia.outrosParlamentares.map(({ parlamentar, valor }) => (
            <li key={parlamentar.id} className="flex flex-wrap items-baseline justify-between gap-x-2 text-sm">
              <span>
                <Link
                  href={`/parlamentares/${parlamentar.id}`}
                  className="font-semibold text-titulo underline-offset-2 hover:underline"
                >
                  {parlamentar.nomeParlamentar}
                </Link>{" "}
                <span className="text-xs text-texto-suave">
                  {parlamentar.siglaPartido}-{parlamentar.uf}
                </span>
              </span>
              <span className="numeros text-xs text-texto-suave" title={formatarReais(valor)}>
                {formatarMoedaCompacta(valor)} ·{" "}
                <Link
                  href={caminhoDaComparacao(parlamentarId, parlamentar.id)}
                  className="text-titulo underline-offset-2 hover:underline"
                >
                  comparar
                </Link>
              </span>
            </li>
          ))}
        </ul>
      </div>
    </li>
  );
}

function Empresa({ empresa, rotulo }: { empresa: EmpresaNaCadeia; rotulo: string }) {
  return (
    <div className="min-w-0 bg-surface p-2.5">
      <p className="text-xs text-texto-suave">
        {rotulo}{" "}
        {rotulo.endsWith("pagou") ? (
          <strong className="numeros text-titulo" title={formatarReais(empresa.valor)}>
            {formatarMoedaCompacta(empresa.valor)}
          </strong>
        ) : null}
      </p>
      <p className="mt-0.5 text-sm font-semibold break-words text-titulo">{empresa.razaoSocial}</p>
      <p className="numeros text-xs text-texto-suave">{empresa.documento}</p>
    </div>
  );
}
