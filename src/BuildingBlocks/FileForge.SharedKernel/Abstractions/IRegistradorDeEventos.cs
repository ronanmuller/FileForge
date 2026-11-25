using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileForge.SharedKernel.Abstractions;

/// <summary>
/// Converte os eventos de integração pendentes dos aggregates alterados em mensagens persistidas
/// na mesma transação. Implementação na Infrastructure; a Application e o UnitOfWork só conhecem esta interface.
/// </summary>
public interface IRegistradorDeEventos
{
    void RegistrarEventos();
}
