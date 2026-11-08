import Link from "next/link";
import { classeDeBotao } from "./base/Botao";
import { montarConsulta, type FiltrosDeDespesas } from "@/lib/filtros";

/**
 * Anterior, próxima e onde se está.
 *
 * Sem numeração de 1 a N de propósito: um parlamentar tem centenas de páginas de despesa, e
 * uma régua com trinta números é ruído que ninguém usa. O que importa é não perder o rumo —
 * daí a posição escrita por extenso — e **não perder o filtro**, que é o erro caro: paginar
 * jogando o leitor de volta na lista inteira, sem avisar, faz o resultado seguinte parecer
 * responder à pergunta anterior.
 */
export function Paginacao({
  base,
  filtros,
  totalDePaginas,
}: {
  base: string;
  filtros: FiltrosDeDespesas;
  totalDePaginas: number;
}) {
  // Fora da faixa, o rodapé some: "Página 999 de 89" com um "Anterior" para a 998, também
  // vazia, era o que aparecia (auditoria de 25/09). Quem orienta ali é a mensagem da lista.
  if (totalDePaginas <= 1 || filtros.pagina > totalDePaginas) {
    return null;
  }

  const endereco = (pagina: number) => {
    const consulta = montarConsulta(filtros, { pagina });
    return consulta === "" ? base : `${base}?${consulta}`;
  };

  const classe = classeDeBotao("secundario");

  return (
    <nav
      aria-label="Paginação das despesas"
      className="mt-4 flex flex-wrap items-center gap-3"
    >
      {filtros.pagina > 1 ? (
        <Link href={endereco(filtros.pagina - 1)} rel="prev" className={classe}>
          ← Anterior
        </Link>
      ) : null}

      <span className="numeros text-sm text-texto-suave">
        Página {filtros.pagina} de {totalDePaginas}
      </span>

      {filtros.pagina < totalDePaginas ? (
        <Link href={endereco(filtros.pagina + 1)} rel="next" className={classe}>
          Próxima →
        </Link>
      ) : null}
    </nav>
  );
}
