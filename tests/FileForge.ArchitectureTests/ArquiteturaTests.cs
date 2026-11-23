using System.Reflection;
using FileForge.Domain.Erros;
using FileForge.SharedKernel.Domain;
using NetArchTest.Rules;

namespace FileForge.ArchitectureTests;

/// <summary>
/// Transforma as regras do GUIA-DO-PROJETO.md em testes: se alguém violar a arquitetura, o build do CI falha.
/// </summary>
public class ArquiteturaTests
{
    private static readonly Assembly Domain = typeof(DomainErrors).Assembly;
    private static readonly Assembly SharedKernel = typeof(Entity<>).Assembly;
    private static readonly Assembly Application = typeof(FileForge.Application.DependencyInjection).Assembly;

    private static void AssertOk(TestResult resultado) =>
        Assert.True(resultado.IsSuccessful,
            "Violação: " + string.Join(", ", resultado.FailingTypeNames ?? []));

    // ---------- Direção das dependências ----------

    [Fact]
    public void SharedKernel_NaoDependeDeNenhumaOutraCamada() =>
        AssertOk(Types.InAssembly(SharedKernel).ShouldNot()
            .HaveDependencyOnAny("FileForge.Domain", "FileForge.Application", "FileForge.Mediator", "FileForge.Infrastructure")
            .GetResult());

    [Fact]
    public void Domain_NaoDependeDeMediatorApplicationNemInfra() =>
        AssertOk(Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny("FileForge.Mediator", "FileForge.Application", "FileForge.Infrastructure")
            .GetResult());

    [Fact]
    public void Domain_NaoDependeDeFrameworksDeInfraestrutura() =>
        AssertOk(Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.Extensions.Logging",
                "Microsoft.Extensions.DependencyInjection", "Microsoft.AspNetCore", "System.Net.Http")
            .GetResult());

    [Fact]
    public void Application_NaoDependeDeInfraestrutura() =>
        AssertOk(Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny("FileForge.Infrastructure", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult());

    // ---------- Convenções de DDD ----------
    // Reflection direta: os filtros Inherit/ImplementInterface do NetArchTest não casam com genéricos abertos
    // (Entity<>) nem com interfaces herdadas — as regras "passariam no vazio". Ver Filtros_EncontramOsTiposEsperados.

    private static IEnumerable<Type> Entidades => Domain.GetTypes().Where(t => !t.IsAbstract && IsSubclassOfGeneric(t, typeof(Entity<>)));
    private static IEnumerable<Type> ValueObjects => Domain.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(ValueObject)));
    private static IEnumerable<Type> Eventos => Domain.GetTypes().Where(t => !t.IsAbstract && typeof(IDomainEvent).IsAssignableFrom(t));

    private static void SemViolacoes(IEnumerable<string> violacoes, string regra)
    {
        var lista = violacoes.ToList();
        Assert.True(lista.Count == 0, $"{regra}: {string.Join(", ", lista)}");
    }

    [Fact]
    public void ValueObjects_SaoSealed() =>
        SemViolacoes(ValueObjects.Where(t => !t.IsSealed).Select(t => t.Name), "Value Object deve ser sealed");

    [Fact]
    public void Entidades_SaoSealed() =>
        SemViolacoes(Entidades.Where(t => !t.IsSealed).Select(t => t.Name), "Entidade deve ser sealed");

    [Fact]
    public void DomainEvents_SaoSealedEImutaveis() =>
        SemViolacoes(Eventos.Where(t => !t.IsSealed || t.GetProperties().Any(p => p.SetMethod is { IsPublic: true } s && !IsInitOnly(s)))
            .Select(t => t.Name), "Evento deve ser sealed e sem setter público");

    [Fact]
    public void Entidades_NaoTemConstrutorPublico() =>
        SemViolacoes(Entidades.Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length > 0)
            .Select(t => t.Name), "Use um método de fábrica (Criar/Registrar)");

    [Fact]
    public void EntidadesEValueObjects_NaoTemSetterPublico() =>
        SemViolacoes(Entidades.Concat(ValueObjects)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod?.IsPublic == true)
                .Select(p => $"{t.Name}.{p.Name}")), "Estado só muda por métodos de negócio");

    // ---------- Sanidade: os filtros acima realmente encontram tipos ----------

    [Fact]
    public void Filtros_EncontramOsTiposEsperados()
    {
        Assert.Contains("SolicitacaoArquivo", Entidades.Select(t => t.Name));
        Assert.Contains("Conta", Entidades.Select(t => t.Name));
        Assert.Contains("Dinheiro", ValueObjects.Select(t => t.Name));
        Assert.Contains("ArquivoGerado", Eventos.Select(t => t.Name));
    }

    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");

    private static bool IsSubclassOfGeneric(Type? type, Type generic)
    {
        for (type = type?.BaseType; type is not null; type = type.BaseType)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == generic)
                return true;
        return false;
    }
}
