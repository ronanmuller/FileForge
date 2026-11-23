namespace FileForge.SharedKernel.Abstractions;

/// <summary>Abstrai o relógio do sistema para permitir testes determinísticos.</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
