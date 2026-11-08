"use client";

import { useRef, useState } from "react";
import type { NoDoEstado } from "@/lib/contrato";
import {
  RAIO_DO_MIOLO,
  arcoDoRotulo,
  caminhoDaAresta,
  caminhoDaForma,
  caminhoDoSetor,
  caminhoDoTalo,
  fonteDoRotuloDoSetor,
  formaDoNo,
  giroPara,
  montarGrafo,
  type NoPosicionado,
} from "@/lib/grafoRadial";
import { corDoPoder } from "@/lib/grafo";
import { formatarNumero } from "@/lib/formato";
import { BuscaNoGrafo } from "./BuscaNoGrafo";
import { PainelDaUnidade } from "./PainelDaUnidade";
import { MudancasDaUnidade } from "./MudancasDaUnidade";

/**
 * O mapa da estrutura do Estado, como grafo de nós e arestas.
 *
 * **Duas dimensões independentes.** O ângulo é o poder; o raio é a natureza jurídica —
 * ente federativo, administração direta, administração indireta, colegiado. O raio ser
 * natureza, e não profundidade na árvore, é o que impede um conselho ligado direto à
 * Presidência de parecer ter a estatura de um ministério.
 *
 * **Cada nó é um link de verdade.** O componente intercepta o clique para selecionar sem
 * sair da página, mas o `<a href>` embaixo continua lá: sem JavaScript, clicar leva à
 * página da unidade; com teclado, o nó é alcançável e ativável; com Ctrl ou botão do meio,
 * abre em outra aba. A interação é melhoria, não requisito — é a aposta do ADR-0001.
 *
 * **Forma, e não só cor.** Órgão é quadrado, entidade é hexágono, ente é círculo,
 * colegiado é losango. Quem não distingue as cores dos poderes distingue os contornos.
 */
