namespace FileForge.Application.Abstractions.Seguranca;

/// <summary>
/// Catálogo de permissões. Os valores são os mesmos que vêm no JWT (claim <c>permissions</c>/roles mapeadas),
/// então um nome errado aqui quebra a autorização — por isso ficam centralizados.
/// </summary>
public static class Permissoes
{
    public const string SolicitarExtrato = "extratos:solicitar";
    public const string ConsultarExtrato = "extratos:consultar";

    /// <summary>Vê solicitações de qualquer cliente (operador/admin). Sem ela, só as do próprio cliente.</summary>
    public const string ConsultarTodos = "extratos:consultar-todos";

    public const string Reprocessar = "extratos:reprocessar";

    /// <summary>Ligar simulação de falhas no Worker, republicar DLQ etc. Só em desenvolvimento.</summary>
    public const string Simular = "admin:simular";

    public static IReadOnlyList<string> Todas { get; } =
        [SolicitarExtrato, ConsultarExtrato, ConsultarTodos, Reprocessar, Simular];
}

/// <summary>Declara a permissão exigida por um command/query. Verificada pelo <c>AuthorizationBehavior</c>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RequerPermissaoAttribute(string permissao) : Attribute
{
    public string Permissao { get; } = permissao;
}
