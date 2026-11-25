using FileForge.Domain.Models;
using FileForge.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Infrastructure.Persistence;

/// <summary>
/// Contexto de escrita do domínio. Persiste os aggregates e as mensagens do Outbox na mesma transação.
/// </summary>
public sealed class FileForgeDbContext(DbContextOptions<FileForgeDbContext> options) : DbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Conta> Contas => Set<Conta>();
    public DbSet<Transacao> Transacoes => Set<Transacao>();
    public DbSet<SolicitacaoArquivo> SolicitacoesArquivo => Set<SolicitacaoArquivo>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FileForgeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