export function GrafoDoEstado({
  nos,
  aoEscolherPoder,
  comZoom = true,
  temHistorico = false,
}: {
  nos: readonly NoDoEstado[];

  /**
   * Já houve uma segunda coleta do SIORG?
   *
   * Vai até a barra lateral: sem isto, "nenhuma mudança nesta unidade" seria lido como
   * "nada mudou" quando significa "ainda não comparamos".
   */
  temHistorico?: boolean;

  /**
   * Chamado ao clicar na **cunha** de um poder — não num nó.
   *
   * É o que liga as duas vistas: clicar no Executivo aqui e encontrar os ministros dele no
   * mapa de poder. A cunha já existia como fundo; passou a ser alvo.
   */
  aoEscolherPoder?: (chaveDoPoder: string) => void;

  comZoom?: boolean;
}) {
  const [selecionado, setSelecionado] = useState<number | null>(null);
  const [sobre, setSobre] = useState<number | null>(null);

  /*
    Zoom e deslocamento. O mapa tem 334 nos e os do anel externo tem poucos pixels de
    lado: sem aproximar, o rotulo deles nao se le e o alvo de clique nao se acerta. O
    estado e' do componente, e nao da URL, porque e' postura de leitura e nao conteudo.
  */
  const [zoom, setZoom] = useState(1);
  const [deslocamento, setDeslocamento] = useState({ x: 0, y: 0 });
  const arrastando = useRef<{ x: number; y: number } | null>(null);
  const [sobrePoder, setSobrePoder] = useState<string | null>(null);

  const grafo = montarGrafo(nos);

  if (grafo.nos.length === 0) return null;

  const porCodigo = new Map(grafo.nos.map((p) => [p.no.codigo, p]));
  const noMiolo = new Set(grafo.miolo.map((n) => n.codigo));

  const escolhido = selecionado === null ? null : porCodigo.get(selecionado);
  const giro = escolhido ? giroPara(escolhido.angulo) : 0;
  const giroEmGraus = (giro * 180) / Math.PI;

  // As arestas do nó selecionado, nos dois sentidos: quem manda nele e quem ele manda.
  const ligadas = new Set<number>();

  if (selecionado !== null) {
    for (const aresta of grafo.arestas) {
      if (aresta.de === selecionado) ligadas.add(aresta.para);
      if (aresta.para === selecionado) ligadas.add(aresta.de);
    }
  }

  const emFoco = (codigo: number) =>
    selecionado === null || codigo === selecionado || ligadas.has(codigo);

  // A margem agora precisa caber o rotulo curvo do poder, que corre por fora do anel
  // externo. Sem folga ele sai do viewBox e some sem erro nenhum.
  const margem = 78;
  const lado = (grafo.raioExterno + margem) * 2;

  // O zoom encolhe a JANELA, e nao aumenta o desenho: e' o que mantem a espessura das
  // linhas (todas com vectorEffect) e o tamanho do texto proporcionais ao que se ve.
  const visivel = lado / zoom;
  const janela = [
    -visivel / 2 + deslocamento.x,
    -visivel / 2 + deslocamento.y,
    visivel,
    visivel,
  ]
    .map((n) => n.toFixed(1))
    .join(" ");

  function aproximar(fator: number) {
    setZoom((atual) => Math.min(6, Math.max(1, atual * fator)));
  }

  function enquadrar() {
    setZoom(1);
    setDeslocamento({ x: 0, y: 0 });
  }
  const destacado = sobre ?? selecionado;
  const rotulado = destacado === null ? null : porCodigo.get(destacado);

  return (
    /*
      Duas colunas a partir de `lg`: o desenho fica e a barra lateral aparece ao lado, como
      na referência. Abaixo disso a barra vira um bloco embaixo — empilhar e' o unico jeito
      de caber, e abrir uma gaveta sobre o desenho esconderia justamente o que se clicou.
    */
    <div className={escolhido ? "lg:grid lg:grid-cols-[minmax(0,1fr)_22rem] lg:gap-6" : ""}>
      <div className="min-w-0">
        <div className="mb-4">
          <BuscaNoGrafo
            nos={grafo.nos.map((p) => p.no)}
            aoEscolher={(codigo) => setSelecionado(codigo)}
          />
        </div>

        <figure className="relative m-0">
          {comZoom ? (
            <ControlesDeZoom
              zoom={zoom}
              aoAproximar={() => aproximar(1.5)}
              aoAfastar={() => aproximar(1 / 1.5)}
              aoEnquadrar={enquadrar}
            />
          ) : null}

          {/*
            O desenho fica fora da ordem do Tab, e este atalho leva à lista em texto. Medido em
            25/09: dentro do svg de role="img" havia 334 links focáveis — quem usa teclado
            passava por 334 paradas antes do resto da página, e o leitor de tela anunciava
            focos sem papel de link (filhos de role="img" são apresentacionais).
          */}
          <a
            href="#orgaos-e-entidades"
            className="sr-only focus:not-sr-only focus:mb-2 focus:inline-block focus:bg-titulo focus:px-3 focus:py-2 focus:text-sm focus:text-sobre-titulo"
          >
            Pular o desenho e ir à lista de órgãos
          </a>

          <svg
            viewBox={janela}
            className="mx-auto block h-auto w-full max-w-[42rem] touch-manipulation active:cursor-grabbing"
            /*
            O cursor de "arrastando" sai do CSS (`active:`), e nao do ref: ler
            `arrastando.current` durante a renderizacao e' o que o react-hooks/refs proibe,
            e com razao — o valor pode mudar sem provocar novo desenho, e a mao ficaria
            fechada depois de soltar.
          */
            style={{ cursor: zoom > 1 ? "grab" : "default" }}
            onPointerDown={(evento) => {
              if (!comZoom || zoom === 1) return;

              arrastando.current = { x: evento.clientX, y: evento.clientY };
              evento.currentTarget.setPointerCapture(evento.pointerId);
            }}
            onPointerMove={(evento) => {
              const inicio = arrastando.current;

              if (!inicio) return;

              // A conversao de pixel para unidade de viewBox depende do zoom: sem ela, o
              // desenho foge do ponteiro quanto mais aproximado estiver.
              const escala = visivel / evento.currentTarget.clientWidth;

              setDeslocamento((atual) => ({
                x: atual.x - (evento.clientX - inicio.x) * escala,
                y: atual.y - (evento.clientY - inicio.y) * escala,
              }));

              arrastando.current = { x: evento.clientX, y: evento.clientY };
            }}
            onPointerUp={() => {
              arrastando.current = null;
            }}
            role="img"
            aria-label={
              `Mapa da estrutura do Estado: ${formatarNumero(grafo.nos.length)} órgãos e ` +
              "entidades, dispostos por poder no ângulo e por natureza jurídica no raio. " +
              "A lista em texto, navegável pelo teclado, está na seção Órgãos e entidades."
            }
            onClick={() => setSelecionado(null)}
          >
            <g
              transform={`rotate(${giroEmGraus.toFixed(2)})`}
              style={{ transition: "transform 600ms cubic-bezier(0.22, 1, 0.36, 1)" }}
            >
              {/*
              As cunhas de poder. Vêm primeiro de tudo: são o fundo sobre o qual o resto
              é desenhado, e é delas que o eixo ANGULAR passa a existir para quem olha.
              Antes, o poder só aparecia na cor do nó e na legenda embaixo.
            */}
              {grafo.setores.map((setor) => (
                <path
                  key={`cunha-${setor.chave}`}
                  d={caminhoDoSetor(
                    setor.inicio,
                    setor.fim,
                    RAIO_DO_MIOLO * 0.92,
                    grafo.raioExterno,
                  )}
                  fill={corDoPoder(setor.chave)}
                  /* Tinta muito pálida: a cunha situa, e não compete com o nó. */
                  opacity={sobrePoder === setor.chave ? 0.16 : 0.07}
                  style={{
                    transition: "opacity 180ms ease",
                    cursor: aoEscolherPoder ? "pointer" : "default",
                  }}
                  pointerEvents={aoEscolherPoder ? "auto" : "none"}
                  onMouseEnter={() => setSobrePoder(setor.chave)}
                  onMouseLeave={() => setSobrePoder(null)}
                  onClick={(evento) => {
                    if (!aoEscolherPoder) return;

                    // Sem isto o clique sobe ate' o <svg>, que limpa a selecao — e o leitor
                    // veria a vista trocar e o no' escolhido sumir no mesmo gesto.
                    evento.stopPropagation();
                    aoEscolherPoder(setor.chave);
                  }}
                >
                  <title>{`Ver quem comanda: ${setor.rotulo}`}</title>
                </path>
              ))}

              {/* Os anéis, como guias finíssimas. Sólidas: tracejado em grade lê-se como
                "projeção", e estas são posições reais. */}
              {grafo.aneis.map((anel) => (
                <circle
                  key={anel.indice}
                  r={anel.raio}
                  fill="none"
                  stroke="var(--color-borda-suave)"
                  strokeWidth={1}
                  vectorEffect="non-scaling-stroke"
                  opacity={0.5}
                />
              ))}

              {/*
              As arestas, em dois regimes.

              SEM seleção, cada nó recebe só um TALO — o tracinho radial que aponta para
              quem manda nele. Eram 334 curvas atravessando o disco ao mesmo tempo, e o
              leque que elas formavam dominava a tela sem deixar seguir nenhuma: cada
              aresta é um fato verdadeiro, e as 334 juntas não comunicavam nenhum.

              COM seleção, as arestas do nó escolhido aparecem por inteiro. É quando são
              poucas, e quando o trajeto completo é a informação que se quer.
            */}
              {selecionado === null
                ? grafo.nos.map((p) => {
                    const talo = caminhoDoTalo(p);

                    // Vazio quando o anel esta' cheio demais — ver ARCO_MINIMO_PARA_TALO.
                    if (talo === "") return null;

                    return (
                      <path
                        key={`talo-${p.no.codigo}`}
                        d={talo}
                        fill="none"
                        stroke={corDoPoder(p.no.chaveDoPoder)}
                        strokeWidth={0.9}
                        strokeLinecap="round"
                        vectorEffect="non-scaling-stroke"
                        opacity={0.38}
                        pointerEvents="none"
                      />
                    );
                  })
                : grafo.arestas.map((aresta) => {
                    const de = porCodigo.get(aresta.de);
                    const para = porCodigo.get(aresta.para);

                    // Aresta que sai do miolo parte do centro.
                    const origem = de ?? (noMiolo.has(aresta.de) ? { x: 0, y: 0 } : null);

                    if (!origem || !para) return null;

                    if (aresta.de !== selecionado && aresta.para !== selecionado) return null;

                    return (
                      <path
                        key={`${aresta.de}-${aresta.para}`}
                        d={caminhoDaAresta(origem, para)}
                        fill="none"
                        stroke={corDoPoder(para.no.chaveDoPoder)}
                        strokeWidth={1.6}
                        vectorEffect="non-scaling-stroke"
                        opacity={0.85}
                      />
                    );
                  })}

              {/*
              O rótulo do poder, curvado na borda do seu setor. Vive DENTRO do grupo que
              gira, porque pertence à roda: quando o disco vira, o nome do poder vira
              junto com os órgãos dele.
            */}
              <defs>
                {grafo.setores.map((setor) => (
                  <path
                    key={`arco-${setor.chave}`}
                    id={`arco-${setor.chave}`}
                    d={arcoDoRotulo(setor.inicio, setor.fim, grafo.raioExterno + 19, giro)}
                    fill="none"
                  />
                ))}
              </defs>

              {grafo.setores.map((setor) => {
                const texto = setor.rotulo.toUpperCase();
                const medida = fonteDoRotuloDoSetor(
                  texto,
                  setor.inicio,
                  setor.fim,
                  grafo.raioExterno + 19,
                );

                // Nulo quando nem encolhido caberia. O nome continua na legenda.
                if (medida === null) return null;

                return (
                  <text
                    key={`rotulo-${setor.chave}`}
                    fill={corDoPoder(setor.chave)}
                    fontSize={medida.fonte}
                    fontWeight={700}
                    letterSpacing={medida.espaco}
                    opacity={0.85}
                    pointerEvents="none"
                  >
                    <textPath
                      href={`#arco-${setor.chave}`}
                      startOffset="50%"
                      textAnchor="middle"
                    >
                      {texto}
                    </textPath>
                  </text>
                );
              })}

              {/* Os nós. */}
              {grafo.nos.map((p) => (
                <NoDoGrafo
                  key={p.no.codigo}
                  posicionado={p}
                  aceso={destacado === p.no.codigo}
                  apagado={!emFoco(p.no.codigo)}
                  aoEntrar={() => setSobre(p.no.codigo)}
                  aoSair={() => setSobre(null)}
                  aoEscolher={() => setSelecionado(p.no.codigo)}
                />
              ))}

              {/* O rótulo flutuante do nó apontado. Contra-girado para o texto ficar de pé
                mesmo com o disco virado. */}
              {rotulado ? (
                <g
                  transform={`translate(${rotulado.x} ${rotulado.y}) rotate(${(-giroEmGraus).toFixed(2)})`}
                  pointerEvents="none"
                >
                  <RotuloFlutuante
                    texto={rotulado.no.nome}
                    cor={corDoPoder(rotulado.no.chaveDoPoder)}
                  />
                </g>
              ) : null}
            </g>

            {/*
            O nome de cada anel — o eixo RADIAL, que é a natureza jurídica.

            Fica FORA do grupo que gira, e é deliberado: o anel não roda junto com a roda,
            porque "administração indireta" continua sendo o mesmo anel depois de o disco
            virar. Só o conteúdo gira; a régua fica.

            Estava faltando por inteiro. O raio era natureza jurídica desde o começo, o
            cálculo já sabia disso, e nada na tela dizia — a segunda dimensão do desenho
            existia só no parágrafo abaixo dele.

            Some enquanto há um nó selecionado: a roda gira para pôr o escolhido embaixo,
            que é exatamente onde a régua fica, e a tarja acabava em cima dele. Quem está
            olhando uma unidade não está consultando a régua.
          */}
            {selecionado !== null
              ? null
              : grafo.aneis.map((anel) =>
                  anel.rotulo === "" ? null : (
                    <RotuloDoAnel
                      key={`anel-${anel.indice}`}
                      raio={anel.raio}
                      texto={anel.rotulo}
                    />
                  ),
                )}

            {/* O miolo não gira: é a raiz, e ela é o eixo. */}
            <circle
              r={RAIO_DO_MIOLO - 8}
              fill="var(--color-surface)"
              stroke="var(--color-acento)"
              strokeWidth={2}
              vectorEffect="non-scaling-stroke"
            />

            {grafo.miolo.map((no, i) => {
              // "República Federativa do Brasil" numa linha só passava da borda do miolo.
              // Em duas, cabe; o que vem abaixo dela desce para dar lugar.
              const linhas = i === 0 ? emDuasLinhas(no.sigla ?? no.nome, 16) : [no.sigla ?? no.nome];
              const raizEmDuas =
                emDuasLinhas(grafo.miolo[0].sigla ?? grafo.miolo[0].nome, 16).length === 2;

              return (
                <text
                  key={no.codigo}
                  textAnchor="middle"
                  y={i === 0 ? (raizEmDuas ? -14 : -6) : raizEmDuas ? 24 : 14}
                  className={
                    i === 0
                      ? "fill-titulo font-display text-[15px] font-bold"
                      : "fill-texto-suave text-[12px]"
                  }
                >
                  {linhas.map((linha, j) => (
                    <tspan key={linha} x={0} dy={j === 0 ? 0 : 17}>
                      {linha}
                    </tspan>
                  ))}
                </text>
              );
            })}
          </svg>

          <figcaption className="mt-4 space-y-2 text-sm text-texto-suave">
            <p>
              O <strong className="font-semibold">ângulo</strong> é o poder; o{" "}
              <strong className="font-semibold">raio</strong> é a natureza jurídica — ente
              federativo no centro, depois administração direta e administração indireta. A
              forma do nó repete o tipo, para quem não distingue as cores. Passe o ponteiro para
              ver o nome.
            </p>
            <p>
              O tracinho que sai de cada nó aponta para quem manda nele.{" "}
              <strong className="font-semibold">Clique</strong> para ver a ligação inteira:
              desenhar as 334 de uma vez produzia um emaranhado em que nenhuma delas podia ser
              seguida.
            </p>
            <p>
              Cada poder ocupa no mínimo 30° do círculo, senão o Legislativo — 4 órgãos contra
              266 do Executivo — ficaria pequeno demais para ser clicado. A desproporção
              continua visível, e os números exatos estão na legenda.
            </p>
          </figcaption>
        </figure>
      </div>

      {escolhido ? (
        <aside
          aria-label="Detalhe da unidade selecionada"
          className="mt-6 space-y-6 lg:sticky lg:top-24 lg:mt-0 lg:max-h-[calc(100vh-8rem)] lg:overflow-y-auto"
        >
          <PainelDaUnidade
            no={escolhido.no}
            /*
              O miolo entra na lista de ligados. Sem isto, selecionar o Poder Executivo
              mostrava a quem ele manda e escondia a quem ele responde — a Uniao mora no
              miolo, e o miolo nao esta' entre os nos posicionados.
            */
            ligados={[
              ...grafo.miolo.filter((n) => ligadas.has(n.codigo)),
              ...grafo.nos.filter((p) => ligadas.has(p.no.codigo)).map((p) => p.no),
            ]}
            aoFechar={() => setSelecionado(null)}
          />

          {/*
            A chave e' o codigo da unidade: trocar de orgao RECRIA o componente, que volta
            a "carregando" sozinho. E' o jeito que o React recomenda para repor estado, e
            evita o setState em cascata dentro do efeito.
          */}
          <MudancasDaUnidade
            key={escolhido.no.codigo}
            codigo={escolhido.no.codigo}
            temHistorico={temHistorico}
          />
        </aside>
      ) : null}
    </div>
  );
}

