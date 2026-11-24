using FileForge.Application.Abstractions.Seguranca;
using FileForge.Application.Solicitacoes.Obter;
using FileForge.Domain.Interfaces;
using FileForge.Domain.ValueObjects;
using FileForge.Mediator;
using FileForge.SharedKernel.Abstractions;
using FileForge.SharedKernel.Results;

namespace FileForge.Application.Solicitacoes.Reprocessar;

/// <summary>Operador/admin devolve uma solicitação <c>Falhou</c> para <c>Pendente</c>.</summary>
[RequerPermissao(Permissoes.Reprocessar)]
public sealed record ReprocessarSolicitacaoCommand(Guid Id) : IRequest<Result>;

public sealed class ReprocessarSolicitacaoHandler(
    ISolicitacaoArquivoRepository repositorio,
    IUnitOfWork unitOfWork,
    IDateTimeProvider relogio) : IRequestHandler<ReprocessarSolicitacaoCommand, Result>
{
    public async Task<Result> Handle(ReprocessarSolicitacaoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var solicitacao = await repositorio
            .ObterPorIdAsync(new SolicitacaoArquivoId(request.Id), cancellationToken)
            .ConfigureAwait(false);

        if (solicitacao is null)
            return ErrosDeSolicitacao.NaoEncontrada;

        var resultado = solicitacao.Reprocessar(relogio);
        if (resultado.IsFailure)
            return resultado;

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
