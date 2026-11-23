using FileForge.Application.Abstractions.Seguranca;
using FileForge.Domain.Erros;
using FileForge.Domain.Solicitacoes;
using FileForge.Domain.ValueObjects;
using FileForge.Mediator;
using FileForge.SharedKernel.Abstractions;
using FileForge.SharedKernel.Results;
using FluentValidation;

namespace FileForge.Application.Solicitacoes.Criar;

/// <summary>
/// Solicita a geração do extrato. Repare: <b>não</b> há <c>ClienteId</c> no command — ele vem do token
/// (<see cref="IUsuarioAtual.ClienteId"/>). Assim a Ana não consegue pedir o extrato do Bruno trocando um campo do JSON.
/// </summary>
[RequerPermissao(Permissoes.SolicitarExtrato)]
public sealed record CriarSolicitacaoCommand(DateOnly DataReferencia) : IRequest<Result<Guid>>;

public sealed class CriarSolicitacaoValidator : AbstractValidator<CriarSolicitacaoCommand>
{
    public CriarSolicitacaoValidator() =>
        RuleFor(c => c.DataReferencia)
            .NotEmpty().WithErrorCode("Obrigatoria").WithMessage("Informe a data de referência.");
}

public sealed class CriarSolicitacaoHandler(
    ISolicitacaoArquivoRepository repositorio,
    IUnitOfWork unitOfWork,
    IUsuarioAtual usuario,
    IDateTimeProvider relogio) : IRequestHandler<CriarSolicitacaoCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CriarSolicitacaoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (usuario.ClienteId is not { } clienteId)
            return ErrosDeSeguranca.UsuarioSemCliente;

        var data = DataReferencia.Criar(request.DataReferencia, relogio);
        if (data.IsFailure)
            return data.Error;

        // Regra entre aggregates: fica na Application (+ índice único no banco na etapa 9).
        if (await repositorio.ExisteAtivaAsync(clienteId, data.Value, cancellationToken).ConfigureAwait(false))
            return DomainErrors.SolicitacaoArquivo.JaExisteAtiva;

        var solicitacao = SolicitacaoArquivo.Criar(clienteId, data.Value, usuario.Id, relogio);
        repositorio.Adicionar(solicitacao);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return solicitacao.Id.Value;
    }
}