/**
 * Os controles de aproximação.
 *
 * **Botões, e não só a roda do mouse.** O anel externo tem 222 entidades com poucos
 * pixels de lado, e sem aproximar o rótulo não se lê nem o alvo se acerta — mas amarrar
 * isso à roda do mouse sozinho deixaria de fora quem usa teclado e quem está no celular,
 * e ainda sequestraria a rolagem da página, que é o jeito mais rápido de irritar um
 * leitor. Com zoom aplicado, arrastar desloca.
 */
function ControlesDeZoom({
  zoom,
  aoAproximar,
  aoAfastar,
  aoEnquadrar,
}: {
  zoom: number;
  aoAproximar: () => void;
  aoAfastar: () => void;
  aoEnquadrar: () => void;
}) {
  return (
    <div className="absolute top-0 right-0 z-10 flex items-center gap-1 rounded-selo border border-borda-suave bg-surface/90 p-1">
      <Botao rotulo="Afastar" simbolo="−" aoClicar={aoAfastar} inativo={zoom <= 1} />

      <span className="numeros w-10 text-center text-xs text-texto-suave">
        {zoom.toFixed(1).replace(".", ",")}×
      </span>

      <Botao rotulo="Aproximar" simbolo="+" aoClicar={aoAproximar} inativo={zoom >= 6} />

      <Botao
        rotulo="Enquadrar o mapa inteiro"
        simbolo="⤢"
        aoClicar={aoEnquadrar}
        inativo={zoom === 1}
      />
    </div>
  );
}

