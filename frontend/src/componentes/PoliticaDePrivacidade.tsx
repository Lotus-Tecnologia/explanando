import { ENCARREGADO } from "@/lib/site";

/**
 * A Política de Privacidade.
 *
 * <b>Nota sobre o vocabulário do ADR-0004:</b> a palavra "fraude" aparece aqui de propósito,
 * em "prevenção à fraude". Ela é o termo da própria LGPD para a finalidade que ampara o
 * legítimo interesse do art. 7º, IX — não é imputação sobre um parlamentar nomeado, que é o
 * que o ADR-0004 existe para impedir. É a mesma situação de "desvio padrão", que o documento
 * de Negócios lista como termo *obrigatório* contendo um termo *proibido*. Não "corrija" isto
 * para outra palavra: mudaria a base legal declarada.
 */

function Secao({ titulo, children }: { titulo: string; children: React.ReactNode }) {
  return (
    <section className="mt-8">
      <h2 className="text-base font-bold text-titulo">{titulo}</h2>
      <div className="mt-2 space-y-3 text-sm text-texto">{children}</div>
    </section>
  );
}

export function PoliticaDePrivacidade() {
  return (
    <article className="mx-auto max-w-3xl px-4 py-8">
      <h1 className="text-2xl font-bold text-titulo">Privacidade e Direitos LGPD</h1>

      <p className="mt-2 text-sm text-texto-suave">
        Como o Explanando trata dado pessoal, sob que base legal, e o que você pode exigir.
      </p>

      <Secao titulo="O que esta plataforma faz">
        <p>
          O Explanando lê registros públicos de gastos da cota parlamentar — publicados pela
          Câmara dos Deputados e pelo Senado Federal — e os cruza com o cadastro público de
          empresas da Receita Federal, obtido via BrasilAPI. Sobre esse cruzamento roda um
          motor de regras determinístico, que aponta inconsistências entre registros.
        </p>
        <p>
          <strong>
            Os indicadores descrevem inconsistências e não caracterizam, por si,
            irregularidade.
          </strong>{" "}
          Cada achado é identificado pelo código da regra que o produziu, com link para o
          documento oficial quando a origem o publica. A conclusão é de quem lê.
        </p>
      </Secao>

      <Secao titulo="Que dados pessoais são tratados">
        <p>
          <strong>De parlamentares:</strong> nome civil, nome parlamentar, partido, unidade
          federativa, casa legislativa, foto oficial e os gastos declarados. São agentes
          públicos, e esses dados já são publicados pelas próprias casas.
        </p>
        <p>
          <strong>De sócios de empresas fornecedoras (QSA):</strong> nome completo,
          qualificação societária e data de entrada na sociedade. Aqui há pessoas físicas que{" "}
          <strong>não</strong> são agentes públicos, e é por causa delas que esta política
          existe.
        </p>
        <p>
          O CPF do sócio chega <strong>mascarado da própria fonte</strong> — a Receita expõe
          apenas seis dos onze dígitos, e o número completo nunca entra neste sistema.
        </p>
      </Secao>

      <Secao titulo="O que não é coletado">
        <p>
          Necessidade também se prova pelo que falta. Estes dados são oferecidos pelas fontes
          e <strong>não são coletados nem armazenados</strong>, por não alimentarem nenhuma
          regra (LGPD, art. 6º, III):
        </p>
        <ul className="ml-5 list-disc space-y-1">
          <li>o CPF do parlamentar, que a API da Câmara entrega completo;</li>
          <li>data e município de nascimento;</li>
          <li>perfis de redes sociais;</li>
          <li>a faixa etária do sócio.</li>
        </ul>
        <p className="text-texto-suave">
          A decisão é de projeto, não de configuração: esses campos não são sequer
          desserializados, e há teste automatizado que falha se alguém os acrescentar.
        </p>
      </Secao>

      <Secao titulo="Sob que base legal">
        <p>
          O tratamento é amparado pelo <strong>legítimo interesse</strong> —{" "}
          <strong>art. 7º, IX</strong> da Lei nº 13.709/2018 —, com as finalidades de{" "}
          <strong>prevenção à fraude</strong> e de <strong>controle social</strong> do uso de
          recursos públicos.
        </p>
        <p>
          A Lei de Acesso à Informação entra como fundamento do interesse público dentro dessa
          avaliação, e não como base legal em si: ela ampara <em>obter</em> a informação
          pública, não <em>tratar</em> dado pessoal de terceiros.
        </p>
      </Secao>

      <Secao titulo="Seus direitos, e em quanto tempo respondemos">
        <p>
          Você pode pedir confirmação de tratamento, acesso, correção de dado incorreto,
          anonimização ou bloqueio, oposição ao tratamento, e desindexação em caso de
          homônimo.
        </p>
        <p>
          <strong>Prazo de resposta: 15 dias úteis</strong>, contados do recebimento do
          pedido.
        </p>
      </Secao>

      <Secao titulo="Encarregado de Dados">
        <p>
          O art. 41, § 1º exige que a identidade e o contato do Encarregado sejam divulgados
          publicamente — só o endereço de e-mail não cumpre.
        </p>
        <p>
          <strong>Gustavo Lira Criado</strong> —{" "}
          <a
            href={`mailto:${ENCARREGADO.email}`}
            className="numeros text-titulo underline underline-offset-2"
          >
            {ENCARREGADO.email}
          </a>
        </p>
      </Secao>

      <Secao titulo="Contestação de parlamentares">
        <p>
          Gabinetes podem responder publicamente a qualquer indicador. A resposta oficial é
          exibida ao lado do achado, com identificação de origem. O acesso é verificado por
          posse da caixa institucional (<span className="numeros">@camara.leg.br</span> ou{" "}
          <span className="numeros">@senado.leg.br</span>), por código numérico enviado àquele
          endereço.
        </p>
      </Secao>
    </article>
  );
}
