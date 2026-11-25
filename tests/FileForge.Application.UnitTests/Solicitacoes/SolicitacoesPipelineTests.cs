using FileForge.Application.Abstractions.Seguranca;
using FileForge.Application.Solicitacoes.Criar;
using FileForge.Application.Solicitacoes.Obter;
using FileForge.Application.Solicitacoes.Reprocessar;
using FileForge.Application.UnitTests.Fakes;
using FileForge.Domain.Erros;
using FileForge.Domain.Events;
using FileForge.Domain.Interfaces;
using FileForge.Domain.Models;
using FileForge.Domain.Specifications;
using FileForge.Domain.ValueObjects;
using FileForge.Mediator;
using FileForge.SharedKernel.Abstractions;
using FileForge.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Application.UnitTests.Solicitacoes;

/// <summary>
/// Pipeline completo (Logging → Autorização → Validação → Handler) montado pelo <c>AddApplication()</c> real.
/// Cobre os cenários de segurança que a API com JWT vai expor: 401, 403, IDOR e o fluxo feliz.
/// </summary>
public sealed class SolicitacoesPipelineTests : IDisposable
{
    private readonly SolicitacaoRepositorioFake _repo = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly RelogioFixo _relogio = new();
    private readonly List<ServiceProvider> _providers = [];

