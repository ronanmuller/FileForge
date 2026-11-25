using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FileForge.Infrastructure.Persistence;

/// <summary>
/// Usada somente pelas ferramentas de design-time (<c>dotnet ef migrations add</c>).
/// Essas ferramentas precisam criar o DbContext sem executar a API, então esta fábrica
/// fornece uma connection string local apenas para gerar as migrations.
/// Em tempo de execução, quem configura o banco é a API ou o Worker via <c>AddInfrastructure</c>.
/// </summary>
internal sealed class FileForgeDbContextFactory : IDesignTimeDbContextFactory<FileForgeDbContext>
{
    private const string _connectionStringDesignTime =
        "Host=localhost;Port=5432;Database=fileforge;Username=fileforge;Password=fileforge";

    public FileForgeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FileForgeDbContext>()
            .UseNpgsql(_connectionStringDesignTime)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new FileForgeDbContext(options);
    }
}