function Botao({
  rotulo,
  simbolo,
  aoClicar,
  inativo,
}: {
  rotulo: string;
  simbolo: string;
  aoClicar: () => void;
  inativo: boolean;
}) {
  return (
    <button
      type="button"
      onClick={aoClicar}
      disabled={inativo}
      aria-label={rotulo}
      title={rotulo}
      className="flex size-7 items-center justify-center rounded-campo text-sm font-bold text-texto-suave hover:bg-realce hover:text-titulo disabled:cursor-not-allowed disabled:opacity-35 disabled:hover:bg-transparent"
    >
      {simbolo}
    </button>
  );
}

function NoDoGrafo({
  posicionado,
  aceso,
  apagado,
  aoEntrar,
  aoSair,
  aoEscolher,
}: {
  posicionado: NoPosicionado;
  aceso: boolean;
  apagado: boolean;
  aoEntrar: () => void;
  aoSair: () => void;
  aoEscolher: () => void;
}) {
  const { no, x, y, tamanho } = posicionado;
  const cor = corDoPoder(no.chaveDoPoder);

  return (
    <a
      href={`/estado/${no.codigo}`}
      onClick={(evento) => {
        // Ctrl, Cmd e botão do meio continuam abrindo a página, como em qualquer link.
        if (evento.metaKey || evento.ctrlKey || evento.shiftKey) return;

        evento.preventDefault();
        evento.stopPropagation();
        aoEscolher();
      }}
      onMouseEnter={aoEntrar}
      onMouseLeave={aoSair}
      onFocus={aoEntrar}
      onBlur={aoSair}
      // Fora do Tab: o svg é role="img", e o teclado navega pela lista em texto (ver o atalho
      // antes do desenho) e pela busca no grafo.
      tabIndex={-1}
      aria-label={`${no.nome} — ${no.tipo}${no.poder ? `, ${no.poder}` : ""}`}
    >
      <g transform={`translate(${x.toFixed(1)} ${y.toFixed(1)})`}>
        {/* Alvo generoso e invisível: um nó de 4px que exige mira é o defeito que isto
            evita. O raio de 15 dá ~30px de diâmetro no tamanho renderizado. */}
        <circle r={15} className="fill-transparent" />

        <path
          d={caminhoDaForma(formaDoNo(no.chaveDoTipo), aceso ? tamanho * 1.35 : tamanho)}
          fill={aceso ? cor : "var(--color-fundo)"}
          stroke={cor}
          strokeWidth={aceso ? 2 : 1.4}
          vectorEffect="non-scaling-stroke"
          opacity={apagado ? 0.18 : 1}
          style={{ transition: "opacity 200ms, d 120ms" }}
        />

        <title>
          {`${no.nome}${no.sigla ? ` (${no.sigla})` : ""} — ${no.tipo}` +
            `${no.poder ? `, ${no.poder}` : ""}` +
            `, ${formatarNumero(no.unidadesSubordinadas)} unidades subordinadas`}
        </title>
      </g>
    </a>
  );
}

