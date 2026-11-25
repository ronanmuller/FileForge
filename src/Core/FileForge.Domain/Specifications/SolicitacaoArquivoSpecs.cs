using System.Linq.Expressions;
using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;

namespace FileForge.Domain.Specifications;

/// <summary>
/// Regras de consulta como <see cref="Expression"/>: a mesma regra serve para filtrar em memória
/// (<c>Compile()</c>) e para virar SQL no EF Core. Evita que <see cref="SolicitacaoArquivo.Ativa"/>
/// (propriedade calculada, que o EF não traduz) seja reescrita de outro jeito na Infrastructure.
/// </summary>
public static class SolicitacaoArquivoSpecs
{
    public static Expression<Func<SolicitacaoArquivo, bool>> Ativa =>
        s => SolicitacaoArquivo.StatusAtivos.Contains(s.Status);

    public static Expression<Func<SolicitacaoArquivo, bool>> AtivaDoClienteNaData(ClienteId clienteId, DataReferencia data) =>
        s => s.ClienteId == clienteId
             && s.DataReferencia == data
             && SolicitacaoArquivo.StatusAtivos.Contains(s.Status);
}
