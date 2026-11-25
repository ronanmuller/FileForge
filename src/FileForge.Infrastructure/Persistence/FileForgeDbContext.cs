using FileForge.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Infrastructure.Persistence;

/// <summary>
/// Contexto de escrita do domínio. Nesta etapa ele só persiste os aggregates e será a base do Unit of Work.
/// Outbox e repositórios concretos entram no próximo passo.
/// </summary>
public sealed class FileForgeDbContext(DbContextOptions<FileForgeDbContext> options) : DbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Transacao> Transacoes => Set<Transacao>();
    public DbSet<SolicitacaoArquivo> SolicitacoesArquivo => Set<SolicitacaoArquivo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FileForgeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
