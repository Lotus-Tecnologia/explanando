import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { notFound, permanentRedirect } from "next/navigation";
import { obterPerfil } from "@/lib/api";
import { CASAS } from "@/lib/casas";
import {
  avaliarComparacao,
  compararComposicoes,
  mesesComDespesa,
  parCanonico,
  type LadoDaRegra,
} from "@/lib/comparacao";
import type { PerfilDoParlamentar } from "@/lib/contrato";
import { formatarDensidade, formatarNumero, formatarReais } from "@/lib/formato";
import { iniciais, retratoSeguro } from "@/lib/fotos";

/* Literal pelo mesmo motivo das outras páginas: o Next lê este export estaticamente. */
export const revalidate = 300;

type Parametros = { params: Promise<{ a: string; b: string }> };

/**
 * Dois parlamentares lado a lado — a Fase 2 do plano de execução.
 *
 * **Não é pré-renderizada.** São 131.328 pares só na Câmara; a página é gerada no primeiro
 * acesso e fica em cache. Também **não é indexada**: 131 mil páginas quase iguais gastariam
 * a atenção do buscador com o que ele já tem nos perfis, e o valor desta página está no link
 * compartilhado, não na busca.
 */
export async function generateMetadata({ params }: Parametros): Promise<Metadata> {
  const { a, b } = await params;
  const [perfilA, perfilB] = await Promise.all([obterPerfil(a), obterPerfil(b)]);

  if (!perfilA || !perfilB) {
    return { title: "Comparação não encontrada" };
  }

  const [primeiro, segundo] = parCanonico(a, b);

  return {
    title: `${perfilA.nomeParlamentar} × ${perfilB.nomeParlamentar} — cota parlamentar`,
    description:
      `${perfilA.nomeParlamentar} e ${perfilB.nomeParlamentar}, lado a lado: quanto gastaram da ` +
      "cota parlamentar e de onde vêm os indicadores de cada um. Indicador não é juízo de conduta.",
    alternates: { canonical: `/comparar/${primeiro}/${segundo}` },
    robots: { index: false, follow: true },
  };
}

