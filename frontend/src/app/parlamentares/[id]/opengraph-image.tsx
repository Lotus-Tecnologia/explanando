import { ImageResponse } from "next/og";
import { obterPerfil } from "@/lib/api";
import { textosDoCartao } from "@/lib/cartao";
import { iniciais, retratoSeguro } from "@/lib/fotos";

/*
  O card de compartilhamento de um perfil: a imagem que o WhatsApp, o X e o Telegram mostram
  quando alguém cola o link. Até aqui nenhuma página tinha `og:image` (auditoria de 25/09), e o
  link circulava como um retângulo de texto — num produto cuja aquisição é o compartilhamento.

  O texto vem de `lib/cartao.ts`, testado à parte: a ressalva do ADR-0004 vai NA IMAGEM, porque
  o card circula sozinho, e o índice abaixo do piso sai como "não calculado", nunca como zero.
  Sem cor de alerta (ADR-0013): o card não tem gravidade para marcar, só números.
*/

export const alt = "Resumo dos gastos da cota parlamentar no Explanando";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";
export const revalidate = 300;

/*
  Para a CDN guardar o card. A rota sai dinâmica no build (ƒ) e o Next responde com
  `max-age=0, must-revalidate` — medido no compose de produção —, o que faria a Cloudflare ir
  à origem a cada pré-visualização de link. O dado muda uma vez por mês: uma hora no edge e um
  dia servindo o velho enquanto revalida é folga de sobra.
*/
const CACHE_NA_CDN = { "Cache-Control": "public, s-maxage=3600, stale-while-revalidate=86400" };

const FUNDO = "#0f172a";
const SUPERFICIE = "#1e293b";
const MARCA = "#6ee7b7";
const TEXTO = "#e2e8f0";
const SUAVE = "#adbcd0";

/**
 * A foto oficial, embutida como dado. Buscada aqui, e não deixada para o gerador, para que
 * uma casa legislativa fora do ar vire as iniciais em vez de derrubar o card — ou o build
 * inteiro, que gera os ~630 de uma vez.
 */
async function retratoEmDados(url: string | null): Promise<string | null> {
  const seguro = retratoSeguro(url);

  if (!seguro) {
    return null;
  }

  try {
    const resposta = await fetch(seguro, {
      signal: AbortSignal.timeout(5_000),
      next: { revalidate: 86_400 },
    });
    const tipo = resposta.headers.get("content-type") ?? "";

    if (!resposta.ok || !tipo.startsWith("image/")) {
      return null;
    }

    const bytes = Buffer.from(await resposta.arrayBuffer()).toString("base64");
    return `data:${tipo};base64,${bytes}`;
  } catch {
    return null;
  }
}

function Numero({ valor, legenda }: { valor: string; legenda: string }) {
  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        flex: 1,
        padding: "22px 26px",
        borderRadius: 16,
        background: SUPERFICIE,
      }}
    >
      <div style={{ display: "flex", fontSize: 44, fontWeight: 700, color: TEXTO }}>{valor}</div>
      <div style={{ display: "flex", fontSize: 22, color: SUAVE, marginTop: 6 }}>{legenda}</div>
    </div>
  );
}

export default async function Image({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const perfil = await obterPerfil(id).catch(() => null);

  // Sem perfil, um card neutro com a marca — melhor que erro na pré-visualização do link.
  if (!perfil) {
    return new ImageResponse(
      (
        <div
          style={{
            display: "flex",
            width: "100%",
            height: "100%",
            alignItems: "center",
            justifyContent: "center",
            background: FUNDO,
            color: MARCA,
            fontSize: 72,
            fontWeight: 700,
          }}
        >
          Explanando
        </div>
      ),
      size,
    );
  }

  const textos = textosDoCartao(perfil);
  const retrato = await retratoEmDados(perfil.fotoUrl);

  return new ImageResponse(
    (
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          width: "100%",
          height: "100%",
          padding: "44px 56px",
          background: FUNDO,
          color: TEXTO,
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <div style={{ display: "flex", fontSize: 34, fontWeight: 700, color: MARCA }}>Explanando</div>
          <div style={{ display: "flex", fontSize: 24, color: SUAVE }}>gastos da cota parlamentar</div>
        </div>

        <div style={{ display: "flex", alignItems: "center", marginTop: 36 }}>
          {retrato ? (
            // eslint-disable-next-line @next/next/no-img-element -- o gerador de imagem só entende <img>
            <img
              src={retrato}
              alt=""
              width={132}
              height={176}
              style={{ borderRadius: 14, objectFit: "cover" }}
            />
          ) : (
            <div
              style={{
                display: "flex",
                width: 132,
                height: 176,
                borderRadius: 14,
                background: SUPERFICIE,
                color: MARCA,
                fontSize: 52,
                fontWeight: 700,
                alignItems: "center",
                justifyContent: "center",
              }}
            >
              {iniciais(perfil.nomeParlamentar)}
            </div>
          )}

          <div style={{ display: "flex", flexDirection: "column", marginLeft: 36, flex: 1 }}>
            <div style={{ display: "flex", fontSize: 62, fontWeight: 700, lineHeight: 1.1 }}>{textos.nome}</div>
            <div style={{ display: "flex", fontSize: 30, color: SUAVE, marginTop: 12 }}>{textos.legenda}</div>
          </div>
        </div>

        <div style={{ display: "flex", gap: 20, marginTop: 36 }}>
          <Numero valor={textos.volume} legenda={textos.volumeLegenda} />
          <Numero valor={textos.indice ?? "—"} legenda={textos.indiceLegenda} />
          <Numero valor={textos.altas} legenda={textos.altasLegenda} />
        </div>

        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-end",
            marginTop: "auto",
          }}
        >
          <div style={{ display: "flex", fontSize: 24, color: SUAVE }}>{textos.ressalva}</div>
          <div style={{ display: "flex", fontSize: 28, fontWeight: 700, color: MARCA }}>{textos.endereco}</div>
        </div>
      </div>
    ),
    { ...size, headers: CACHE_NA_CDN },
  );
}
