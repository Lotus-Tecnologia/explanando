/**
 * Espelho do contrato da API pública.
 *
 * Escrito à mão, e não gerado do OpenAPI, de propósito: os comentários abaixo carregam o
 * *porquê* de campos que parecem redundantes — e é justamente esse porquê que impede a
 * interface de renderizar ausência como aprovação.
 */

/** O que aconteceu com uma regra numa passada. Sai da API por nome, não por número. */
export type SituacaoDaRegra =
  | "Emitiu"
  | "SemAchados"
  | "CoberturaParcial"
  | "SemDado"
  | "Falhou";

/** Os três pesos do ADR-0013. Vermelho fica estritamente com `Alta`. */
export type Gravidade = "Baixa" | "Media" | "Alta";

export type OrdenacaoDoRanking = "volume" | "indice" | "gravidade";

export interface Pagina<T> {
  itens: T[];
  total: number;
  numeroDaPagina: number;
  itensPorPagina: number;
  totalDePaginas: number;
}

export interface ItemDoRanking {
  id: string;
  idExterno: number;
  nomeParlamentar: string;
  siglaPartido: string;
  uf: string;
  fotoUrl: string | null;

  /** O número do B2C. Sempre presente — não depende de amostra. */
  volumeTotalGasto: number;
  despesasAvaliadas: number;

  /**
   * **Nulo quando a amostra é insuficiente**, e nulo não é zero: zero diria que a densidade
   * foi medida e deu nada. Quinze dos 513 caem aqui.
   */
  indiceDeRisco: number | null;
  indiceDeAltas: number | null;

  flagsAltas: number;
  flagsMedias: number;
  flagsBaixas: number;
  amostraSuficiente: boolean;
}

export interface CoberturaDeRegra {
  codigo: string;
  situacao: SituacaoDaRegra;
  despesasComDado: number;
  despesasSemDado: number;
  flagsEmitidas: number;

  /**
   * **O campo que decide o que a interface desenha.** Falso significa exibir a tag de *não
   * avaliado* — nunca a de aprovado. Duas regras podem ter `flagsEmitidas: 0` e significar
   * coisas opostas; este booleano é a diferença.
   */
  silencioEhConclusivo: boolean;

  /** Em uma frase, por que a regra não pôde avaliar. Nulo quando avaliou tudo. */
  motivoDaAbstencao: string | null;
}

export interface MesDoParlamentar {
  ano: number;
  mes: number;
  totalLiquido: number;
  quantidadeDeDespesas: number;

  /** Nulo quando a origem não informa a glosa. O Senado inteiro cai aqui. */
  totalGlosa: number | null;

  /** Falso depois da poda: não há como oferecer "ver as notas deste mês". */
  detalheDisponivel: boolean;
}

export interface PerfilDoParlamentar {
  id: string;
  idExterno: number;
  nomeParlamentar: string;
  nomeCivil: string;
  siglaPartido: string;
  uf: string;
  casa: string;
  fotoUrl: string | null;

  volumeTotalGasto: number;
  despesasAvaliadas: number;
  indiceDeRisco: number | null;
  indiceDeAltas: number | null;
  amostraSuficiente: boolean;

  flagsAltas: number;
  flagsMedias: number;
  flagsBaixas: number;

  dataUltimaVarredura: string | null;
  versaoDoMotor: number;

  serieMensal: MesDoParlamentar[];

  /** Vai **ao lado** dos achados, não em rodapé. */
  cobertura: CoberturaDeRegra[];
  regrasConclusivas: number;
  regrasSemDado: number;
}

export interface FlagDaDespesa {
  codigoRegra: string;
  gravidade: Gravidade;
  descricao: string;
  grauDeConfianca: number;

  /**
   * Distingue achado novo por *despesa nova* de achado novo por *regra alterada*. Só o
   * primeiro é notícia sobre o parlamentar.
   */
  versaoDoMotor: number;
}

export interface ItemDeDespesa {
  id: string;
  ano: number;
  mes: number;
  tipoDespesa: string;
  dataDocumento: string;
  nomeFornecedor: string | null;

  /** CNPJ inteiro; CPF sempre mascarado. A borda não decide isso — o domínio decide. */
  documentoFornecedor: string;

  valorLiquido: number;
  valorDocumento: number;

  /**
   * **Nulo quando a origem não informa.** A interface omite a quebra "declarado × glosa" por
   * inteiro — exibir `R$ 0,00` afirmaria que houve conferência e nada foi cortado.
   */
  valorGlosa: number | null;

  temDocumentoPublicado: boolean;

  /** Nulo quando a origem não publicou. Aí a interface exibe a tag, não um link quebrado. */
  urlDocumento: string | null;

  /** Devolução de valor à cota. Aparece com valor negativo, e escondê-lo inflaria o gasto. */
  ehEstorno: boolean;

  flags: FlagDaDespesa[];
  maiorGravidade: Gravidade | null;
}
