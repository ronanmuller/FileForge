namespace FileForge.SharedKernel.Abstractions;

/// <summary>Abstrai o relógio do sistema para permitir testes determinísticos.</summary>
public interface IDateTimeProvider
{
    /// <summary>Instante atual em UTC (para gravar "quando aconteceu").</summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Data de hoje no fuso horário do negócio (para regras de calendário, ex.: "não pode ser no futuro").
    /// Não use <c>DateOnly.FromDateTime(UtcNow)</c>: às 22h em São Paulo, em UTC já é o dia seguinte.
    /// </summary>
    DateOnly Hoje { get; }
}
