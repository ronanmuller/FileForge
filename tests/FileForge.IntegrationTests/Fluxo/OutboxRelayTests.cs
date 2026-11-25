using FileForge.Infrastructure;
using FileForge.Infrastructure.Outbox;
using FileForge.Infrastructure.Persistence;
using FileForge.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileForge.IntegrationTests.Fluxo;

/// <summary>
/// Prova o contrato do relay: publica as pendentes, marca só as publicadas, e uma falha
/// não bloqueia as demais mensagens do lote.
/// </summary>
public sealed class OutboxRelayTests() : PostgresFixture("fileforge_test_relay")
{
    [Fact]
    public async Task ProcessarLote_PublicaEMarcaAsPendentes()
    {
        await Salvar(db =>
        {
            db.OutboxMessages.Add(Mensagem("ArquivoGerado", "{}"));
            db.OutboxMessages.Add(Mensagem("ArquivoGerado", "{}"));
        });

        var publicador = new PublicadorEmMemoria();
        var relay = CriarRelay(publicador);

        var publicadas = await relay.ProcessarLoteAsync(default);

        Assert.Equal(2, publicadas);
        Assert.Equal(2, publicador.Publicadas.Count);

        await using var leitura = Escopo();
        var db = leitura.ServiceProvider.GetRequiredService<FileForgeDbContext>();
        var todas = await db.OutboxMessages.AsNoTracking().ToListAsync();
        Assert.All(todas, m => Assert.False(m.Pendente));
    }

    [Fact]
    public async Task ProcessarLote_FalhaEmUmaMensagem_NaoMarcaEAsOutrasSaem()
    {
        var ruim = Mensagem("Quebra", "{}");
        var boa = Mensagem("ArquivoGerado", "{}");
        await Salvar(db =>
        {
            db.OutboxMessages.Add(ruim);
            db.OutboxMessages.Add(boa);
        });

        var publicador = new PublicadorQueFalhaPara("Quebra");
        var relay = CriarRelay(publicador);

        var publicadas = await relay.ProcessarLoteAsync(default);

        Assert.Equal(1, publicadas);

        await using var leitura = Escopo();
        var db = leitura.ServiceProvider.GetRequiredService<FileForgeDbContext>();
        var lidaRuim = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == ruim.Id);
        var lidaBoa = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == boa.Id);

        Assert.True(lidaRuim.Pendente);
        Assert.Equal(1, lidaRuim.Tentativas);
        Assert.False(lidaBoa.Pendente);
    }

    private IOutboxRelay CriarRelay(IMensagePublicador publicador)
    {
        var provedor = new ServiceCollection()
            .AddInfrastructure(ConexaoAtual)
            .AddLogging()
            .AddSingleton<IDateTimeProvider>(Relogio)
            .AddSingleton(publicador)
            .BuildServiceProvider();

        return provedor.GetRequiredService<IOutboxRelay>();
    }

    private static OutboxMessage Mensagem(string tipo, string conteudo) =>
        new(Guid.NewGuid(), tipo, conteudo, new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc));

    private sealed class PublicadorQueFalhaPara(string tipoQueFalha) : IMensagePublicador
    {
        public Task PublicarAsync(OutboxMessage mensagem, CancellationToken cancellationToken) =>
            mensagem.Tipo == tipoQueFalha
                ? throw new InvalidOperationException("broker indisponível")
                : Task.CompletedTask;
    }
}
