using FileForge.Infrastructure;
using FileForge.Infrastructure.Persistence;
using FileForge.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.IntegrationTests;

/// <summary>
/// Base dos testes de integração contra PostgreSQL. A connection string vem da variável
/// <c>FILEFORGE_TEST_CONN</c> (sem senha no código). Cada classe usa um banco próprio, informado
/// pelo construtor, para que um <c>EnsureDeleted</c> nunca apague o banco de outra classe em paralelo.
/// </summary>
public abstract class PostgresFixture(string nomeDoBanco) : IAsyncLifetime
{
    private const string _variavelConexao = "FILEFORGE_TEST_CONN";

    protected ServiceProvider Provider { get; private set; } = null!;
    protected RelogioTeste Relogio { get; } = new();

    public async Task InitializeAsync()
    {
        var conexao = ConexaoDeTeste(nomeDoBanco);
        Provider = new ServiceCollection().AddInfrastructure(conexao).BuildServiceProvider();

        var db = Provider.GetRequiredService<FileForgeDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        var db = Provider.GetRequiredService<FileForgeDbContext>();
        await db.Database.EnsureDeletedAsync();
        await Provider.DisposeAsync();
    }

    protected AsyncServiceScope Escopo() => Provider.CreateAsyncScope();

    protected async Task Salvar(Action<FileForgeDbContext> adicionar)
    {
        await using var escopo = Escopo();
        var db = escopo.ServiceProvider.GetRequiredService<FileForgeDbContext>();
        adicionar(db);
        await db.SaveChangesAsync();
    }

    private static string ConexaoDeTeste(string nomeDoBanco)
    {
        var baseConexao = Environment.GetEnvironmentVariable(_variavelConexao)
            ?? throw new InvalidOperationException(
                $"Defina a variável {_variavelConexao} com a connection string base do PostgreSQL para os testes de integração.");

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(baseConexao) { Database = nomeDoBanco };
        return builder.ConnectionString;
    }
}

public sealed class RelogioTeste : IDateTimeProvider
{
    public DateTime UtcNow => new(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    public DateOnly Hoje => DateOnly.FromDateTime(UtcNow);
}
