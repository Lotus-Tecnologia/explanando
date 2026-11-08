import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import {
  coletarIdsParaPreRender,
  listarDespesas,
  obterPagamentosPorFornecedor,
  obterPerfil,
  obterSociosEmComum,
  obterVotacao,
  ranquearTudo,
} from "@/lib/api";
import { metadadosDoParlamentar } from "@/lib/metadados";
import { formatarNumero } from "@/lib/formato";
import { CASAS } from "@/lib/casas";
import { BlocoDeCobertura } from "@/componentes/Abstencao";
import { ComposicaoDoIndice } from "@/componentes/ComposicaoDoIndice";
import { CompararCom } from "@/componentes/CompararCom";
import { EscadaDeFornecedores } from "@/componentes/EscadaDeFornecedores";
import { SociosEmComum } from "@/componentes/SociosEmComum";
import { VotacaoDoParlamentar } from "@/componentes/VotacaoDoParlamentar";
import { ResumoDoPerfil } from "@/componentes/ResumoDoPerfil";
import { Botao } from "@/componentes/base/Botao";
import { ListaDeDespesas } from "@/componentes/ListaDeDespesas";
import { SerieMensal } from "@/componentes/SerieMensal";

/*
  Literal, e não a constante importada de `lib/api`: o Next lê este export **estaticamente**,
  antes de executar qualquer coisa, e uma referência que ele não consegue resolver derruba o
  build inteiro com "Invalid segment configuration export". O valor é o mesmo
  `SEGUNDOS_DE_REVALIDACAO` — cinco minutos, a janela do cache do ranking na API.
*/
export const revalidate = 300;

/**
 * Quantas despesas com achado o perfil mostra antes de mandar para a lista completa. Dez, e
 * não 25: com 25 o perfil passava de 7.500px no celular mesmo com o resto recolhido.
 */
const ACHADOS_EM_DESTAQUE = 10;

/**
 * As 513 páginas de perfil, geradas no build.
 *
 * O ADR-0001 aposta o custo do projeto na **CDN**, não no Redis: é ela que absorve o pico
 * viral. Enquanto estas páginas fossem dinâmicas, cada acesso no pico virava uma consulta ao
 * Postgres — e a premissa não se sustentava.
 *
 * Sem lista, o Next não desiste da rota: `dynamicParams` continua no padrão, e o perfil que
 * faltar é renderizado sob demanda. É por isso que um build sem API no ar ainda passa.
 */
export async function generateStaticParams() {
  const ids = await coletarIdsParaPreRender();
  return ids.map((id) => ({ id }));
}

/**
 * Título e descrição próprios por parlamentar.
 *
 * Sem isto a página é estática mas não é indexável de verdade: 513 páginas com o mesmo título
 * competem entre si. O texto sai de `metadadosDoParlamentar`, que carrega as ressalvas do
 * ADR-0004 e do ADR-0009 — o resultado do buscador é lido por quem nunca abriu a página, e
 * ali não há bloco de cobertura ao lado para corrigir a impressão.
 *
 * O `obterPerfil` daqui não custa uma segunda ida à API: o Next deduplica o `fetch` dentro da
 * mesma renderização.
 */
export async function generateMetadata({
  params,
}: {
  params: Promise<{ id: string }>;
}): Promise<Metadata> {
  const { id } = await params;
  const perfil = await obterPerfil(id);

  if (!perfil) {
    return { title: "Parlamentar não encontrado" };
  }

  const { title, description } = metadadosDoParlamentar(perfil);

  return {
    title,
    description,
    alternates: {
      canonical: `/parlamentares/${id}`,
      // O leitor de RSS e o navegador descobrem o feed sozinhos por esta tag.
      types: { "application/rss+xml": `/parlamentares/${id}/feed.xml` },
    },
    openGraph: { title, description, type: "profile" },
  };
}

