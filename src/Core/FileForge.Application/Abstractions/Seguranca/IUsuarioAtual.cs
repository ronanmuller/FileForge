using FileForge.Domain.ValueObjects;

namespace FileForge.Application.Abstractions.Seguranca;

/// <summary>
/// Quem está chamando. A Application não conhece JWT nem HttpContext: a API implementa lendo as claims
/// do token; o Worker implementa como "sistema"; os testes usam um fake.
/// </summary>
public interface IUsuarioAtual
{
    bool Autenticado { get; }

    /// <summary>Identificador estável do usuário (claim <c>sub</c>).</summary>
    string Id { get; }

    /// <summary>Cliente ao qual o usuário pertence (claim <c>cliente_id</c>). Nulo para operador/admin.</summary>
    ClienteId? ClienteId { get; }

    bool PossuiPermissao(string permissao);
}
