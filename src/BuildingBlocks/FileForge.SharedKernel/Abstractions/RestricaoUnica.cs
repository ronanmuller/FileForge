namespace FileForge.SharedKernel.Abstractions;

/// <summary>
/// Restrições de unicidade que a persistência pode reportar. Neutro: a Application compara
/// este valor, sem conhecer nomes de índice do banco. O mapeamento para o índice fica na Infrastructure.
/// </summary>
public enum RestricaoUnica
{
    Desconhecida = 0,
    SolicitacaoAtiva,
    ContaAgenciaNumero,
    ClienteDocumento
}
