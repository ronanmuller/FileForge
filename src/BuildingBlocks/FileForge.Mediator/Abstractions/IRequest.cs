namespace FileForge.Mediator;

/// <summary>Marca uma mensagem (comando ou consulta) que produz uma resposta.</summary>
public interface IRequest<out TResponse>;

/// <summary>Processa um único tipo de requisição (SRP: um handler por caso de uso).</summary>
public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
