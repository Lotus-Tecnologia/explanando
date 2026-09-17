import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    "out/**",
    "build/**",
    "next-env.d.ts",

    /*
      A saída do adaptador do Cloudflare e o estado do wrangler.

      O `.gitignore` já ignora as duas, mas a config achatada do ESLint não lê o
      `.gitignore` — ela só ignora `node_modules` e `.git` por conta própria, e a lista
      acima SUBSTITUI os padrões do `eslint-config-next` em vez de somar a eles.

      Sem estas duas linhas, um `npm run lint` rodado depois de qualquer `cf:build` varre o
      bundle gerado e devolve dezenas de milhares de problemas em código que não é nosso —
      43.595 na primeira vez que isso aconteceu. O CI nunca viu, porque lá o checkout é
      limpo e o lint roda antes de existir build; quem paga é quem desenvolve.
    */
    ".open-next/**",
    ".wrangler/**",
  ]),
]);

export default eslintConfig;