/**
 * O rótulo que segue o ponteiro.
 *
 * Fundo opaco com a cor do poder no texto: sobre um feixe de arestas, texto sem fundo
 * vira ilegível na hora.
 */
/**
 * O nome de um anel, escrito sobre a régua radial.
 *
 * **Fica na vertical de baixo, com uma tarja atrás.** Ali é onde o desenho tem menos nó em
 * qualquer rotação, e a tarja resolve o resto: sem ela o texto cai em cima de uma aresta
 * ou de um contorno e vira borrão. O deslocamento para dentro do anel é o que impede o
 * rótulo de sentar exatamente sobre os nós, que ficam no raio exato.
 */
function RotuloDoAnel({ raio, texto }: { raio: number; texto: string }) {
  // Bem para DENTRO do anel, e nao rente a ele: os nos ficam no raio exato, e um rotulo
  // colado na linha divide espaco com eles. Aqui ele cai no vao entre dois aneis.
  const y = raio - 26;
  const largura = texto.length * 5.6 + 14;

  return (
    <g pointerEvents="none">
      <rect
        x={-largura / 2}
        y={y - 9}
        width={largura}
        height={14}
        rx={7}
        fill="var(--color-fundo)"
        opacity={0.82}
      />
      <text
        y={y}
        textAnchor="middle"
        fontSize={9.5}
        fontWeight={700}
        letterSpacing={1.1}
        fill="var(--color-texto-suave)"
      >
        {texto.toUpperCase()}
      </text>
    </g>
  );
}

