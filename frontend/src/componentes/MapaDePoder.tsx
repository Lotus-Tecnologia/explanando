import Image from "next/image";
import type { CadeiraDoMapa, CamadaDoMapa, MapaDePoder as Mapa } from "@/lib/contrato";
import { iniciais, retratoSeguro } from "@/lib/fotos";
import { corDoPoder } from "@/lib/grafo";
import { formatarMomento } from "@/lib/formato";

/**
 * O mapa de poder de um poder: quem ocupa as cadeiras de comando, em camadas.
 *
 * **É a vista de pessoas que se alterna com o grafo de instituições**, e o formato é o da
 * referência (`docs/referencias/civlab.md` §6): retratos circulares em faixas verticais,
 * a chefia em cima e a representação embaixo. O que muda é a aresta do sentido — lá as
 * pessoas se ligam por citação em jornal, aqui a camada é o cargo que a pessoa ocupa, que
 * é fato publicado pelo próprio Estado.
 *
 * **Cada cadeira mostra de onde veio e quando foi vista.** Não é rodapé de auditoria: uma
 * tabela de ocupantes envelhece em silêncio — o ministro troca e a página continua igual —
 * e a data é o único antídoto. Por isso o rodapé do mapa usa a verificação mais *antiga*
 * do conjunto, que é o pior caso.
 */
export function MapaDePoder({ mapa }: { mapa: Mapa }) {
  if (mapa.totalDeCadeiras === 0) {
    return <PoderSemOcupantes mapa={mapa} />;
  }

  return (
    <div className="space-y-8">
      {mapa.camadas.map((camada) => (
        <Camada key={camada.chave} camada={camada} chaveDoPoder={mapa.chaveDoPoder} />
      ))}
    </div>
  );
}

function Camada({ camada, chaveDoPoder }: { camada: CamadaDoMapa; chaveDoPoder: string }) {
  /*
    A chefia tem retrato grande e as camadas de baixo vão diminuindo. É hierarquia
    desenhada como tamanho — o mesmo recurso da referência, onde o retrato cresce com a
    importância. Aqui a importância não é uma métrica nossa: é a camada do cargo.
  */
  const grande = camada.ordem === 0;
  const media = camada.ordem === 1;

  return (
    <section>
      <h3 className="flex items-baseline gap-2 text-xs font-bold tracking-widest text-texto-suave uppercase">
        <span
          aria-hidden="true"
          className="inline-block size-2 rounded-full"
          style={{ background: corDoPoder(chaveDoPoder) }}
        />
        {camada.rotulo}
        <span className="numeros font-normal normal-case">{camada.cadeiras.length}</span>
      </h3>

      <ul
        className={
          grande
            ? "mt-4 flex flex-wrap justify-center gap-8"
            : media
              ? "mt-4 grid grid-cols-2 gap-x-4 gap-y-6 sm:grid-cols-3 lg:grid-cols-4"
              : "mt-4 grid grid-cols-3 gap-x-3 gap-y-5 sm:grid-cols-4 lg:grid-cols-6"
        }
      >
        {camada.cadeiras.map((cadeira, indice) => (
          /*
            O indice entra na chave como rede de seguranca. Cargo + ocupante DEVERIA ser
            unico, e quando nao foi — a mesma deputada liderando partido, bloco e bancada
            com o mesmo rotulo — o React omitiu cadeiras silenciosamente. O coletor foi
            corrigido na origem; isto aqui impede que o proximo caso do genero suma da tela
            em vez de aparecer duplicado, que e' o defeito mais facil de nao notar.
          */
          <li key={`${cadeira.cargo}-${cadeira.ocupante ?? "vaga"}-${indice}`}>
            <Cadeira
              cadeira={cadeira}
              chaveDoPoder={chaveDoPoder}
              tamanho={grande ? 104 : media ? 64 : 48}
            />
          </li>
        ))}
      </ul>
    </section>
  );
}

