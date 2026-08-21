import type { NextConfig } from "next";

const nextConfig: NextConfig = {
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
  },
};

export default nextConfig;