function RotuloFlutuante({ texto, cor }: { texto: string; cor: string }) {
  /*
    O NOME, e não a sigla. "PE" não diz nada a quem está descobrindo o que é aquele ponto,
    e o rótulo existe justamente para isso.

    Truncado em 46 caracteres: "Instituto Federal de Educação, Ciência e Tecnologia do
    Sudeste de Minas Gerais" sairia do desenho pelos dois lados. O nome inteiro está no
    `<title>` e no painel.
  */
  const LIMITE = 46;
  const curto = texto.length > LIMITE ? `${texto.slice(0, LIMITE - 1).trimEnd()}…` : texto;

  // Largura estimada pelo número de caracteres. Medir exigiria o DOM, e este componente
  // também renderiza no servidor.
  const largura = Math.max(46, curto.length * 7.4 + 16);

  return (
    <>
      <rect
        x={-largura / 2}
        y={-38}
        width={largura}
        height={22}
        rx={6}
        fill="var(--color-surface)"
        stroke={cor}
        strokeWidth={1}
        vectorEffect="non-scaling-stroke"
      />
      <text
        textAnchor="middle"
        y={-23}
        className="fill-titulo text-[12px] font-semibold"
        style={{ paintOrder: "stroke" }}
      >
        {curto}
      </text>
    </>
  );
}

