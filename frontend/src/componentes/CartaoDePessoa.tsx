import Image from "next/image";
import Link from "next/link";
import type { ItemDoRanking } from "@/lib/contrato";
import { formatarReais } from "@/lib/formato";
import { iniciais, retratoSeguro } from "@/lib/fotos";
import { TagDeGravidade } from "./TagDeGravidade";

/** Foto do parlamentar, ou as iniciais quando a foto não existe ou não é de origem confiável. */
export function Retrato({
  nome,
  fotoUrl,
  tamanho = 44,
}: {
  nome: string;
  fotoUrl: string | null;
  tamanho?: number;
}) {
  const url = retratoSeguro(fotoUrl);
  const estilo = { width: tamanho, height: tamanho };

  if (!url) {
    return (
      <span
        aria-hidden="true"
        style={estilo}
        className="flex shrink-0 items-center justify-center rounded-full bg-realce font-bold text-texto"
      >
        {iniciais(nome)}
      </span>
    );
  }

  return (
    <Image
      src={url}
      alt=""
      width={tamanho}
      height={tamanho}
      unoptimized
      style={estilo}
      className="shrink-0 rounded-full border border-borda-suave bg-realce object-cover"
    />
  );
}

/** As três contagens de gravidade, só as que não são zero: zero achado não vira tag. */
export function ContagensDeGravidade({ item }: { item: Pick<ItemDoRanking, "flagsAltas" | "flagsMedias" | "flagsBaixas"> }) {
  const tags = [
    ["Alta", item.flagsAltas],
    ["Media", item.flagsMedias],
    ["Baixa", item.flagsBaixas],
  ] as const;

  if (tags.every(([, n]) => n === 0)) {
    return <span className="text-sm text-texto-suave">Nenhum achado</span>;
  }

  return (
    <span className="flex flex-wrap gap-1.5">
      {tags.map(([gravidade, n]) =>
        n > 0 ? <TagDeGravidade key={gravidade} gravidade={gravidade} contagem={n} /> : null,
      )}
    </span>
  );
}

/**
 * Uma pessoa do ranking, como cartão: retrato, nome, partido e UF, o valor e, abaixo, as
 * contagens de achado ou uma barra proporcional ao maior valor da lista.
 *
 * O cartão inteiro é o alvo de toque (64px de altura mínima), mas o link é só o nome, para
 * que o leitor de tela ouça "Fulano, link" e não o cartão inteiro como rótulo.
 */
export function CartaoDePessoa({
  item,
  posicao,
  destaque,
  fracaoDaBarra,
}: {
  item: ItemDoRanking;
  posicao?: number;
  /** O número à direita. Padrão: o volume gasto. */
  destaque?: React.ReactNode;
  /** Com valor, desenha a barra no lugar das contagens. */
  fracaoDaBarra?: number;
}) {
  return (
    <div className="relative grid min-h-16 grid-cols-[44px_minmax(0,1fr)_auto] items-center gap-x-3 gap-y-2 rounded-cartao border border-borda-suave bg-surface px-3.5 py-3 hover:border-borda-estrutural">
      <Retrato nome={item.nomeParlamentar} fotoUrl={item.fotoUrl} />

      <span className="min-w-0">
        <Link
          href={`/parlamentares/${item.id}`}
          title={item.nomeParlamentar}
          className="block truncate font-semibold text-texto after:absolute after:inset-0 after:rounded-cartao"
        >
          {posicao !== undefined ? <span className="sr-only">{posicao}º, </span> : null}
          {item.nomeParlamentar}
        </Link>
        <span className="block text-sm text-texto-suave">
          {item.siglaPartido}, {item.uf}
        </span>
      </span>

      <span className="numeros text-right font-display text-[1.0625rem] font-bold whitespace-nowrap text-titulo">
        {destaque ?? formatarReais(item.volumeTotalGasto)}
      </span>

      <span className="col-start-2 col-end-4">
        {fracaoDaBarra !== undefined ? (
          <span aria-hidden="true" className="block h-1.5 rounded-full bg-realce">
            <span
              className="block h-full rounded-full bg-acento"
              style={{ width: `${Math.max(2, Math.min(100, fracaoDaBarra * 100))}%` }}
            />
          </span>
        ) : (
          <ContagensDeGravidade item={item} />
        )}
      </span>
    </div>
  );
}
