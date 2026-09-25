// Baixa os arquivos anuais do TSE que o comando `importar-tse` do worker lê.
//
// Por que um script em Node, e não o próprio worker: o CDN do TSE (Akamai) recusa curl,
// PowerShell, Python e o HttpClient do .NET pela impressão digital do cliente — medido em
// 2026-09-25. O `fetch` do Node passa. Os arquivos saem uma vez por eleição, então baixar à mão
// não é gargalo. Ver docs/contratos/tse.md.
//
// Uso, da raiz do repositório:
//   node ferramentas/baixar-tse.mjs [ano] [pasta]
// Padrão: 2022, dados/tse. São ~1 GB compactados.

import { createWriteStream, mkdirSync } from "node:fs";
import { join } from "node:path";
import { Readable } from "node:stream";
import { pipeline } from "node:stream/promises";

const ano = process.argv[2] ?? "2022";
const pasta = process.argv[3] ?? join("dados", "tse");
const base = "https://cdn.tse.jus.br/estatistica/sead/odsele";

const arquivos = [
  [`prestacao_contas/prestacao_de_contas_eleitorais_orgaos_partidarios_${ano}.zip`, `orgaos_partidarios_${ano}.zip`],
  [`prestacao_contas/prestacao_de_contas_eleitorais_candidatos_${ano}.zip`, `candidatos_${ano}.zip`],
  [`votacao_candidato_munzona/votacao_candidato_munzona_${ano}.zip`, `votacao_munzona_${ano}.zip`],
];

const cabecalhos = {
  "User-Agent":
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Safari/537.36",
  Accept: "*/*",
  "Accept-Language": "pt-BR,pt;q=0.9",
};

mkdirSync(pasta, { recursive: true });

for (const [origem, destino] of arquivos) {
  const url = `${base}/${origem}`;
  const resposta = await fetch(url, { headers: cabecalhos });

  if (!resposta.ok) {
    console.error(`${resposta.status} em ${url}`);
    process.exit(1);
  }

  const caminho = join(pasta, destino);
  const tamanho = Number(resposta.headers.get("content-length") ?? 0);

  console.log(`baixando ${destino} (${(tamanho / 1e6).toFixed(0)} MB)...`);
  await pipeline(Readable.fromWeb(resposta.body), createWriteStream(caminho));
}

console.log(`pronto. Agora: dotnet run --project backend/src/Explanando.Worker -- importar-tse --ano ${ano} --pasta <caminho absoluto de ${pasta}>`);
