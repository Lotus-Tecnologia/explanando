import type { ItemDoRanking } from "@/lib/contrato";
import type { Casa } from "@/lib/casas";
import { formatarDensidade, formatarNumero, formatarReais } from "@/lib/formato";

/**
 * A linha de números que abre um ranking.
 *
 * **São números sobre a CASA, não sobre a plataforma.** As referências de painel abrem com
 * telemetria do produto — "Pending Audits 42", "System Uptime 285" — que serve a quem opera
 * a ferramenta todo dia. Quem chega aqui veio de um link, quer saber de uma pessoa, e não
 * tem por que se importar com quantas despesas nós processamos.
 *
 * Então cada cartão responde a uma pergunta que o leitor faria: quanto se gastou, sobre
 * quantas despesas isso foi apurado, e qual a densidade de achados nessa casa.
 *
 * A taxa é calculada sobre o que está na tela e não vem pronta da API de propósito: assim
 * ela nunca discorda da tabela logo abaixo.
 */
export function CartoesDeNumero({
  casa,
  itens,
}: {
  casa: Casa;
  itens: readonly ItemDoRanking[];
}) {
  const volume = itens.reduce((soma, item) => soma + item.volumeTotalGasto, 0);
  const despesas = itens.reduce((soma, item) => soma + item.despesasAvaliadas, 0);

  const indicadores = itens.reduce(
    (soma, item) => soma + item.flagsAltas + item.flagsMedias + item.flagsBaixas,
    0,
  );

  const taxa = despesas === 0 ? null : (indicadores / despesas) * 100;

  return (
    <dl className="mt-6 grid gap-px overflow-hidden rounded-cartao border border-borda-suave bg-borda-suave shadow-cartao sm:grid-cols-2 lg:grid-cols-4">
      <Cartao rotulo={`${casa.integrantes} na base`} valor={formatarNumero(itens.length)} />

      <Cartao
        rotulo="Volume gasto"
        valor={formatarReais(volume)}
        nota="estornos abatidos"
      />

      <Cartao
        rotulo="Despesas analisadas"
        valor={formatarNumero(despesas)}
        nota={`cota ${casa.cota}`}
      />

      <Cartao
        rotulo="Indicadores por 100 despesas"
        /*
          Nulo quando não há despesa — e não zero. Zero diria que a densidade foi medida e
          deu nada, que é a distinção que o produto inteiro sustenta.
        */
        valor={taxa === null ? "—" : formatarDensidade(taxa)}
        nota={
          taxa === null
            ? "sem despesa analisada"
            : `${formatarNumero(indicadores)} no total`
        }
      />
    </dl>
  );
}

function Cartao({
  rotulo,
  valor,
  nota,
}: {
  rotulo: string;
  valor: string;
  nota?: string;
}) {
  return (
    <div className="bg-fundo px-4 py-4">
      <dt className="text-[11px] font-semibold tracking-wider text-texto-suave uppercase">
        {rotulo}
      </dt>

      {/*
        O número é o conteúdo do cartão, então ele recebe o display e o peso. `numeros`
        mantém a largura por dígito: sem isso os quatro cartões alinham em lugares
        diferentes e a linha treme.
      */}
      <dd className="numeros mt-1 font-display text-2xl font-bold tracking-tight text-titulo">
        {valor}
      </dd>

      {nota ? <p className="mt-0.5 text-xs text-texto-suave">{nota}</p> : null}
    </div>
  );
}