function Cadeira({
  cadeira,
  chaveDoPoder,
  tamanho,
}: {
  cadeira: CadeiraDoMapa;
  chaveDoPoder: string;
  tamanho: number;
}) {
  const retrato = retratoSeguro(cadeira.fotoUrl);
  const cor = corDoPoder(chaveDoPoder);

  return (
    <figure className="m-0 flex flex-col items-center text-center">
      <div
        className="relative flex items-center justify-center overflow-hidden rounded-full bg-surface"
        style={{
          width: tamanho,
          height: tamanho,
          /*
            Anel na cor do poder. Cadeira VAGA recebe traço interrompido em vez de anel
            cheio: é cor E forma, como manda o ADR-0013, e vaga não é gravidade — o anel
            nunca é vermelho.
          */
          boxShadow: cadeira.vaga ? "none" : `0 0 0 2.5px ${cor}`,
          border: cadeira.vaga ? `2.5px dashed ${cor}` : "none",
        }}
      >
        {cadeira.vaga ? (
          <span className="px-1 text-[10px] leading-tight font-semibold text-texto-suave">
            Vaga
          </span>
        ) : retrato === null ? (
          <span
            className="font-bold text-texto-suave"
            style={{ fontSize: Math.round(tamanho * 0.3) }}
          >
            {iniciais(cadeira.ocupante ?? cadeira.cargo)}
          </span>
        ) : (
          <Image
            src={retrato}
            alt=""
            width={tamanho}
            height={tamanho}
            className="size-full object-cover object-top"
            unoptimized
          />
        )}
      </div>

      <figcaption className="mt-2 max-w-[11rem]">
        <p
          className="font-semibold text-titulo"
          style={{ fontSize: tamanho >= 104 ? 15 : 12.5 }}
        >
          {cadeira.ocupante ?? "Sem ocupante"}
        </p>

        <p className="mt-0.5 text-[11px] leading-snug text-texto-suave">{cadeira.cargo}</p>

        {cadeira.partido === null ? null : (
          <p className="numeros mt-0.5 text-[11px] text-texto-suave">
            {cadeira.partido}
            {cadeira.uf === null ? "" : `-${cadeira.uf}`}
          </p>
        )}

        {cadeira.curada ? (
          <p className="mt-1 text-[10px] font-semibold text-acento">transcrito à mão</p>
        ) : null}
      </figcaption>
    </figure>
  );
}

/**
 * Um poder do qual não temos os ocupantes.
 *
 * **Explica o motivo, e não só a ausência.** "Sem dados" faria parecer desleixo nosso ou
 * da fonte; o motivo real é que o cadastro oficial da estrutura não publica titular. O
 * texto vem da API, e não deste arquivo, porque é uma afirmação sobre o que o dado
 * significa — e afirmação dessas tem dono e tem teste.
 */
function PoderSemOcupantes({ mapa }: { mapa: Mapa }) {
  return (
    <div className="rounded-cartao border border-borda-suave bg-surface p-6">
      <p className="text-sm font-semibold text-titulo">
        Não sabemos quem ocupa os cargos de comando do {mapa.poder}.
      </p>

      <p className="mt-2 max-w-2xl text-sm text-texto-suave">{mapa.porQueFalta}</p>
    </div>
  );
}

/**
 * O rodapé de procedência do mapa.
 *
 * Fica fora de <see cref="MapaDePoder"/> porque pertence à tela inteira, e não a uma
 * camada: é o carimbo de "isto foi visto em tal dia, nestas páginas".
 */
export function ProcedenciaDoMapa({ mapa, ressalva }: { mapa: Mapa; ressalva: string }) {
  if (mapa.totalDeCadeiras === 0) {
    return null;
  }

  const fontes = [...new Set(mapa.camadas.flatMap((c) => c.cadeiras.map((s) => s.fonte)))];
  const temCurada = mapa.camadas.some((c) => c.cadeiras.some((s) => s.curada));

  return (
    <div className="mt-8 space-y-2 border-t border-borda-suave pt-4 text-xs text-texto-suave">
      <p>
        {mapa.totalDeCadeiras} cadeiras
        {mapa.vagas > 0 ? `, ${mapa.vagas} vagas` : ""}.{" "}
        {mapa.verificadoDesde === null ? null : (
          <>
            Verificado desde{" "}
            <time dateTime={mapa.verificadoDesde} className="numeros">
              {formatarMomento(mapa.verificadoDesde)}
            </time>
            {" — é a leitura mais antiga do conjunto, não a mais recente."}
          </>
        )}
      </p>

      {temCurada ? <p>{ressalva}</p> : null}

      <details>
        <summary className="cursor-pointer font-medium hover:text-titulo">
          De onde veio ({fontes.length} {fontes.length === 1 ? "fonte" : "fontes"})
        </summary>

        <ul className="mt-2 space-y-1">
          {fontes.map((fonte) => (
            <li key={fonte}>
              <a
                href={fonte}
                rel="noopener noreferrer nofollow"
                target="_blank"
                className="break-all hover:text-titulo hover:underline"
              >
                {fonte}
              </a>
            </li>
          ))}
        </ul>
      </details>
    </div>
  );
}