/**
 * A legenda dos poderes.
 *
 * Fica fora do SVG: dentro, o texto escala com o desenho e some no celular.
 */
export function LegendaDePoderes({
  poderes,
}: {
  poderes: readonly { rotulo: string; unidades: number }[];
}) {
  return (
    <ul className="flex flex-wrap gap-x-5 gap-y-2">
      {poderes.map((poder) => (
        <li key={poder.rotulo} className="flex items-center gap-2 text-sm">
          <span
            aria-hidden="true"
            className="size-3 shrink-0 rounded-[3px]"
            style={{ background: corDoPoder(poder.rotulo) }}
          />
          <span className="text-texto">{poder.rotulo}</span>
          <span className="numeros text-texto-suave">{formatarNumero(poder.unidades)}</span>
        </li>
      ))}
    </ul>
  );
}

/**
 * Parte um rótulo em duas linhas no espaço mais perto do meio, quando ele passa do limite.
 * Rótulo curto, ou sem espaço, fica como está: quebrar palavra no meio é pior que apertar.
 */
function emDuasLinhas(texto: string, limite: number): string[] {
  if (texto.length <= limite || !texto.includes(" ")) {
    return [texto];
  }

  const meio = texto.length / 2;
  let corte = -1;

  for (let i = 0; i < texto.length; i++) {
    if (texto[i] === " " && (corte === -1 || Math.abs(i - meio) < Math.abs(corte - meio))) {
      corte = i;
    }
  }

  return [texto.slice(0, corte), texto.slice(corte + 1)];
}
