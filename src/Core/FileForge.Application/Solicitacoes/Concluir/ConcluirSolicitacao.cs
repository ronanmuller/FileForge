using FileForge.Application.Solicitacoes.Obter;
using FileForge.Domain.Interfaces;
using FileForge.Domain.ValueObjects;
using FileForge.Mediator;
using FileForge.SharedKernel.Abstractions;
using FileForge.SharedKernel.Results;

namespace FileForge.Application.Solicitacoes.Concluir;

/// <summary>
/// Chamado pelo Worker quando o extrato foi gerado. Sem <c>RequerPermissao</c>: não é ação de usuário,
/// e o Worker não recebe o token do usuário (ver <c>SolicitadoPor</c> no aggregate).
/// </summary>
public sealed record ConcluirSolicitacaoCommand(Guid Id, string NomeArquivo) : IRequest<Result>;

public sealed class ConcluirSolicitacaoHandler(
    ISolicitacaoArquivoRepository repositorio,
    IUnitOfWork unitOfWork,
    IRegistradorDeEventos registrador,
    IDateTimeProvider relogio) : IRequestHandler<ConcluirSolicitacaoCommand, Result>
{
    public async Task<Result> Handle(ConcluirSolicitacaoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var solicitacao = await repositorio
            .ObterPorIdAsync(new SolicitacaoArquivoId(request.Id), cancellationToken)
            .ConfigureAwait(false);

        if (solicitacao is null)
            return ErrosDeSolicitacao.NaoEncontrada;

        var resultado = solicitacao.Concluir(request.NomeArquivo, relogio);
        if (resultado.IsFailure)
            return resultado;

        // Gera o ArquivoGerado (evento de integração) e grava no Outbox na mesma transação do status.
        registrador.RegistrarEventos();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
