using FileForge.Application.Abstractions.Seguranca;
using FileForge.Domain.Events;
using FileForge.Domain.Interfaces;
using FileForge.Domain.Models;
using FileForge.Domain.Specifications;
using FileForge.Domain.ValueObjects;
using FileForge.SharedKernel.Abstractions;

namespace FileForge.Application.UnitTests.Fakes;

/// <summary>Usuário controlável. Use os atalhos <see cref="Ana"/>, <see cref="Bruno"/>, <see cref="Operador"/>, <see cref="Anonimo"/>.</summary>
internal sealed class UsuarioFake : IUsuarioAtual
{
    public static readonly ClienteId ClienteDaAna = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
    public static readonly ClienteId ClienteDoBruno = new(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"));

    private readonly HashSet<string> _permissoes;

    public UsuarioFake(string id, ClienteId? clienteId, bool autenticado = true, params string[] permissoes)
    {
        Id = id;
        ClienteId = clienteId;
        Autenticado = autenticado;
        _permissoes = [.. permissoes];
    }

    public bool Autenticado { get; }
    public string Id { get; }
    public ClienteId? ClienteId { get; }
    public bool PossuiPermissao(string permissao) => _permissoes.Contains(permissao);

    public static UsuarioFake Ana() => new("ana", ClienteDaAna, true, Permissoes.SolicitarExtrato, Permissoes.ConsultarExtrato);
    public static UsuarioFake Bruno() => new("bruno", ClienteDoBruno, true, Permissoes.SolicitarExtrato, Permissoes.ConsultarExtrato);
    public static UsuarioFake Operador() => new("operador", null, true,
        Permissoes.ConsultarExtrato, Permissoes.ConsultarTodos, Permissoes.Reprocessar);
    public static UsuarioFake Anonimo() => new("", null, autenticado: false);
}

internal sealed class RelogioFixo : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    public DateOnly Hoje => DateOnly.FromDateTime(UtcNow);
}

/// <summary>Repositório em memória que usa a MESMA specification que o EF usará (garante que a regra é única).</summary>
internal sealed class SolicitacaoRepositorioFake : ISolicitacaoArquivoRepository
{
    public List<SolicitacaoArquivo> Itens { get; } = [];

    public Task<SolicitacaoArquivo?> ObterPorIdAsync(SolicitacaoArquivoId id, CancellationToken cancellationToken) =>
        Task.FromResult(Itens.FirstOrDefault(s => s.Id == id));

    public Task<bool> ExisteAtivaAsync(ClienteId clienteId, DataReferencia data, CancellationToken cancellationToken) =>
        Task.FromResult(Itens.AsQueryable().Any(SolicitacaoArquivoSpecs.AtivaDoClienteNaData(clienteId, data)));

    public void Adicionar(SolicitacaoArquivo solicitacao) => Itens.Add(solicitacao);
}

/// <summary>Simula a conclusão sem Outbox: a Application só conhece o contrato, a Infrastructure grava de fato.</summary>
internal sealed class RegistradorDeEventosFake : IRegistradorDeEventos
{
    public int Registros { get; private set; }

    public void RegistrarEventos() => Registros++;
}

internal sealed class UnitOfWorkFake : IUnitOfWork
{
    public int Commits { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Commits++;
        return Task.FromResult(1);
    }
}

/// <summary>Simula a corrida: o commit é recusado pelo índice único, como o banco faria.</summary>
internal sealed class UnitOfWorkQueViolaUnicidade(RestricaoUnica restricao) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.FromException<int>(new UnicidadeVioladaException(restricao));
}
