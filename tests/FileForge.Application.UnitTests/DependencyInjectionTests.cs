using FileForge.Application.Behaviors;
using FileForge.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Application.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistraMediatorELoggingBehavior()
    {
        var services = new ServiceCollection().AddLogging().AddApplication();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IMediator>());
        Assert.Contains(services, d => d.ServiceType == typeof(IPipelineBehavior<,>) && d.ImplementationType == typeof(LoggingBehavior<,>));
    }
}
