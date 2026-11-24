using FileForge.Domain.Events;
using FileForge.Domain.Models;
using FileForge.Domain.Specifications;
using FileForge.Domain.ValueObjects;

namespace FileForge.Domain.Interfaces;

/// <summary>Repositório do aggregate <see cref="Cliente"/> (as <see cref="Conta"/>s vêm junto: são parte do aggregate).</summary>
public interface IClienteRepository
{
    Task<Cliente?> ObterPorIdAsync(ClienteId id, CancellationToken cancellationToken);

    Task<bool> ExisteComDocumentoAsync(Documento documento, CancellationToken cancellationToken);

    void Adicionar(Cliente cliente);
}
