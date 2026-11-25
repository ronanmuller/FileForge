namespace FileForge.SharedKernel.Abstractions;

/// <summary>
/// Violação de unicidade detectada pela persistência. A Infrastructure a lança ao traduzir o erro do banco;
/// a Application a converte em erro de negócio, sem conhecer o provedor.
/// </summary>
public sealed class UnicidadeVioladaException(string restricao, Exception? interna = null)
    : Exception($"Violação de unicidade: {restricao}.", interna)
{
    public string Restricao { get; } = restricao;
}
