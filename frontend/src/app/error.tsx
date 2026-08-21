"use client";

import { Aviso } from "@/componentes/Aviso";

/**
 * O limite de erro da aplicação (Arquitetura §2.2).
 *
 * Sem ele, uma falha ao renderizar — a API fora, um campo que mudou de forma — derruba a
 * página inteira e o leitor vê a tela branca do Next. Aqui a falha vira uma frase que
 * **atribui a causa a nós**: num produto que aponta inconsistências no gasto de terceiros,
 * deixar a impressão de que o dado é que está errado seria a pior troca possível.
 */
export default function Erro({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <Aviso
      titulo="Não conseguimos carregar esta página"
      acao={
        <button
          type="button"
          onClick={reset}
          className="inline-block border border-borda-suave px-3 py-1.5 text-sm font-semibold text-titulo hover:bg-surface focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
        >
          Tentar de novo
        </button>
      }
    >
      <p>
        A falha é da plataforma, não do dado público. Os registros da Câmara e do Senado
        continuam disponíveis nas fontes originais.
      </p>

      {/*
        O digest é o que liga esta tela ao registro do servidor. A mensagem crua não aparece
        de propósito: ela pode carregar detalhe de infraestrutura, e ninguém aqui pode fazer
        nada com ela.
      */}
      {error.digest ? (
        <p className="numeros mt-2 text-xs text-texto-suave">
          Código da ocorrência: {error.digest}
        </p>
      ) : null}
    </Aviso>
  );
}