export default async function Comparacao({ params }: Parametros) {
  const { a, b } = await params;
  const [primeiro, segundo] = parCanonico(a, b);

  // A URL invertida vira a canônica. Um link compartilhado dos dois jeitos precisa acabar no
  // mesmo endereço, ou a conversa se divide em duas páginas.
  if (primeiro !== a) {
    permanentRedirect(`/comparar/${primeiro}/${segundo}`);
  }

  const [perfilA, perfilB] = await Promise.all([obterPerfil(a), obterPerfil(b)]);

  if (!perfilA || !perfilB) {
    notFound();
  }

  const situacao = avaliarComparacao(perfilA, perfilB);

  if (situacao.tipo === "mesmo-parlamentar") {
    permanentRedirect(`/parlamentares/${perfilA.id}`);
  }

  if (situacao.tipo === "casas-diferentes") {
    return <CasasDiferentes a={perfilA} b={perfilB} />;
  }

  const casa = CASAS.find((candidata) => candidata.nomeOficial === perfilA.casa);
  const regras = compararComposicoes(perfilA, perfilB);

  return (
    <main className="mx-auto max-w-5xl px-4 py-8">
      <Link
        href={`/parlamentares/${perfilA.id}`}
        className="text-sm text-titulo underline-offset-2 hover:underline"
      >
        ← {perfilA.nomeParlamentar}
      </Link>

      <h1 className="mt-4 text-2xl font-bold text-balance text-titulo">
        {perfilA.nomeParlamentar} × {perfilB.nomeParlamentar}
      </h1>
      <p className="mt-1 text-sm text-texto-suave">
        Dois {casa?.integrantes ?? "parlamentares"} lado a lado, na mesma cota
        {casa ? ` (${casa.cota})` : ""}.
      </p>

      {/*
        A ressalva vem ANTES dos números. Dois índices lado a lado convidam "X é mais
        corrupto que Y", e a frase que desmonta isso não pode estar num rodapé que ninguém lê.
      */}
      <aside className="mt-5 border-l-4 border-titulo bg-surface p-4 text-sm leading-relaxed text-texto">
        <p>
          <strong>Índice de achados técnicos não é juízo de conduta.</strong> Um índice maior quer
          dizer mais inconsistências entre registros públicos — e cada uma pode ter explicação
          legítima.
        </p>
        <p className="mt-2">
          A comparação também não corrige diferenças que não são escolha de ninguém: o teto da
          cota varia por estado, e quem assumiu no meio do mandato teve menos meses para gastar.
          Por isso os meses com despesa aparecem logo abaixo do volume.
        </p>
      </aside>

      <div className="mt-6 grid grid-cols-2 gap-3 sm:gap-6">
        <Cabecalho perfil={perfilA} />
        <Cabecalho perfil={perfilB} />
      </div>

      <section aria-labelledby="titulo-numeros" className="mt-6">
        <h2 id="titulo-numeros" className="sr-only">
          Os números
        </h2>
        <dl className="border border-borda-suave">
          <Linha
            rotulo="Volume gasto"
            a={formatarReais(perfilA.volumeTotalGasto)}
            b={formatarReais(perfilB.volumeTotalGasto)}
          />
          <Linha
            rotulo="Meses com despesa"
            a={formatarNumero(mesesComDespesa(perfilA))}
            b={formatarNumero(mesesComDespesa(perfilB))}
          />
          <Linha
            rotulo="Gasto médio por mês com despesa"
            a={mediaMensal(perfilA)}
            b={mediaMensal(perfilB)}
          />
          <Linha
            rotulo="Despesas analisadas"
            a={formatarNumero(perfilA.despesasAvaliadas)}
            b={formatarNumero(perfilB.despesasAvaliadas)}
          />
          <Linha
            rotulo="Fornecedores distintos"
            a={formatarNumero(perfilA.fornecedoresDistintos ?? 0)}
            b={formatarNumero(perfilB.fornecedoresDistintos ?? 0)}
          />
          <Linha
            rotulo="Índice de indicadores"
            nota="Peso dos achados a cada 100 despesas."
            a={indice(perfilA)}
            b={indice(perfilB)}
          />
          <Linha
            rotulo="Achados de alta relevância"
            a={formatarNumero(perfilA.flagsAltas)}
            b={formatarNumero(perfilB.flagsAltas)}
          />
          <Linha
            rotulo="De média e de baixa"
            a={`${formatarNumero(perfilA.flagsMedias)} · ${formatarNumero(perfilA.flagsBaixas)}`}
            b={`${formatarNumero(perfilB.flagsMedias)} · ${formatarNumero(perfilB.flagsBaixas)}`}
          />
        </dl>
      </section>

      {regras.length > 0 ? (
        <section aria-labelledby="titulo-regras" className="mt-8">
          <h2 id="titulo-regras" className="text-base font-bold text-titulo">
            De onde vem o índice de cada um
          </h2>
          <p className="mt-1 text-sm text-texto-suave">
            A fatia do peso de cada regra, dentro do índice de cada parlamentar. Cada coluna
            fecha 100%.
          </p>

          <ul className="mt-4 border border-borda-suave">
            {/* As colunas repetem os nomes: o cabeçalho com as fotos já saiu da tela aqui. */}
            <li
              aria-hidden="true"
              className="grid grid-cols-2 gap-3 border-b border-borda-suave bg-surface px-3 py-2 text-xs font-semibold text-texto-suave sm:gap-6 sm:px-4"
            >
              <span className="truncate">{perfilA.nomeParlamentar}</span>
              <span className="truncate">{perfilB.nomeParlamentar}</span>
            </li>
            {regras.map((regra) => (
              <li
                key={regra.codigo}
                className="border-b border-borda-suave p-3 last:border-b-0 sm:p-4"
              >
                <p className="text-sm text-titulo">{regra.descricao}</p>
                <div className="mt-2 grid grid-cols-2 gap-3 sm:gap-6">
                  <LadoDaBarra lado={regra.a} />
                  <LadoDaBarra lado={regra.b} />
                </div>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <p className="mt-8 border-t border-borda-suave pt-4 text-xs text-texto-suave">
        Cada número acima vem do perfil de cada parlamentar, com o mesmo motor de regras e a
        mesma base. Os achados, um a um, e as respostas dos gabinetes estão nos perfis.
      </p>
    </main>
  );
}

function indice(perfil: PerfilDoParlamentar): string {
  return perfil.indiceDeRisco === null
    ? "não calculado"
    : formatarDensidade(perfil.indiceDeRisco);
}

function mediaMensal(perfil: PerfilDoParlamentar): string {
  const meses = mesesComDespesa(perfil);
  return meses === 0 ? "—" : formatarReais(perfil.volumeTotalGasto / meses);
}

function Cabecalho({ perfil }: { perfil: PerfilDoParlamentar }) {
  const retrato = retratoSeguro(perfil.fotoUrl);

  return (
    <Link
      href={`/parlamentares/${perfil.id}`}
      className="group flex min-w-0 flex-col items-start gap-2 sm:flex-row sm:items-center sm:gap-3"
    >
      {retrato ? (
        <Image
          src={retrato}
          alt=""
          width={56}
          height={74}
          className="h-[74px] w-14 shrink-0 border border-borda-suave object-cover"
        />
      ) : (
        <span
          aria-hidden="true"
          className="flex h-[74px] w-14 shrink-0 items-center justify-center bg-realce text-sm font-bold text-titulo"
        >
          {iniciais(perfil.nomeParlamentar)}
        </span>
      )}
      <span className="min-w-0">
        <span className="block text-base font-bold text-titulo group-hover:underline">
          {perfil.nomeParlamentar}
        </span>
        <span className="block text-xs text-texto-suave">
          {perfil.siglaPartido} · {perfil.uf}
        </span>
      </span>
    </Link>
  );
}

function Linha({
  rotulo,
  nota,
  a,
  b,
}: {
  rotulo: string;
  nota?: string;
  a: string;
  b: string;
}) {
  return (
    <div className="grid grid-cols-2 gap-x-3 border-b border-borda-suave p-3 last:border-b-0 odd:bg-fundo even:bg-surface sm:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)_minmax(0,1fr)] sm:gap-x-6 sm:px-4">
      <dt className="col-span-2 text-xs font-semibold text-texto-suave sm:col-span-1 sm:text-sm">
        {rotulo}
        {nota ? <span className="block text-xs font-normal">{nota}</span> : null}
      </dt>
      <dd className="numeros mt-1 text-base font-bold text-titulo sm:mt-0">{a}</dd>
      <dd className="numeros mt-1 text-base font-bold text-titulo sm:mt-0">{b}</dd>
    </div>
  );
}

function LadoDaBarra({ lado }: { lado: LadoDaRegra }) {
  if (!lado.avaliada) {
    return <p className="text-xs font-semibold text-abstencao-texto">Não avaliada — sem dado</p>;
  }

  return (
    <div className="min-w-0">
      <p className="numeros text-xs text-texto-suave">
        {lado.porcentagem !== null ? (
          <strong className="text-sm text-titulo">{lado.porcentagem}%</strong>
        ) : null}{" "}
        {formatarNumero(lado.quantidade)} {lado.quantidade === 1 ? "achado" : "achados"}
      </p>
      {lado.porcentagem !== null ? (
        <div aria-hidden="true" className="mt-1 h-1.5 w-full overflow-hidden rounded-full bg-surface">
          <div
            className="h-full rounded-full bg-titulo"
            style={{ width: lado.porcentagem > 0 ? `max(2px, ${lado.porcentagem}%)` : "0" }}
          />
        </div>
      ) : null}
    </div>
  );
}

/** "da Câmara dos Deputados", "do Senado Federal": o artigo muda com a casa. */
function daCasa(casa: string): string {
  return casa === "Senado Federal" ? `do ${casa}` : `da ${casa}`;
}

/**
 * A recusa, com a razão. Uma página 404 aqui deixaria o leitor supor um defeito; a verdade é
 * que a comparação foi pedida e não faz sentido — e dizer por quê é o que ensina a ler a cota.
 */
function CasasDiferentes({ a, b }: { a: PerfilDoParlamentar; b: PerfilDoParlamentar }) {
  return (
    <main className="mx-auto max-w-3xl px-4 py-8">
      <h1 className="text-2xl font-bold text-titulo">Esta comparação não é feita</h1>
      <p className="mt-3 text-sm leading-relaxed text-texto">
        <strong>{a.nomeParlamentar}</strong> é {daCasa(a.casa)}, e{" "}
        <strong>{b.nomeParlamentar}</strong> é {daCasa(b.casa)}. As duas casas têm cotas diferentes — a CEAP da Câmara e a CEAPS do Senado —,
        com regras e valores próprios. Pôr os dois no mesmo eixo compararia números que não medem
        a mesma coisa.
      </p>
      <p className="mt-3 text-sm text-texto">Cada um pode ser comparado com alguém da própria casa:</p>
      <ul className="mt-2 space-y-1 text-sm">
        {[a, b].map((perfil) => (
          <li key={perfil.id}>
            <Link
              href={`/parlamentares/${perfil.id}`}
              className="font-semibold text-titulo underline underline-offset-2"
            >
              {perfil.nomeParlamentar}
            </Link>{" "}
            <span className="text-texto-suave">· {perfil.casa}</span>
          </li>
        ))}
      </ul>
    </main>
  );
}
