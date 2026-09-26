import type { NextConfig } from "next";

/*
  A política de conteúdo, escrita para o que o site de fato carrega — levantado no código em
  2026-09-25: só as fotos oficiais vêm de fora (Câmara e Senado, com o redirecionamento do
  Senado para `legis.`), a fonte é servida do próprio domínio pelo `next/font`, e o navegador
  só fala com o próprio site (a ilha de mudanças do Estado pede a `/estado/.../mudancas`).

  `script-src` aceita 'unsafe-inline', e isso é concessão consciente: o Next injeta o payload
  do RSC em <script> inline, e o nonce que o evitaria obriga toda página a ser dinâmica — o
  que desmontaria o cache em que a premissa de custo se apoia (ADR-0001). A política ainda
  barra o que importa: script de outro domínio, <object>, <base> sequestrado e o site dentro
  de um iframe alheio.

  Só em produção: o `next dev` usa eval no recarregamento, e a política o quebraria.
*/
const POLITICA_DE_CONTEUDO = [
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline'",
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: https://www.camara.leg.br https://www.senado.leg.br https://legis.senado.leg.br",
  "font-src 'self'",
  "connect-src 'self'",
  "object-src 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "frame-ancestors 'none'",
].join("; ");

const CABECALHOS_DE_SEGURANCA = [
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  { key: "X-Frame-Options", value: "DENY" },
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(), interest-cohort=()" },
  // A Cloudflare termina o HTTPS; o cabeçalho vale para o navegador, que o ignora em HTTP.
  { key: "Strict-Transport-Security", value: "max-age=31536000; includeSubDomains" },
  ...(process.env.NODE_ENV === "production"
    ? [{ key: "Content-Security-Policy", value: POLITICA_DE_CONTEUDO }]
    : []),
];

const nextConfig: NextConfig = {
  // "X-Powered-By: Next.js" só serve a quem procura versão para explorar.
  poweredByHeader: false,

  async headers() {
    return [{ source: "/:path*", headers: CABECALHOS_DE_SEGURANCA }];
  },

  /*
    O servidor sai empacotado em `.next/standalone`, com só o que ele importa.

    É como o Next roda em contêiner na VPS (ADR-0019): `node server.js`, sem o node_modules
    de desenvolvimento — que é quase todo o peso — e sem adaptador de plataforma nenhum. O
    Cloudflare continua na frente, mas como CDN, não mais como runtime. Ver frontend/Dockerfile.
  */
  output: "standalone",

  images: {
    /*
      As fotos oficiais, servidas pelas próprias casas legislativas.

      A lista é explícita — e não `**` — porque `next/image` busca a imagem pelo servidor: um
      padrão aberto transformaria a rota de otimização num proxy que qualquer pessoa poderia
      apontar para qualquer endereço.
    */
    remotePatterns: [
      { protocol: "https", hostname: "www.camara.leg.br" },
      { protocol: "https", hostname: "www.senado.leg.br" },
    ],

    /*
      Sem otimização, e isso é escolha e não limitação.

      São retratos oficiais de 72×96 no perfil, JPEG pequeno que a própria casa já serve: o
      ganho de reprocessá-los é ruído, e o custo seria CPU de uma VPS de duas OCPUs gasta
      redimensionando foto — além do `sharp`, binário nativo a mais na imagem ARM.

      De quebra some a rota de otimização, que é um proxy de saída no nosso domínio. Uma
      superfície a menos.
    */
    unoptimized: true,
  },
};

export default nextConfig;
