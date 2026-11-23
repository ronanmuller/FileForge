using FileForge.Application.Behaviors;
using FileForge.Application.Solicitacoes.Criar;
using FileForge.Application.UnitTests.Fakes;
using FileForge.Mediator;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Application.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistraBehaviorsNaOrdemCorreta()
    {
        var services = new ServiceCollection().AddLogging().AddApplication();

        var behaviors = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(d => d.ImplementationType)
            .ToList();

        Assert.Equal([typeof(LoggingBehavior<,>), typeof(AuthorizationBehavior<,>), typeof(ValidationBehavior<,>)], behaviors);
    }

    [Fact]
    public void AddApplication_ComDependenciasDoHost_ResolveTudo()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<Abstractions.Seguranca.IUsuarioAtual>(UsuarioFake.Ana())
            .AddSingleton<Domain.Solicitacoes.ISolicitacaoArquivoRepository>(new SolicitacaoRepositorioFake())
            .AddSingleton<SharedKernel.Abstractions.IUnitOfWork>(new UnitOfWorkFake())
            .AddSingleton<SharedKernel.Abstractions.IDateTimeProvider>(new RelogioFixo());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IMediator>());
        Assert.NotEmpty(scope.ServiceProvider.GetServices<IValidator<CriarSolicitacaoCommand>>());
    }
}
