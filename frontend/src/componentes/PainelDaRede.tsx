import type { AgrupamentoDaRede } from "@/lib/grafoDaRede";
import { PESSOAS_PARA_QUADRO_LARGO } from "@/lib/grafoDaRede";
import type { LigacaoDaRede, MapaDaRede, ParlamentarNaRede } from "@/lib/contrato";
import { GrafoDaRede } from "./GrafoDaRede";
import { formatarMoedaCompacta, formatarNumero, formatarReais } from "@/lib/formato";

/**
 * Os quadros do mapa de fornecedores compartilhados.
 *
 * **Um agrupamento por quadro, e não uma tela cheia de linhas.** A razão está medida: com
 * um fornecedor em comum bastando, 454 dos 463 parlamentares caem num componente conexo
 * único. Exigindo dois, sobram ilhas de 2 a 14 pessoas — e ilha se lê uma a uma.
 */
export function AgrupamentosDaRede({
  agrupamentos,
}: {
  agrupamentos: readonly AgrupamentoDaRede[];
}) {
  if (agrupamentos.length === 0) {
    return (
      <p className="rounded-cartao border border-borda-suave bg-surface p-5 text-sm text-texto-suave">
        Nenhum grupo de parlamentares divide dois ou mais fornecedores nesta base.
      </p>
    );
  }

  return (
    <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
      {agrupamentos.map((agrupamento) => {
        const largo = agrupamento.nos.length >= PESSOAS_PARA_QUADRO_LARGO;

        return (
          /*
            O agrupamento grande ocupa duas colunas. O viewBox e' o mesmo para todos, entao
            e' a largura em CSS que da' aos catorze nomes o espaco que os tres ja' tinham —
            sem isto, os rotulos se sobrepoem e o quadro grande fica ilegivel.
          */
          <article
            key={agrupamento.id}
            /*
              `min-w-0` nao e' enfeite: um item de grade tem `min-width: auto` por padrao, e
              a largura minima do grafo la' dentro esticava o cartao — o que punha a PAGINA
              inteira para rolar de lado, em vez de so' o grafo.
            */
            className={
              largo
                ? "min-w-0 rounded-cartao border border-borda-suave bg-surface p-4 sm:col-span-2"
                : "min-w-0 rounded-cartao border border-borda-suave bg-surface p-4"
            }
          >
            <header className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
              <h3 className="text-sm font-semibold text-titulo">
                {agrupamento.ufs.map((item) => `${item.uf} (${item.quantos})`).join(" · ")}
              </h3>

              <p className="numeros text-xs text-texto-suave">
                {formatarMoedaCompacta(agrupamento.valor)}
              </p>
            </header>

            <p className="mt-0.5 text-xs text-texto-suave">
              {agrupamento.nos.length} parlamentares · {agrupamento.pares}{" "}
              {agrupamento.pares === 1 ? "ligação" : "ligações"}
            </p>

            {/*
            No celular, um agrupamento de catorze pessoas em 343 pixels desenha o nome de
            cada uma com quatro pixels de altura — clutter, e nao informacao. Em vez de
            esconder o grafo, ele rola na horizontal com largura minima: o leitor arrasta e
            le. E' o mesmo tratamento que a tabela larga recebe.
          */}
            <div className="mt-2 -mx-1 overflow-x-auto px-1">
              <div className={largo ? "min-w-[34rem]" : ""}>
                <GrafoDaRede agrupamento={agrupamento} />
              </div>
            </div>
          </article>
        );
      })}
    </div>
  );
}

/**
 * Os agrupamentos de duas pessoas, em lista.
 *
 * São 21 dos 49, e desenhar cada um como um quadro com dois retratos e uma linha gastaria
 * meia tela para dizer o que uma linha de texto diz melhor. A dupla é a forma mais simples
 * do achado, não a mais interessante.
 */
export function DuplasDaRede({ agrupamentos }: { agrupamentos: readonly AgrupamentoDaRede[] }) {
  if (agrupamentos.length === 0) {
    return null;
  }

  return (
    <ul className="divide-y divide-borda-suave overflow-hidden rounded-cartao border border-borda-suave bg-surface">
      {agrupamentos.map((agrupamento) => {
        const [um, outro] = agrupamento.nos;
        const aresta = agrupamento.arestas[0];

        return (
          <li
            key={agrupamento.id}
            className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 px-4 py-3"
          >
            <p className="text-sm text-texto">
              <a
                href={`/parlamentares/${um.pessoa.id}`}
                className="font-medium text-titulo hover:underline"
              >
                {um.pessoa.nome}
              </a>{" "}
              <span className="text-xs text-texto-suave">
                ({um.pessoa.partido}-{um.pessoa.uf})
              </span>{" "}
              <span className="text-texto-suave">e</span>{" "}
              <a
                href={`/parlamentares/${outro.pessoa.id}`}
                className="font-medium text-titulo hover:underline"
              >
                {outro.pessoa.nome}
              </a>{" "}
              <span className="text-xs text-texto-suave">
                ({outro.pessoa.partido}-{outro.pessoa.uf})
              </span>
            </p>

            <p className="numeros text-xs text-texto-suave">
              {aresta?.fornecedores ?? 0} fornecedores ·{" "}
              {formatarMoedaCompacta(agrupamento.valor)}
              {aresta && !aresta.mesmaUf ? (
                <span className="ml-2 font-semibold text-acento">estados diferentes</span>
              ) : null}
            </p>
          </li>
        );
      })}
    </ul>
  );
}

