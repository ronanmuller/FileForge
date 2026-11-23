using FileForge.Domain.Erros;
using FileForge.Domain.Solicitacoes;
using FileForge.Domain.ValueObjects;
using FileForge.Mediator;
using FileForge.SharedKernel.Abstractions;
using FileForge.SharedKernel.Results;

namespace FileForge.IntegrationTests.Fluxo;

// Protótipo do caso de uso que será implementado de verdade na Etapa 8 (Application).
// Aqui ele existe só para exercitar Mediator + Behaviors + Domain juntos.

public sealed record CriarSolicitacaoCommand(Guid ClienteId, DateOnly DataReferencia) : IRequest<Result<Guid>>;

public sealed class CriarSolicitacaoHandler(
    RepositorioSolicitacoesEmMemoria repositorio,
    IDateTimeProvider relogio) : IRequestHandler<CriarSolicitacaoCommand, Result<Guid>>
{
    public Task<Result<Guid>> Handle(CriarSolicitacaoCommand request, CancellationToken cancellationToken)
    {
        var clienteId = ClienteId.Criar(request.ClienteId);
        if (clienteId.IsFailure)
            return Task.FromResult<Result<Guid>>(clienteId.Error);

        var data = DataReferencia.Criar(request.DataReferencia, relogio);
        if (data.IsFailure)
            return Task.FromResult<Result<Guid>>(data.Error);

        // Regra entre aggregates: fica na Application, não dentro do aggregate.
        if (repositorio.ExisteAtiva(clienteId.Value, data.Value))
            return Task.FromResult<Result<Guid>>(DomainErrors.SolicitacaoArquivo.JaExisteAtiva);

        var solicitacao = SolicitacaoArquivo.Criar(clienteId.Value, data.Value, "usuario-prototipo", relogio);
        repositorio.Adicionar(solicitacao);

        return Task.FromResult<Result<Guid>>(solicitacao.Id.Value);
    }
}

/// <summary>Repositório fake: guarda em memória (o real será EF Core na Etapa 9).</summary>
public sealed class RepositorioSolicitacoesEmMemoria
{
    private readonly List<SolicitacaoArquivo> _itens = [];

    public IReadOnlyList<SolicitacaoArquivo> Itens => _itens;

    public bool ExisteAtiva(ClienteId clienteId, DataReferencia data) =>
        _itens.Any(s => s.ClienteId == clienteId && s.DataReferencia == data && s.Ativa);

    public void Adicionar(SolicitacaoArquivo solicitacao) => _itens.Add(solicitacao);
}

/// <summary>Behavior que registra a ordem de execução, para visualizar a "cebola" do pipeline.</summary>
public sealed class RastreamentoBehavior<TRequest, TResponse>(Rastro rastro) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        rastro.Passos.Add($"antes:{typeof(TRequest).Name}");
        var response = await next();
        rastro.Passos.Add($"depois:{typeof(TRequest).Name}");
        return response;
    }
}

public sealed class Rastro
{
    public List<string> Passos { get; } = [];
}

/// <summary>Ponte Domain → Mediator: o evento de domínio é embrulhado numa notificação.</summary>
public sealed record DomainEventNotification(FileForge.SharedKernel.Domain.IDomainEvent Evento) : INotification;

public sealed class AuditoriaHandler(Rastro rastro) : INotificationHandler<DomainEventNotification>
{
    public Task Handle(DomainEventNotification notification, CancellationToken cancellationToken)
    {
        rastro.Passos.Add($"evento:{notification.Evento.GetType().Name}");
        return Task.CompletedTask;
    }
}
