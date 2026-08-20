namespace Explanando.Domain.Regras;

/// <summary>
/// Periodo de recesso parlamentar, insumo de REG_GEO_003.
///
/// Diferente dos limiares de <see cref="ParametrosDoMotor"/>, isto <b>nao e' calibragem</b>:
/// o art. 57 da Constituicao fixa a sessao legislativa de 2 de fevereiro a 17 de julho e de
/// 1º de agosto a 22 de dezembro. Recesso e' o complemento — 18 a 31 de julho, e 23 de
/// dezembro a 1º de fevereiro.
///
/// Convocacao extraordinaria pode fazer o Congresso funcionar em recesso. Por isso a regra
/// que usa este calendario e' de gravidade baixa: recesso torna o gasto <b>atipico</b>, nao
/// indevido.
/// </summary>
public static class CalendarioLegislativo
{
    public static bool EmRecesso(DateOnly data)
    {
        // Recesso de meio de ano: 18 a 31 de julho.
        if (data.Month == 7 && data.Day >= 18)
        {
            return true;
        }

        // Recesso de fim de ano: 23 a 31 de dezembro.
        if (data.Month == 12 && data.Day >= 23)
        {
            return true;
        }

        // Continuacao do recesso de fim de ano: janeiro inteiro e 1º de fevereiro.
        return data.Month == 1 || (data.Month == 2 && data.Day == 1);
    }

    public static bool EmRecesso(DateTimeOffset data) =>
        EmRecesso(DateOnly.FromDateTime(data.UtcDateTime));
}