/**
 * O que o recorte revelou, em três números.
 *
 * **O terceiro número é o que impede a página de ser lida errado.** Um mapa de pessoas
 * ligadas por dinheiro sugere articulação política; o dado diz que 91% das ligações são
 * do mesmo estado e só 19% do mesmo partido. A explicação dominante é o mercado local.
 */
export function OQueORecorteMostra({ mapa }: { mapa: MapaDaRede }) {
  const densos = mapa.pares.length;
  const mesmaUf = mapa.pares.filter((par) => par.mesmaUf).length;
  const mesmoPartido = mapa.pares.filter((par) => par.mesmoPartido).length;

  const porcentagem = (parte: number) =>
    densos === 0 ? "—" : `${Math.round((parte / densos) * 100)}%`;

  return (
    <dl className="grid gap-4 sm:grid-cols-3">
      <Ficha
        rotulo="Ligações no desenho"
        valor={formatarNumero(densos)}
        nota={`de ${formatarNumero(densos + mapa.paresComUmFornecedorSo)} pares — o resto divide um fornecedor só`}
      />
      <Ficha
        rotulo="Do mesmo estado"
        valor={porcentagem(mesmaUf)}
        nota="gabinetes contratam no mercado do próprio estado"
      />
      <Ficha
        rotulo="Do mesmo partido"
        valor={porcentagem(mesmoPartido)}
        nota="a proximidade aqui é geográfica, não partidária"
      />
    </dl>
  );
}

function Ficha({ rotulo, valor, nota }: { rotulo: string; valor: string; nota: string }) {
  return (
    <div className="rounded-cartao border border-borda-suave bg-surface p-4">
      <dt className="text-xs font-bold tracking-wider text-texto-suave uppercase">{rotulo}</dt>
      <dd className="numeros mt-1 font-display text-3xl font-bold text-titulo">{valor}</dd>
      <p className="mt-1 text-xs text-texto-suave">{nota}</p>
    </div>
  );
}

/**
 * A tabela dos fornecedores do mapa.
 *
 * **Todo desenho aqui tem gêmeo em tabela.** É a exigência de acessibilidade do produto, e
 * também a única forma de responder "qual é a empresa?", que o grafo não responde: lá a
 * aresta é a pessoa-com-pessoa, e a empresa some dentro dela.
 */
export function FornecedoresDaRede({
  ligacoes,
  parlamentares,
  quantos = 40,
}: {
  ligacoes: readonly LigacaoDaRede[];
  parlamentares: readonly ParlamentarNaRede[];
  quantos?: number;
}) {
  const porId = new Map(parlamentares.map((pessoa) => [pessoa.id, pessoa]));
  const mostrados = ligacoes.slice(0, quantos);

  return (
    <div className="overflow-x-auto rounded-cartao border border-borda-suave">
      <table className="w-full min-w-[42rem] text-left text-sm">
        <caption className="sr-only">
          Os fornecedores de maior valor entre os que atendem de dois a cinco parlamentares.
        </caption>

        <thead className="bg-surface text-xs tracking-wider text-texto-suave uppercase">
          <tr>
            <th scope="col" className="px-4 py-2 font-bold">
              Fornecedor
            </th>
            <th scope="col" className="px-4 py-2 font-bold">
              Onde
            </th>
            <th scope="col" className="px-4 py-2 text-right font-bold">
              Valor
            </th>
            <th scope="col" className="px-4 py-2 font-bold">
              Atende
            </th>
          </tr>
        </thead>

        <tbody className="divide-y divide-borda-suave">
          {mostrados.map((ligacao) => (
            <tr key={ligacao.documento} className="align-top">
              <th scope="row" className="px-4 py-3 font-medium text-titulo">
                {ligacao.fornecedor}
                {ligacao.abertaEm === null ? null : (
                  <span className="block text-xs font-normal text-texto-suave">
                    aberta em {new Date(ligacao.abertaEm).toLocaleDateString("pt-BR")}
                    {ligacao.cnaePrincipal === null ? null : ` · CNAE ${ligacao.cnaePrincipal}`}
                  </span>
                )}
              </th>

              <td className="px-4 py-3 text-texto-suave">
                {ligacao.municipio === null && ligacao.uf === null
                  ? "—"
                  : [ligacao.municipio, ligacao.uf].filter(Boolean).join(" · ")}
              </td>

              <td className="numeros px-4 py-3 text-right text-texto">
                {formatarReais(ligacao.valor)}
                <span className="block text-xs text-texto-suave">
                  {ligacao.notas} {ligacao.notas === 1 ? "nota" : "notas"}
                </span>
              </td>

              <td className="px-4 py-3 text-texto">
                <ul>
                  {ligacao.parlamentares.map((id) => {
                    const pessoa = porId.get(id);

                    return (
                      <li key={id}>
                        {pessoa === undefined ? (
                          <span className="text-texto-suave">—</span>
                        ) : (
                          <a
                            href={`/parlamentares/${id}`}
                            className="hover:text-titulo hover:underline"
                          >
                            {pessoa.nome}{" "}
                            <span className="text-xs text-texto-suave">
                              ({pessoa.partido}-{pessoa.uf})
                            </span>
                          </a>
                        )}
                      </li>
                    );
                  })}
                </ul>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
