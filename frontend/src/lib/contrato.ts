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
  /**
   * A casa por extenso — "Câmara dos Deputados" ou "Senado Federal".
   *
   * Redundante com a rota que pediu o ranking, e é assim de propósito: uma linha que circule
   * fora do contexto, num card compartilhado ou numa exportação, precisa dizer sozinha de
   * que casa ela fala. As cotas são distintas e os números não se comparam entre casas.
   */
  casa: string;

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
  /**
   * Identificador estável da metodologia — `REG_TX_002`.
   *
   * **Não é para a tela.** Serve a quem integra a API: o banco em due diligence e a redação
   * investigativa citam a regra por ele. Quem desenha usa {@link descricao}.
   */
  codigo: string;

  /**
   * O que a regra procura, em português de leitor.
   *
   * Vem do catálogo no domínio, e não de uma tabela aqui, porque cópia na borda envelhece
   * em silêncio — e porque cada consumidor da API acabaria redigindo a sua, que é como
   * "nada irregular encontrado" apareceria em algum lugar.
   *
   * Nulo só se o código não estiver no catálogo, o que um teste de deriva impede. Quando
   * for nulo, a linha cai para o código: o bloco de cobertura existe para ser completo, e
   * regra ausente da lista é lida como regra que ninguém considerou.
   */
  descricao: string | null;

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

  /**
   * Os tipos que este parlamentar de fato teve — as opções da barra de filtros.
   *
   * Vem da API, e não de uma lista fixa aqui, porque o catálogo muda por Ato da Mesa e uma
   * cópia no frontend envelheceria em silêncio. E porque uma opção que devolve lista vazia
   * faz o leitor concluir que não houve gasto.
   */
  tiposDeDespesa: string[];
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

/** Quem gastou. O mínimo que a página de uma despesa precisa dizer. */
export interface ParlamentarDaDespesa {
  id: string;
  nomeParlamentar: string;
  siglaPartido: string;
  uf: string;

  /**
   * A casa por extenso. Decide a frase da tag de comprovante ausente: o ADR-0012 §1 exige
   * atribuir a ausência ao Senado **nominalmente**, e não a "a fonte".
   */
  casa: string;

  fotoUrl: string | null;
}

/** Uma despesa com o parlamentar a que ela pertence. É o que o endereço público dela devolve. */
export interface DespesaDetalhada {
  despesa: ItemDeDespesa;
  parlamentar: ParlamentarDaDespesa;
}

// ---------------------------------------------------------------------------
//  O grafo do Estado
//
//  Contexto novo: aqui não há parlamentar, cota nem indicador. A pergunta é "que órgãos
//  existem, de quem cada um depende, sob que norma — e o que mudou".
// ---------------------------------------------------------------------------

/** Um nó do grafo. Os rótulos já vêm por extenso da API, nunca como código de enum. */
export interface NoDoEstado {
  codigo: number;

  /** Quem manda na unidade na estrutura real. */
  codigoPai: number | null;

  /**
   * O ancestral mais próximo que **também está no mapa**.
   *
   * Só vem no mapa. Existe porque oito órgãos penduram de uma unidade administrativa — os
   * comandos militares ficam entre o Ministério da Defesa e os órgãos abaixo deles —, e a
   * unidade administrativa não entra no desenho. Sem este campo, esses oito virariam
   * raízes soltas e sumiriam.
   */
  codigoPaiNoMapa?: number | null;
  nome: string;
  sigla: string | null;
  tipo: string;
  poder: string | null;
  natureza: string | null;

  /**
   * "Criada por lei ou decreto" contra "Criada por ato interno".
   *
   * Não é metadado: é a diferença entre um órgão que precisa do Congresso para desaparecer
   * e um que some com uma assinatura. `null` quando a fonte não informa.
   */
  baseLegal: string | null;

  /** Quantas unidades pendem deste nó, em qualquer profundidade. Dá escala ao desenho. */
  unidadesSubordinadas: number;
  ativa: boolean;

  /**
   * Servidores lotados **nesta** unidade, ou `null`.
   *
   * `null` e `0` dizem coisas opostas, e e' o ADR-0009 de novo: `null` e' "nao conseguimos
   * ligar esta unidade ao cadastro de pessoal", `0` e' "ligamos, e nao ha' ninguem".
   */
  servidores: number | null;

  /** Quantos deles ocupam funcao de confianca (DAS, FCE, FG...). */
  emFuncaoDeConfianca: number | null;

  /** Servidores em todo o ramo. E' o numero que da' sentido a um ministerio. */
  servidoresNoRamo: number | null;
}

export interface ContagemDoEstado {
  rotulo: string;
  unidades: number;
}

export interface MudancaDoEstado {
  codigoUnidade: number;
  unidade: string;
  descricao: string;
  de: string | null;
  para: string | null;
  detectadaEm: string;
}

export interface MapaDoEstado {
  nos: NoDoEstado[];
  totalDeUnidades: number;
  porPoder: ContagemDoEstado[];
  porTipo: ContagemDoEstado[];
  porBaseLegal: ContagemDoEstado[];
  ultimaColeta: string | null;

  /** O mes do quadro de pessoal, como "2026-07". Outra fonte, outra cadencia. */
  competenciaDoPessoal: string | null;

  /** Total de vinculos no mes, incluindo os que nao chegaram a no' nenhum. */
  totalDeServidores: number | null;

  /** A fracao dos vinculos que a ponte ligou ao grafo. Vai para a tela, nao so' para o log. */
  coberturaDoPessoal: number | null;

  /**
   * Se já houve uma segunda coleta.
   *
   * Sem isto, "nenhuma mudança" seria lido como "nada mudou" quando significa "ainda não
   * comparamos" — os dois zeros diferentes que o ADR-0009 proíbe confundir.
   */
  temHistorico: boolean;
}