export default async function Perfil({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const perfil = await obterPerfil(id);

  if (!perfil) {
    notFound();
  }

  const [despesas, escada, socios, votacao] = await Promise.all([
    listarDespesas(id, { comFlag: true, itens: ACHADOS_EM_DESTAQUE }),
    obterPagamentosPorFornecedor(id),
    obterSociosEmComum(id),
    obterVotacao(id),
  ]);

  const casaDoPerfil = CASAS.find((candidata) => candidata.nomeOficial === perfil.casa);

  // Os colegas de casa, para o comparador. O Next deduplica: é a mesma busca que o ranking
  // faz, e as 513 páginas de perfil a dividem.
  const colegas = casaDoPerfil ? await ranquearTudo(casaDoPerfil, "volume") : null;

  // Volta para o ranking da casa dele, e não para a página inicial: é de lá que quem
  // clicou num nome veio, e as duas casas nunca dividem um ranking.
  const casa = casaDoPerfil;

  return (
    <main className="mx-auto max-w-4xl px-4 py-6 sm:py-10">
      <Link
        href={casa?.caminho ?? "/"}
        className="inline-flex min-h-11 items-center font-semibold text-titulo underline decoration-borda-estrutural underline-offset-4"
      >
        ← {casa ? `Ranking ${casa.chave === "camara" ? "da Câmara" : "do Senado"}` : "Início"}
      </Link>

      <div className="mt-2">
        <ResumoDoPerfil perfil={perfil} />
      </div>

      {/* Logo abaixo dos números, porque é a pergunta que o número provoca: de onde vem isso? */}
      {perfil.composicao ? (
        <div className="mt-6">
          <ComposicaoDoIndice
            composicao={perfil.composicao}
            indice={perfil.indiceDeRisco}
            despesasAvaliadas={perfil.despesasAvaliadas}
            cobertura={perfil.cobertura}
          />
        </div>
      ) : null}

      <section aria-labelledby="titulo-despesas" className="mt-10">
        <h2 id="titulo-despesas" className="font-display text-2xl font-bold tracking-tight text-titulo">
          Despesas com achado
        </h2>

        {despesas && despesas.total > 0 ? (
          <>
            <p className="mt-1 text-texto-suave">
              <strong className="numeros text-texto">{formatarNumero(despesas.total)}</strong> de{" "}
              {formatarNumero(perfil.despesasAvaliadas)} despesas receberam ao menos um
              indicador.
              {despesas.total > ACHADOS_EM_DESTAQUE ? (
                <> As {ACHADOS_EM_DESTAQUE} mais recentes estão abaixo; toque numa para abrir.</>
              ) : (
                <> Toque numa para abrir.</>
              )}
            </p>

            <ListaDeDespesas despesas={despesas.itens} casa={perfil.casa} />

            {despesas.total > ACHADOS_EM_DESTAQUE ? (
              <div className="mt-3">
                <Botao variante="secundario" href={`/parlamentares/${id}/despesas?indicador=com`}>
                  Ver as {formatarNumero(despesas.total)} despesas com indicador
                </Botao>
              </div>
            ) : null}
          </>
        ) : (
          <p className="mt-1 text-texto-suave">
            Nenhuma despesa recebeu indicador entre as que puderam ser analisadas.{" "}
            <strong className="text-texto">Isso não é atestado de regularidade</strong> — o
            bloco abaixo diz quantas regras puderam ser aplicadas.
          </p>
        )}

        {/*
          O caminho para o gasto inteiro fica à vista. Um portal que só mostra o que ele mesmo
          apontou publica a própria leitura, não a prestação de contas.
        */}
        <p className="mt-2 flex flex-wrap gap-x-5">
          <Link href={`/parlamentares/${id}/despesas`} className={LINK}>
            Ver todas as {formatarNumero(perfil.despesasAvaliadas)} despesas
          </Link>
          {/* Acompanhar sem cadastro: o feed não pede e-mail nem guarda nada de quem assina. */}
          <a href={`/parlamentares/${id}/feed.xml`} className={LINK}>
            Acompanhar por RSS
          </a>
        </p>
      </section>

      {/*
        A cobertura vem logo depois dos achados e aberta, nunca recolhida nem em rodapé: é o
        que impede o leitor de ler o silêncio de uma regra como aprovação (ADR-0013 §2).
      */}
      <div className="mt-10">
        <BlocoDeCobertura cobertura={perfil.cobertura} regrasSemDado={perfil.regrasSemDado} />
      </div>

      {colegas && colegas.length > 1 && casa ? (
        <div className="mt-6">
          <CompararCom
            id={perfil.id}
            nome={perfil.nomeParlamentar}
            colegas={colegas}
            integrante={casa.integrante}
          />
        </div>
      ) : null}

      {/* O resto abre sob demanda: é o que levava o perfil a 14 mil pixels no celular. */}
      <div className="mt-6 grid gap-2">
        <Recolhivel titulo="Gasto mês a mês">
          <SerieMensal serie={perfil.serieMensal} idDoParlamentar={perfil.id} />
        </Recolhivel>

        {escada ? (
          <Recolhivel titulo="Para quem foi o dinheiro">
            <EscadaDeFornecedores escada={escada} />
          </Recolhivel>
        ) : null}

        {votacao ? (
          <Recolhivel titulo="Como votou">
            <VotacaoDoParlamentar votacao={votacao} />
          </Recolhivel>
        ) : null}

        {socios ? (
          <Recolhivel titulo="Sócios em comum com outros gabinetes">
            <SociosEmComum resultado={socios} parlamentarId={perfil.id} nome={perfil.nomeParlamentar} />
          </Recolhivel>
        ) : null}
      </div>
    </main>
  );
}

const LINK =
  "inline-flex min-h-11 items-center font-semibold text-titulo underline decoration-borda-estrutural underline-offset-4";

/**
 * Uma seção do perfil que abre sob demanda. O componente de dentro continua com o próprio
 * `<h2>` (para quem navega por título no leitor de tela), escondido à vista porque o resumo
 * já mostra o nome; a moldura dele some para não haver cartão dentro de cartão.
 */
function Recolhivel({ titulo, children }: { titulo: string; children: React.ReactNode }) {
  return (
    <details className="group rounded-cartao border border-borda-suave bg-surface">
      <summary className="flex min-h-14 cursor-pointer list-none items-center gap-3 px-5 font-semibold text-texto [&::-webkit-details-marker]:hidden">
        <span className="flex-1">{titulo}</span>
        <svg
          aria-hidden="true"
          viewBox="0 0 20 20"
          className="size-5 shrink-0 text-texto-suave transition-transform group-open:rotate-180"
        >
          <path d="m5 8 5 5 5-5" fill="none" stroke="currentColor" strokeWidth="2" />
        </svg>
      </summary>
      <div className="border-t border-borda-suave px-5 py-4 [&>section]:border-0 [&>section]:bg-transparent [&>section]:p-0 [&>section>h2:first-child]:sr-only">
        {children}
      </div>
    </details>
  );
}
