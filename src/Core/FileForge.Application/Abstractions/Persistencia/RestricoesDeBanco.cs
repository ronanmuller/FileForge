namespace FileForge.Application.Abstractions.Persistencia;

/// <summary>
/// Nomes das restrições de unicidade do banco. Espelham os índices criados na Infrastructure
/// (<c>ux_solicitacao_ativa</c>, <c>ux_contas_agencia_numero</c>, <c>ux_clientes_documento</c>).
/// </summary>
public static class RestricoesDeBanco
{
    public const string SolicitacaoAtiva = "ux_solicitacao_ativa";
    public const string ContaAgenciaNumero = "ux_contas_agencia_numero";
    public const string ClienteDocumento = "ux_clientes_documento";
}