    private IMediator ComoUsuario(IUsuarioAtual usuario)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISolicitacaoArquivoRepository>(_repo)
            .AddSingleton<IUnitOfWork>(_uow)
            .AddSingleton<IRegistradorDeEventos>(new RegistradorDeEventosFake())
            .AddSingleton<IDateTimeProvider>(_relogio)
            .AddSingleton(usuario);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        _providers.Add(provider);
        return provider.CreateScope().ServiceProvider.GetRequiredService<IMediator>();
    }

    private static readonly DateOnly Dia10 = new(2025, 1, 10);

    // ---------- Fluxo feliz ----------

    [Fact]
    public async Task Criar_ComoAna_UsaClienteDoTokenEGravaQuemPediu()
    {
        var result = await ComoUsuario(UsuarioFake.Ana()).Send(new CriarSolicitacaoCommand(Dia10));

        Assert.True(result.IsSuccess);
        var salva = Assert.Single(_repo.Itens);
        Assert.Equal(UsuarioFake.ClienteDaAna, salva.ClienteId);
        Assert.Equal("ana", salva.SolicitadoPor);
        Assert.Equal(1, _uow.Commits);
    }

    // ---------- 401 / 403 (AuthorizationBehavior) ----------

    [Fact]
    public async Task Criar_SemAutenticacao_RetornaUnauthorizedSemExecutarHandler()
    {
        var result = await ComoUsuario(UsuarioFake.Anonimo()).Send(new CriarSolicitacaoCommand(Dia10));

        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Empty(_repo.Itens);
    }

    [Fact]
    public async Task Criar_SemPermissao_RetornaForbidden()
    {
        var semPermissao = new UsuarioFake("carlos", UsuarioFake.ClienteDaAna, true, Permissoes.ConsultarExtrato);

        var result = await ComoUsuario(semPermissao).Send(new CriarSolicitacaoCommand(Dia10));

        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Contains(Permissoes.SolicitarExtrato, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Criar_OperadorSemCliente_RetornaForbidden()
    {
        var operadorQuePodeSolicitar = new UsuarioFake("op", null, true, Permissoes.SolicitarExtrato);

        var result = await ComoUsuario(operadorQuePodeSolicitar).Send(new CriarSolicitacaoCommand(Dia10));

        Assert.Equal(ErrosDeSeguranca.UsuarioSemCliente, result.Error);
    }

    [Fact]
    public async Task Reprocessar_ComoCliente_RetornaForbidden()
    {
        var result = await ComoUsuario(UsuarioFake.Ana()).Send(new ReprocessarSolicitacaoCommand(Guid.NewGuid()));

        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    // ---------- Validação (ValidationBehavior) ----------

    [Fact]
    public async Task Criar_DataVazia_RetornaValidationErrorComLista()
    {
        var result = await ComoUsuario(UsuarioFake.Ana()).Send(new CriarSolicitacaoCommand(default));

        var validacao = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(validacao.Errors, e => e.Code == "DataReferencia.Obrigatoria");
        Assert.Empty(_repo.Itens);
    }

    [Fact]
    public async Task Criar_SemPermissaoEComDadoInvalido_AutorizacaoVemPrimeiro()
    {
        var result = await ComoUsuario(UsuarioFake.Anonimo()).Send(new CriarSolicitacaoCommand(default));

        Assert.Equal(ErrorType.Unauthorized, result.Error.Type); // não revela regras de validação a quem não pode
    }

    // ---------- Regras de negócio ----------

    [Fact]
    public async Task Criar_DuasVezesMesmaData_RetornaConflito()
    {
        var mediator = ComoUsuario(UsuarioFake.Ana());
        await mediator.Send(new CriarSolicitacaoCommand(Dia10));

        var segunda = await mediator.Send(new CriarSolicitacaoCommand(Dia10));

        Assert.Equal(DomainErrors.SolicitacaoArquivo.JaExisteAtiva, segunda.Error);
    }

    [Fact]
    public async Task Criar_CorridaQueViolaOIndice_DevolveConflitoEmVezDeExcecao()
    {
        var mediator = ComoUsuarioComCommitQueViolaUnicidade(RestricaoUnica.SolicitacaoAtiva, UsuarioFake.Ana());

        var result = await mediator.Send(new CriarSolicitacaoCommand(Dia10));

        Assert.Equal(DomainErrors.SolicitacaoArquivo.JaExisteAtiva, result.Error);
    }

    [Fact]
    public async Task Criar_ViolacaoDeOutraRestricao_NaoEhEngolida()
    {
        var mediator = ComoUsuarioComCommitQueViolaUnicidade(RestricaoUnica.ContaAgenciaNumero, UsuarioFake.Ana());

        await Assert.ThrowsAsync<UnicidadeVioladaException>(() =>
            mediator.Send(new CriarSolicitacaoCommand(Dia10)));
    }

    private IMediator ComoUsuarioComCommitQueViolaUnicidade(RestricaoUnica restricao, IUsuarioAtual usuario)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<ISolicitacaoArquivoRepository>(_repo)
            .AddSingleton<IUnitOfWork>(new UnitOfWorkQueViolaUnicidade(restricao))
            .AddSingleton<IRegistradorDeEventos>(new RegistradorDeEventosFake())
            .AddSingleton<IDateTimeProvider>(_relogio)
            .AddSingleton(usuario);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        _providers.Add(provider);
        return provider.CreateScope().ServiceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Criar_AnaEBrunoMesmaData_NaoConflitam()
    {
        await ComoUsuario(UsuarioFake.Ana()).Send(new CriarSolicitacaoCommand(Dia10));
        var bruno = await ComoUsuario(UsuarioFake.Bruno()).Send(new CriarSolicitacaoCommand(Dia10));

        Assert.True(bruno.IsSuccess);
        Assert.Equal(2, _repo.Itens.Count);
    }

    // ---------- Autorização por recurso (IDOR) ----------

    [Fact]
    public async Task Obter_DonoConsegueVer()
    {
        var id = (await ComoUsuario(UsuarioFake.Ana()).Send(new CriarSolicitacaoCommand(Dia10))).Value;

        var result = await ComoUsuario(UsuarioFake.Ana()).Send(new ObterSolicitacaoQuery(id));

        Assert.True(result.IsSuccess);
        Assert.Equal("Pendente", result.Value.Status);
    }

    [Fact]
    public async Task Obter_BrunoTentaVerDaAna_Recebe404ParaNaoRevelarExistencia()
    {
        var id = (await ComoUsuario(UsuarioFake.Ana()).Send(new CriarSolicitacaoCommand(Dia10))).Value;

        var result = await ComoUsuario(UsuarioFake.Bruno()).Send(new ObterSolicitacaoQuery(id));

        Assert.Equal(ErrosDeSolicitacao.NaoEncontrada, result.Error);
    }

    [Fact]
    public async Task Obter_OperadorVeDeQualquerCliente()
    {
        var id = (await ComoUsuario(UsuarioFake.Ana()).Send(new CriarSolicitacaoCommand(Dia10))).Value;

        var result = await ComoUsuario(UsuarioFake.Operador()).Send(new ObterSolicitacaoQuery(id));

        Assert.True(result.IsSuccess);
    }

    // ---------- Reprocessamento ----------

    [Fact]
    public async Task Obter_IdInexistente_Retorna404()
    {
        var result = await ComoUsuario(UsuarioFake.Ana()).Send(new ObterSolicitacaoQuery(Guid.NewGuid()));

        Assert.Equal(ErrosDeSolicitacao.NaoEncontrada, result.Error);
    }

    [Fact]
    public async Task Obter_Anonimo_RetornaUnauthorized()
    {
        var result = await ComoUsuario(UsuarioFake.Anonimo()).Send(new ObterSolicitacaoQuery(Guid.NewGuid()));

        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
    }

    [Fact]
    public async Task Criar_DataNoFuturo_RetornaErroDeDominioSemCommit()
    {
        var result = await ComoUsuario(UsuarioFake.Ana()).Send(new CriarSolicitacaoCommand(new DateOnly(2025, 1, 16)));

        Assert.Equal(DomainErrors.DataReferencia.NoFuturo, result.Error);
        Assert.Empty(_repo.Itens);
        Assert.Equal(0, _uow.Commits);
    }

    [Fact]
    public async Task Criar_AposFalhaDaAnterior_PermiteNovaSolicitacao()
    {
        var mediator = ComoUsuario(UsuarioFake.Ana());
        await mediator.Send(new CriarSolicitacaoCommand(Dia10));
        var primeira = _repo.Itens.Single();
        primeira.IniciarProcessamento(_relogio);
        primeira.Falhar("erro", _relogio);

        var segunda = await mediator.Send(new CriarSolicitacaoCommand(Dia10));

        Assert.True(segunda.IsSuccess);
        Assert.Equal(2, _repo.Itens.Count);
    }

    [Fact]
    public async Task Reprocessar_IdInexistente_Retorna404()
    {
        var result = await ComoUsuario(UsuarioFake.Operador()).Send(new ReprocessarSolicitacaoCommand(Guid.NewGuid()));

        Assert.Equal(ErrosDeSolicitacao.NaoEncontrada, result.Error);
    }

    [Fact]
    public async Task Reprocessar_OperadorComSolicitacaoFalhada_VoltaParaPendente()
    {
        var id = (await ComoUsuario(UsuarioFake.Ana()).Send(new CriarSolicitacaoCommand(Dia10))).Value;
        var s = _repo.Itens.Single();
        s.IniciarProcessamento(_relogio);
        s.Falhar("bucket fora", _relogio);

        var result = await ComoUsuario(UsuarioFake.Operador()).Send(new ReprocessarSolicitacaoCommand(id));

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusSolicitacao.Pendente, s.Status);
    }

    [Fact]
    public async Task Reprocessar_Pendente_RetornaTransicaoInvalidaSemCommit()
    {
        var id = (await ComoUsuario(UsuarioFake.Ana()).Send(new CriarSolicitacaoCommand(Dia10))).Value;
        var commitsAntes = _uow.Commits;

        var result = await ComoUsuario(UsuarioFake.Operador()).Send(new ReprocessarSolicitacaoCommand(id));

        Assert.Equal(DomainErrors.SolicitacaoArquivo.TransicaoInvalida, result.Error);
        Assert.Equal(commitsAntes, _uow.Commits);
    }

    public void Dispose()
    {
        foreach (var p in _providers)
            p.Dispose();
    }
}
