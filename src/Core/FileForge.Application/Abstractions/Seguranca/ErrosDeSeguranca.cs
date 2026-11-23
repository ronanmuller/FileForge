using FileForge.SharedKernel.Results;

namespace FileForge.Application.Abstractions.Seguranca;

public static class ErrosDeSeguranca
{
    public static readonly Error NaoAutenticado =
        Error.Unauthorized("Seguranca.NaoAutenticado", "É necessário estar autenticado.");

    public static Error SemPermissao(string permissao) =>
        Error.Forbidden("Seguranca.SemPermissao", $"Permissão necessária: {permissao}.");

    public static readonly Error UsuarioSemCliente =
        Error.Forbidden("Seguranca.UsuarioSemCliente", "O usuário não está vinculado a um cliente.");
}
