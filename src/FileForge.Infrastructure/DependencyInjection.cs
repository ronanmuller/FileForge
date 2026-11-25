using FileForge.Domain.Interfaces;
using FileForge.Infrastructure.Persistence;
using FileForge.Infrastructure.Persistence.Repositories;
using FileForge.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Infrastructure;

/// <summary>
/// Registro da camada Infrastructure: DbContext (PostgreSQL) e UnitOfWork.
/// Repositórios e Outbox entram no próximo passo.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<FileForgeDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IDateTimeProvider, RelogioDoSistema>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<ITransacaoRepository, TransacaoRepository>();
        services.AddScoped<ISolicitacaoArquivoRepository, SolicitacaoArquivoRepository>();
        services.AddScoped<IContaRepository, ContaRepository>();

        return services;
    }
}
