using System.Reflection;
using Microsoft.Extensions.Logging;

namespace FileForge.ArchitectureTests;

/// <summary>
/// Cada camada tem o seu catálogo de logs (<c>LogDefinitions</c>, internal) e uma faixa de EventId.
/// Estes testes impedem EventId repetido (alertas e filtros ficariam ambíguos) e EventId fora da faixa da camada.
/// Ao criar o catálogo de uma camada nova (Infrastructure, Worker, API), basta incluí-la em <see cref="Faixas"/>.
/// </summary>
public class LogDefinitionsTests
{
    private static readonly (Assembly Assembly, int Min, int Max)[] Faixas =
    [
        (typeof(FileForge.Application.DependencyInjection).Assembly, 1000, 1999),
    ];

    private static IEnumerable<(Assembly Assembly, string Metodo, int EventId)> Logs() =>
        from faixa in Faixas
        from tipo in faixa.Assembly.GetTypes()
        from metodo in tipo.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        let atributo = metodo.GetCustomAttribute<LoggerMessageAttribute>()
        where atributo is not null
        select (faixa.Assembly, $"{tipo.Name}.{metodo.Name}", atributo.EventId);

    [Fact]
    public void Catalogos_TemLogs()
    {
        foreach (var (assembly, _, _) in Faixas)
            Assert.Contains(Logs(), l => l.Assembly == assembly);
    }

    [Fact]
    public void EventIds_SaoUnicosNaSolucao()
    {
        var repetidos = Logs()
            .GroupBy(l => l.EventId)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(l => l.Metodo))}")
            .ToList();

        Assert.True(repetidos.Count == 0, "EventId repetido: " + string.Join(" | ", repetidos));
    }

    [Fact]
    public void EventIds_EstaoNaFaixaDaCamada()
    {
        var foraDaFaixa = Faixas
            .SelectMany(f => Logs()
                .Where(l => l.Assembly == f.Assembly && (l.EventId < f.Min || l.EventId > f.Max))
                .Select(l => $"{l.Metodo}={l.EventId} (faixa {f.Min}-{f.Max})"))
            .ToList();

        Assert.True(foraDaFaixa.Count == 0, "EventId fora da faixa: " + string.Join(", ", foraDaFaixa));
    }

    [Fact]
    public void Catalogos_SaoInternos()
    {
        var publicos = Faixas
            .SelectMany(f => f.Assembly.GetTypes())
            .Where(t => t.Name == "LogDefinitions" && t.IsPublic)
            .Select(t => t.FullName)
            .ToList();

        Assert.True(publicos.Count == 0, "LogDefinitions deve ser internal: " + string.Join(", ", publicos));
    }
}
