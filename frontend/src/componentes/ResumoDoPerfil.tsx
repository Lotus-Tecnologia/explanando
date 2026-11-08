import type { PerfilDoParlamentar } from "@/lib/contrato";
import { notaDaCobertura } from "@/lib/cobertura";
import { formatarDensidade, formatarMomento, formatarNumero, formatarReais } from "@/lib/formato";
import { ContagensDeGravidade, Retrato } from "./CartaoDePessoa";

/**
 * A frase do segundo cartão. Zero achado com regra abstida nunca vira "nenhum indicador":
 * diz quantas regras puderam olhar, e quantas ficaram sem dado (ADR-0013 §2).
 */
export function fraseDosAchados(
  perfil: Pick<PerfilDoParlamentar, "flagsAltas" | "flagsMedias" | "flagsBaixas" | "regrasSemDado" | "cobertura">,
): string {
  const total = perfil.flagsAltas + perfil.flagsMedias + perfil.flagsBaixas;
  const regras = perfil.cobertura.length;
  const aplicadas = regras - perfil.regrasSemDado;
  const partes: string[] = [];

  if (total === 0) {
    partes.push(
      `Nenhum achado nas ${formatarNumero(aplicadas)} ${aplicadas === 1 ? "regra que pôde ser aplicada" : "regras que puderam ser aplicadas"}.`,
    );
  } else if (perfil.flagsAltas === 0) {
    partes.push("Nenhum achado de gravidade alta.");
  }

  if (perfil.regrasSemDado > 0) {
    partes.push(
      `${formatarNumero(perfil.regrasSemDado)} de ${formatarNumero(regras)} sem dado público, e ali a falta de achado não quer dizer nada.`,
    );
  }

  return partes.join(" ");
}

/**
 * O topo do perfil: quem é, quanto gastou e o que o motor encontrou, numa tela de celular.
 * Tudo o mais vem depois e pode ser aberto.
 */
export function ResumoDoPerfil({ perfil }: { perfil: PerfilDoParlamentar }) {
  const nota = notaDaCobertura(perfil.coberturaDeFornecedor, perfil.coberturaSuficiente);

  return (
    <section aria-labelledby="nome-do-parlamentar">
      <div className="flex items-center gap-4">
        <Retrato nome={perfil.nomeParlamentar} fotoUrl={perfil.fotoUrl} tamanho={72} />
        <div className="min-w-0">
          <h1
            id="nome-do-parlamentar"
            className="font-display text-[1.75rem] leading-tight font-bold tracking-tight text-titulo sm:text-4xl"
          >
            {perfil.nomeParlamentar}
          </h1>
          <p className="mt-1 text-texto-suave">
            {perfil.siglaPartido}, {perfil.uf}. {perfil.casa}.
          </p>
        </div>
      </div>

      {/*
        A data da varredura fica ao lado do nome, e não em rodapé: dois parlamentares podem
        ter frescor diferente, e sem a data à vista quem foi olhado mais perto parece pior.
      */}
      <p className="numeros mt-3 text-sm text-texto-suave">
        {perfil.dataUltimaVarredura
          ? `Nome civil: ${perfil.nomeCivil}. Última varredura em ${formatarMomento(perfil.dataUltimaVarredura)}, motor versão ${perfil.versaoDoMotor}.`
          : `Nome civil: ${perfil.nomeCivil}. Ainda não passou por varredura.`}
      </p>

      <div className="mt-5 grid gap-3 md:grid-cols-2">
        <div className="rounded-cartao border border-borda-suave bg-surface p-5">
          <h2 className="font-semibold text-texto-suave">Gastou da cota</h2>
          <p className="numeros mt-1 font-display text-[1.875rem] leading-tight font-bold whitespace-nowrap text-titulo">
            {formatarReais(perfil.volumeTotalGasto)}
          </p>
          <p className="mt-2 text-texto-suave">
            Em {formatarNumero(perfil.despesasAvaliadas)} despesas.{" "}
            {perfil.indiceDeRisco === null
              ? "Com menos de 100 despesas o índice seria ruído, e não é publicado."
              : `Índice ${formatarDensidade(perfil.indiceDeRisco)}: peso dos achados a cada 100 despesas.`}{" "}
            {perfil.indiceDeRisco !== null && nota ? nota : null}
          </p>
        </div>

        <div className="rounded-cartao border border-borda-suave bg-surface p-5">
          <h2 className="font-semibold text-texto-suave">O que o motor encontrou</h2>
          {perfil.flagsAltas + perfil.flagsMedias + perfil.flagsBaixas > 0 ? (
            <div className="mt-2">
              <ContagensDeGravidade item={perfil} />
            </div>
          ) : null}
          <p className="mt-2 text-texto-suave">{fraseDosAchados(perfil)}</p>
        </div>
      </div>
    </section>
  );
}
