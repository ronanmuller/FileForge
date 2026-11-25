using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FileForge.Infrastructure.Persistence;

/// <summary>
/// Usada somente pelas ferramentas de design-time (<c>dotnet ef migrations add</c>).
/// Usa a connection string do banco local do <c>docker-compose.yml</c>. A variável
/// <c>FILEFORGE_DESIGN_CONN</c> é opcional e sobrescreve o padrão, se existir.
/// </summary>
internal sealed class FileForgeDbContextFactory : IDesignTimeDbContextFactory<FileForgeDbContext>
{
    private const string _variavelConexao = "FILEFORGE_DESIGN_CONN";
    private const string _conexaoLocal =
        "Host=localhost;Port=5432;Database=fileforge;Username=fileforge;Password=fileforge";

    public FileForgeDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable(_variavelConexao) ?? _conexaoLocal;

        var options = new DbContextOptionsBuilder<FileForgeDbContext>()
            .UseNpgsql(conexao)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new FileForgeDbContext(options);
    }
}
