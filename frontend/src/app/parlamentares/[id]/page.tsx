import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { notFound } from "next/navigation";
import {
  coletarIdsParaPreRender,
  listarDespesas,
  obterPerfil,
  ranquearTudo,
} from "@/lib/api";
import { metadadosDoParlamentar } from "@/lib/metadados";
import { formatarDensidade, formatarNumero, formatarReais } from "@/lib/formato";
import { CASAS } from "@/lib/casas";
import { BlocoDeCobertura } from "@/componentes/Abstencao";
import { ComposicaoDoIndice } from "@/componentes/ComposicaoDoIndice";
import { CompararCom } from "@/componentes/CompararCom";
import { retratoSeguro } from "@/lib/fotos";
import { ListaDeDespesas } from "@/componentes/ListaDeDespesas";
import { SerieMensal } from "@/componentes/SerieMensal";

/*
  Literal, e não a constante importada de `lib/api`: o Next lê este export **estaticamente**,
  antes de executar qualquer coisa, e uma referência que ele não consegue resolver derruba o
  build inteiro com "Invalid segment configuration export". O valor é o mesmo
  `SEGUNDOS_DE_REVALIDACAO` — cinco minutos, a janela do cache do ranking na API.
*/
export const revalidate = 300;

/** Quantas despesas com achado o perfil mostra antes de mandar para a lista completa. */
const ACHADOS_EM_DESTAQUE = 25;

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
    alternates: { canonical: `/parlamentares/${id}` },
    openGraph: { title, description, type: "profile" },
  };
}

