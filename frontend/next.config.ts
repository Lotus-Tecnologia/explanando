import type { NextConfig } from "next";
import { initOpenNextCloudflareForDev } from "@opennextjs/cloudflare";

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

    /*
      Sem otimização, e isso é escolha e não limitação.

      No Cloudflare, otimizar imagem remota exige o produto Cloudflare Images, que é pago.
      Aqui não vale o custo: são retratos oficiais de 72×96 no perfil, JPEG pequeno que a
      própria Câmara já serve — o ganho de reprocessá-los é ruído, e a alternativa seria pagar
      por mês para economizar bytes que não pesam.

      De quebra some a rota de otimização, que é um proxy de saída no nosso domínio. Uma
      superfície a menos.
    */
    unoptimized: true,
  },
};

export default nextConfig;

/*
  Liga as ligações do Cloudflare — o balde do R2, as variáveis — durante `next dev`, para que
  o desenvolvimento exercite o mesmo caminho da produção. Não tem efeito no build.
*/
initOpenNextCloudflareForDev();
