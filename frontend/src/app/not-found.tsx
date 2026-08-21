import { Aviso } from "@/componentes/Aviso";

export const metadata = { title: "Endereço não encontrado" };

/**
 * O 404.
 *
 * A frase evita afirmar que o parlamentar "não existe": o endereço pode estar errado, o
 * parlamentar pode ter deixado o exercício, ou a coleta pode não tê-lo alcançado. Dizer o
 * que sabemos — o endereço não corresponde a nada aqui — é honesto; dizer o resto seria
 * inventar a causa.
 */
export default function NaoEncontrado() {
  return (
    <Aviso titulo="Endereço não encontrado">
      <p>
        Nenhuma página deste site corresponde a este endereço. Ele pode ter sido digitado
        errado, ou apontar para um parlamentar que a base não cobre.
      </p>
    </Aviso>
  );
}