function Numero({
  rotulo,
  valor,
  nota,
}: {
  rotulo: string;
  valor: string;
  nota?: string;
}) {
  return (
    <div className="border border-borda-suave bg-fundo p-4">
      <dt className="text-xs font-semibold text-texto-suave">{rotulo}</dt>
      <dd className="numeros mt-1 text-xl font-bold text-titulo">{valor}</dd>
      {nota ? <p className="mt-1 text-xs text-texto-suave">{nota}</p> : null}
    </div>
  );
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

  const despesas = await listarDespesas(id, {
    comFlag: true,
    itens: ACHADOS_EM_DESTAQUE,
  });

  const casaDoPerfil = CASAS.find((candidata) => candidata.nomeOficial === perfil.casa);

  // Os colegas de casa, para o comparador. O Next deduplica: é a mesma busca que o ranking
  // faz, e as 513 páginas de perfil a dividem.
  const colegas = casaDoPerfil ? await ranquearTudo(casaDoPerfil, "volume") : null;
  const retrato = retratoSeguro(perfil.fotoUrl);

  // Volta para o ranking da casa dele, e não para a página inicial: é de lá que quem
  // clicou num nome veio, e as duas casas nunca dividem um ranking.
  const casa = casaDoPerfil;

  return (
    <main className="mx-auto max-w-7xl px-4 py-8">
      <Link
        href={casa?.caminho ?? "/"}
        className="text-sm text-titulo underline-offset-2 hover:underline"
      >
        ← {casa ? `Ranking ${casa.chave === "camara" ? "da Câmara" : "do Senado"}` : "Início"}
      </Link>

      <header className="mt-4 flex flex-wrap items-start gap-4">
        {/*
          A foto é oficial e vem da própria casa legislativa. Entra sem `priority`: ela não é
          o conteúdo da página, e disputar banda com o texto no celular seria trocar o que
          importa pelo enfeite.
        */}
        {retrato ? (
          <Image
            src={retrato}
            alt=""
            width={72}
            height={96}
            className="border border-borda-suave object-cover"
          />
        ) : null}

        <div className="min-w-0">
          <h1 className="text-2xl font-bold text-titulo">{perfil.nomeParlamentar}</h1>
          <p className="mt-1 text-sm text-texto-suave">
            {perfil.nomeCivil} · {perfil.siglaPartido} · {perfil.uf} · {perfil.casa}
          </p>

          {/*
            A data da varredura fica ao lado do nome, e não em rodapé.

            O dado é atualizado por rotina mensal e por varredura sob demanda, então dois
            parlamentares podem ter frescor diferente. Sem a data à vista, quem tem dado mais
            recente parece pior — não por ser, mas por ter sido olhado mais perto.
          */}
          {perfil.dataUltimaVarredura ? (
            <p className="numeros mt-1 text-xs text-texto-suave">
              Última varredura em{" "}
              {new Date(perfil.dataUltimaVarredura).toLocaleDateString("pt-BR")} · motor
              versão {perfil.versaoDoMotor}
            </p>
          ) : (
            <p className="mt-1 text-xs text-texto-suave">
              Este parlamentar ainda não passou por varredura.
            </p>
          )}
        </div>
      </header>

      <dl className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Numero
          rotulo="Volume gasto"
          valor={formatarReais(perfil.volumeTotalGasto)}
          nota={`${formatarNumero(perfil.despesasAvaliadas)} despesas analisadas`}
        />
        <Numero
          rotulo="Densidade de indicadores"
          valor={
            perfil.indiceDeRisco === null
              ? "não publicável"
              : formatarDensidade(perfil.indiceDeRisco)
          }
          nota={
            perfil.indiceDeRisco === null
              ? "Menos de 100 despesas: a densidade seria ruído."
              : "Peso dos achados a cada 100 despesas."
          }
        />
        <Numero
          rotulo="Alta relevância"
          valor={formatarNumero(perfil.flagsAltas)}
          nota="Os achados de maior peso."
        />
        <Numero
          rotulo="Média e baixa"
          valor={`${formatarNumero(perfil.flagsMedias)} · ${formatarNumero(perfil.flagsBaixas)}`}
          nota="Indicadores de peso intermediário e baixo."
        />
      </dl>

      {colegas && colegas.length > 1 && casa ? (
        <div className="mt-4">
          <CompararCom
            id={perfil.id}
            nome={perfil.nomeParlamentar}
            colegas={colegas}
            integrante={casa.integrante}
          />
        </div>
      ) : null}

      {/*
        Logo abaixo dos números, porque é a pergunta que o número provoca: de onde vem isso?
      */}
      {perfil.composicao ? (
        <div className="mt-8">
          <ComposicaoDoIndice
            composicao={perfil.composicao}
            indice={perfil.indiceDeRisco}
            despesasAvaliadas={perfil.despesasAvaliadas}
            cobertura={perfil.cobertura}
          />
        </div>
      ) : null}

      {/*
        A cobertura vem ANTES da lista de achados, e não em rodapé. É o que impede o leitor
        de ler o silêncio de uma regra como aprovação — ADR-0013 §2.
      */}
      <div className="mt-8">
        <BlocoDeCobertura
          cobertura={perfil.cobertura}
          regrasSemDado={perfil.regrasSemDado}
        />
      </div>

      <div className="mt-8">
        <SerieMensal serie={perfil.serieMensal} idDoParlamentar={perfil.id} />
      </div>

      <section aria-labelledby="titulo-despesas" className="mt-8">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <h2 id="titulo-despesas" className="text-base font-bold text-titulo">
            Despesas com indicadores
          </h2>

          {/*
            O caminho para o gasto inteiro fica à vista. Um portal que só mostra o que ele
            mesmo apontou publica a própria leitura, não a prestação de contas.
          */}
          <Link
            href={`/parlamentares/${id}/despesas`}
            className="text-sm font-semibold text-titulo underline-offset-2 hover:underline"
          >
            Ver todas as {formatarNumero(perfil.despesasAvaliadas)} despesas →
          </Link>
        </div>

        {despesas && despesas.total > 0 ? (
          <>
            <p className="mt-1 text-sm text-texto-suave">
              <strong className="numeros">{formatarNumero(despesas.total)}</strong> de{" "}
              {formatarNumero(perfil.despesasAvaliadas)} despesas receberam ao menos um
              indicador.
              {despesas.total > ACHADOS_EM_DESTAQUE ? (
                <> As {ACHADOS_EM_DESTAQUE} mais recentes aparecem abaixo.</>
              ) : null}
            </p>

            <ListaDeDespesas despesas={despesas.itens} casa={perfil.casa} />

            {despesas.total > ACHADOS_EM_DESTAQUE ? (
              <Link
                href={`/parlamentares/${id}/despesas?indicador=com`}
                className="mt-4 inline-block border border-borda-suave px-3 py-1.5 text-sm font-semibold text-titulo hover:bg-surface"
              >
                Ver as {formatarNumero(despesas.total)} despesas com indicador
              </Link>
            ) : null}
          </>
        ) : (
          <p className="mt-1 text-sm text-texto-suave">
            Nenhuma despesa recebeu indicador entre as que puderam ser analisadas.{" "}
            <strong>Isso não é atestado de regularidade</strong> — o bloco acima diz quantas
            regras puderam ser aplicadas.
          </p>
        )}
      </section>
    </main>
  );
}
