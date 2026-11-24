using FileForge.Application.Abstractions.Seguranca;
using FileForge.Application.Logging;
using FileForge.Domain.Events;
using FileForge.Domain.Interfaces;
using FileForge.Domain.Models;
using FileForge.Domain.Specifications;
using FileForge.Domain.ValueObjects;
using FileForge.Mediator;
using FileForge.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace FileForge.Application.Solicitacoes.Obter;

[RequerPermissao(Permissoes.ConsultarExtrato)]
public sealed record ObterSolicitacaoQuery(Guid Id) : IRequest<Result<SolicitacaoResponse>>;

/// <summary>DTO de saída: a API nunca expõe o aggregate (evita vazar estado interno e acoplar o contrato).</summary>
public sealed record SolicitacaoResponse(
    Guid Id,
    Guid ClienteId,
    DateOnly DataReferencia,
    string Status,
    int Tentativas,
    string? NomeArquivo,
    string? MotivoFalha,
    DateTime CriadaEmUtc,
    DateTime? AtualizadaEmUtc)
{
    internal static SolicitacaoResponse De(SolicitacaoArquivo s) => new(
        s.Id.Value, s.ClienteId.Value, s.DataReferencia.Valor, s.Status.ToString(),
        s.Tentativas, s.NomeArquivo, s.MotivoFalha, s.CriadaEmUtc, s.AtualizadaEmUtc);
}

public static class ErrosDeSolicitacao
{
    public static readonly Error NaoEncontrada =
        Error.NotFound("SolicitacaoArquivo.NaoEncontrada", "Solicitação não encontrada.");
}

public sealed class ObterSolicitacaoHandler(
    ISolicitacaoArquivoRepository repositorio,
    IUsuarioAtual usuario,
    ILogger<ObterSolicitacaoHandler> logger) : IRequestHandler<ObterSolicitacaoQuery, Result<SolicitacaoResponse>>
{
    public async Task<Result<SolicitacaoResponse>> Handle(ObterSolicitacaoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var solicitacao = await repositorio
            .ObterPorIdAsync(new SolicitacaoArquivoId(request.Id), cancellationToken)
            .ConfigureAwait(false);

        if (solicitacao is null)
            return ErrosDeSolicitacao.NaoEncontrada;

        // Autorização por RECURSO (anti-IDOR): sem "consultar-todos", só vê as do próprio cliente.
        // Responde 404 (e não 403) para não revelar que o id existe.
        if (!usuario.PossuiPermissao(Permissoes.ConsultarTodos) && usuario.ClienteId != solicitacao.ClienteId)
        {
            logger.AcessoNegadoARecurso(usuario.Id, request.Id);
            return ErrosDeSolicitacao.NaoEncontrada;
        }

        return SolicitacaoResponse.De(solicitacao);
    }
}
