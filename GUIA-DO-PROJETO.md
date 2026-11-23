# 📘 Guia do Projeto FileForge — Decisões e Conceitos

> Documento **vivo**: explica *por que* o código é como é. A cada etapa nova, acrescente uma seção em
> [Histórico de decisões](#11-histórico-de-decisões-adr-resumido).
> O `README.md` diz **o que** é o projeto e o roadmap; este guia diz **por quê**.

## Sumário

1. [Visão geral da arquitetura](#1-visão-geral-da-arquitetura)
2. [DDD — a ideia central](#2-ddd--a-ideia-central)
3. [Os blocos táticos: Value Object, Entity, Aggregate](#3-os-blocos-táticos)
4. [Os aggregates do FileForge](#4-os-aggregates-do-fileforge)
5. [Domain Events](#5-domain-events)
6. [Tratamento de erros: Result × Exceção](#6-tratamento-de-erros-result--exceção)
7. [`record` × `class`: quando usar cada um](#7-record--class-quando-usar-cada-um)
8. [Mediator e Pipeline Behaviors](#8-mediator-e-pipeline-behaviors)
9. [Detalhes de código que parecem estranhos](#9-detalhes-de-código-que-parecem-estranhos)
10. [Testes: como rodar e debugar para estudar](#10-testes-como-rodar-e-debugar-para-estudar)
11. [Histórico de decisões](#11-histórico-de-decisões-adr-resumido)
12. [Segurança: as três camadas](#12-segurança-as-três-camadas)

---

## 1. Visão geral da arquitetura

```mermaid
flowchart LR
    SK["SharedKernel<br/>Entity, ValueObject, Result"] --> D["Domain<br/>regras de negócio"]
    D --> APP["Application<br/>casos de uso (etapa 8)"]
    M["Mediator"] --> APP
    APP --> INF["Infrastructure<br/>EF Core, bucket (etapa 9+)"]
    APP --> API["API / Worker"]
```

| Projeto | Responsabilidade | Depende de |
|---|---|---|
| `FileForge.SharedKernel` | Peças genéricas de DDD (servem a qualquer domínio) | nada |
| `FileForge.Mediator` | Envio de commands/queries e publicação de notificações | DI da Microsoft |
| `FileForge.Domain` | Regras do negócio de extratos | **só** SharedKernel |
| `FileForge.Application` | Orquestra casos de uso (etapa 8) | Domain + Mediator |

**Regra de ouro:** as dependências apontam **para dentro**. O Domain não conhece banco, API, fila nem Mediator — por isso é testável sem subir nada.

---

## 2. DDD — a ideia central

### 2.1 O problema: modelo anêmico

```csharp
// ❌ Anêmico: só dados; qualquer um altera qualquer coisa
public class SolicitacaoArquivo
{
    public Guid Id { get; set; }
    public string Status { get; set; }
}

// a regra fica espalhada (e duplicada) em services, workers, controllers...
if (s.Status == "Pendente") s.Status = "Processando";
```

- Nada impede `s.Status = "Concluida"` direto de `Pendente`.
- A mesma regra é reescrita em vários lugares e diverge com o tempo.

### 2.2 A solução: o objeto protege a si mesmo

```csharp
// ✅ Rico: dados + regras juntos; o estado só muda por métodos com nome de negócio
public Result IniciarProcessamento(IDateTimeProvider relogio)
{
    if (Status != StatusSolicitacao.Pendente)
        return DomainErrors.SolicitacaoArquivo.TransicaoInvalida;
    Status = StatusSolicitacao.Processando;
    ...
}
```

> **Um objeto de domínio nunca existe em estado inválido.**

### 2.3 Estratégico × Tático

| Metade | O que é | No projeto |
|---|---|---|
| **Estratégico** | Entender o negócio: *linguagem ubíqua*, *bounded contexts* | Nomes do negócio em português: `SolicitacaoArquivo`, `Reprocessar`, `DataReferencia` |
| **Tático** | Padrões de código | Value Object, Entity, Aggregate, Domain Event, Repository |

**Linguagem ubíqua:** o código fala como o especialista. Se o analista diz *"a solicitação falhou e foi reprocessada"*, existe `Falhar()` e `Reprocessar()` — não `SetStatus(3)`.

### 2.4 Invariante

Regra que **sempre** precisa ser verdadeira. Garantir invariantes é o motivo de existir dos aggregates e value objects.

| Invariante | Quem garante | Como |
|---|---|---|
| Dinheiro nunca negativo | `Dinheiro` | `Criar` recusa |
| Data de referência não está no futuro | `DataReferencia` | `Criar` compara com `IDateTimeProvider` |
| Conta não duplicada no cliente | `Cliente` | só ele enxerga todas as contas |
| Transição de status válida | `SolicitacaoArquivo` | `private set` + métodos |
| Uma solicitação **ativa** por cliente/data | **Application** (+ índice único no banco) | envolve vários aggregates — ver §4.4 |

---

## 3. Os blocos táticos

| Conceito | Igualdade por | Muda ao longo do tempo? | Exemplos |
|---|---|---|---|
| **Value Object** | **valores** | não (imutável) | `Documento`, `Dinheiro`, `DataReferencia` |
| **Entity** | **Id** | sim | `Conta` |
| **Aggregate Root** | **Id** + é a porta de entrada | sim | `Cliente`, `Transacao`, `SolicitacaoArquivo` |

### 3.1 Value Object — "se existe, é válido"

```csharp
public sealed class Dinheiro : ValueObject
{
    private Dinheiro(decimal valor) => Valor = valor;        // construtor privado

    public static Result<Dinheiro> Criar(decimal valor) =>  // única porta de entrada
        valor < 0 ? DomainErrors.Dinheiro.Negativo
                  : new Dinheiro(Math.Round(valor, 2, MidpointRounding.AwayFromZero));

    public static Dinheiro operator +(Dinheiro a, Dinheiro b) => new(a.Valor + b.Valor); // nova instância
}
```

- Valida **uma vez**, na criação. Quem recebe um `Dinheiro` não precisa revalidar.
- Combate a *primitive obsession* (usar `string`/`decimal` para tudo e validar em todo lugar — ou em lugar nenhum).
- Igualdade definida por `GetEqualityComponents()` na base `ValueObject`.

### 3.2 IDs fortemente tipados

```csharp
public readonly record struct ClienteId(Guid Value);
```

Se tudo fosse `Guid`, `ObterTransacoes(solicitacaoId)` compilaria mesmo sendo um bug. Com tipos próprios, **o compilador impede a troca**.

### 3.3 Entity — identidade

Duas contas com a mesma agência/número continuam sendo contas diferentes se os Ids forem diferentes. A base `Entity<TId>` compara **só tipo + Id**.

### 3.4 Aggregate Root — a fronteira

- Única porta de entrada para alterar o que está dentro.
- Fronteira de **consistência** (o que precisa estar correto na mesma transação) e de **concorrência**.
- Acumula **Domain Events**.

---

## 4. Os aggregates do FileForge

```mermaid
classDiagram
    class Cliente { <<AggregateRoot>> ClienteId Id; string Nome; Documento Documento; AdicionarConta() }
    class Conta { <<Entity>> ContaId Id; string Agencia; string Numero }
    class Transacao { <<AggregateRoot>> TransacaoId Id; ContaId ContaId; Dinheiro Valor; TipoTransacao Tipo }
    class SolicitacaoArquivo { <<AggregateRoot>> SolicitacaoArquivoId Id; ClienteId ClienteId; Status; Tentativas }
    Cliente "1" *-- "N" Conta
    Transacao ..> Conta : só ContaId
    SolicitacaoArquivo ..> Cliente : só ClienteId
```

### 4.1 Como decidir o tamanho de um aggregate

Não pergunte *"o que se relaciona com o quê?"*. Pergunte:

> **"Quais dados precisam estar consistentes na MESMA transação?"**

- **Conta duplicada** → preciso ver todas as contas ao mesmo tempo → `Conta` **dentro** de `Cliente`.
- **Transação** → registrar uma não depende de regra do cliente; e um cliente pode ter **milhões** → aggregate **separado**. Se ficasse dentro de `Cliente`, adicionar uma conta exigiria carregar milhões de linhas, e duas transações simultâneas brigariam pelo mesmo registro.

### 4.2 Regras aplicadas

| Regra | Código |
|---|---|
| Aggregates pequenos | `Transacao` separado de `Cliente` |
| Referência entre aggregates **só por Id** | `public ContaId ContaId` (nunca `public Conta Conta`) |
| Entidade filha só muda via root | `Conta.Criar` é `internal`; `Cliente.AdicionarConta` é `public` |
| Coleção não exposta para escrita | `private List<Conta> _contas` + `IReadOnlyCollection<Conta> Contas` |
| Um aggregate alterado por commit | será aplicado nos handlers (etapa 8) |

### 4.3 `SolicitacaoArquivo` — máquina de status

```mermaid
stateDiagram-v2
    [*] --> Pendente: Criar
    Pendente --> Processando: IniciarProcessamento (Tentativas++)
    Processando --> Concluida: Concluir(nomeArquivo)
    Processando --> Falhou: Falhar(motivo)
    Falhou --> Pendente: Reprocessar
```

- `Status` tem `private set`: a **única** forma de mudar é pelos métodos.
- Transição inválida → `Result.Failure(TransicaoInvalida)` e **nada muda** (nem evento é gerado).
- `Ativa` = `Pendente` ou `Processando` → base da idempotência.

### 4.4 Regra que **não** cabe num aggregate

"Só uma solicitação ativa por cliente/data" envolve **várias** solicitações; um aggregate não enxerga os outros. Solução pragmática:

1. Handler consulta `ExisteAtivaAsync` antes de criar (Application).
2. Índice único no banco como rede de segurança contra corrida (etapa 9).

> Nem toda regra cabe num aggregate — e tudo bem.

---

## 5. Domain Events

```csharp
RaiseDomainEvent(new ArquivoGerado(Id, NomeArquivo));
```

- Fato **no passado** (`Criada`, `Iniciado`, `Gerado`, `Falhou`).
- O aggregate só **anota**; não sabe quem vai reagir (e-mail, fila, auditoria). Isso o mantém desacoplado.
- Eventos atuais: `SolicitacaoArquivoCriada`, `ProcessamentoIniciado`, `ArquivoGerado`, `ProcessamentoFalhou`.

Fluxo previsto (etapas 8–11):

```mermaid
sequenceDiagram
    participant H as Handler
    participant A as Aggregate
    participant U as UnitOfWork
    participant O as Outbox
    participant W as Worker
    H->>A: método de negócio
    A-->>A: RaiseDomainEvent
    H->>U: SaveChangesAsync
    U->>O: grava eventos na MESMA transação
    O-->>W: publica depois
```

**Outbox** resolve a *dupla escrita*: sem ele, poderíamos salvar no banco e cair antes de publicar na fila (ou o contrário).

---

## 6. Tratamento de erros: Result × Exceção

> **Exceção é para o excepcional. Falha de negócio esperada é resultado.**

| Situação | Exemplo | Tratamento |
|---|---|---|
| Regra de negócio | conta duplicada, data no futuro, transição inválida | `return DomainErrors.X;` → `Result.Failure` |
| Entrada inválida | `ClienteId` vazio | `ErrorType.Validation` (FluentValidation na etapa 8) |
| Bug do programador | `relogio` nulo, `TipoTransacao` = 99 | `throw` — corrige-se o código |
| Infra transitória | timeout no banco/bucket | exceção + Polly (etapa 12) |

Por que `Result`:
- A assinatura `Result<Cliente>` **avisa** que pode falhar; exceção é invisível.
- Exceção é cara e funciona como "goto invisível" quando usada para fluxo.
- `ErrorType` vira status HTTP na API: `Validation`→400, `NotFound`→404, `Conflict`→409.

**Catálogo `DomainErrors`**: todos os erros num lugar, com **código estável** (`Cliente.ContaDuplicada`). Testes comparam o erro exato; clientes da API podem depender do código.

**Conversões implícitas** permitem escrever:

```csharp
return DomainErrors.Cliente.NomeObrigatorio;   // Error → Result<Cliente> (falha)
return new Cliente(...);                       // Cliente → Result<Cliente> (sucesso)
```

---

## 7. `record` × `class`: quando usar cada um

### 7.1 O que `record` gera

Igualdade por valor, `ToString`, `with`, desconstrução, propriedades `init`.

### 7.2 Decisões no projeto

| Tipo | Escolha | Motivo |
|---|---|---|
| IDs (`ClienteId`...) | `readonly record struct` | valor único, igualdade grátis, sem alocação |
| `Error` | `sealed record` | dado imutável sem invariante |
| Domain Events | `sealed record` | fatos imutáveis |
| Commands/Queries/DTOs (etapa 8) | `sealed record` | mensagens imutáveis |
| `Documento`, `Dinheiro`, `DataReferencia` | `class : ValueObject` | **têm invariante** (ver 7.3) |
| `Cliente`, `Conta`, `Transacao`, `SolicitacaoArquivo` | `class : Entity/AggregateRoot` | identidade + mutáveis (ver 7.4) |

### 7.3 Armadilhas de usar `record` em Value Object com regra

```csharp
public sealed record Dinheiro(decimal Valor);
var d = new Dinheiro(-50m);              // ❌ construtor público burla a validação
var e = d with { Valor = -999m };        // ❌ "with" também burla
```

Mesmo com construtor privado, o `with` usa o construtor de cópia + `init`. Além disso, `record` compara `List<T>` **por referência**. Com `class : ValueObject`, a única porta é `Criar`.

> Demonstração executável: `IgualdadeEInvariantesTests.ArmadilhaDoRecord_ConstrutorPublicoEWithBurlamValidacao`.

### 7.4 Por que Entity **nunca** é `record`

- `record` compara **todos** os campos; entidade é igual pelo **Id**.
- Entidade é mutável por natureza; `record` é pensado para imutabilidade.
- O change tracker do EF Core depende de identidade estável.

### 7.5 Fluxograma de decisão

```mermaid
flowchart TD
    A["Novo tipo"] --> B{"Tem identidade<br/>ao longo do tempo?"}
    B -->|Sim| C["class : Entity / AggregateRoot"]
    B -->|Não| D{"Tem invariante<br/>a proteger?"}
    D -->|Sim| E["class : ValueObject<br/>construtor privado + Criar"]
    D -->|Não| F{"Valor único e pequeno?"}
    F -->|Sim| G["readonly record struct"]
    F -->|Não| H["sealed record"]
```

---

## 8. Mediator e Pipeline Behaviors

### 8.1 Por quê

Cada caso de uso vira uma **mensagem** com **um handler**:

```csharp
public sealed record CriarSolicitacaoCommand(Guid ClienteId, DateOnly Data) : IRequest<Result<Guid>>;
var result = await mediator.Send(new CriarSolicitacaoCommand(id, data), ct);
```

- Um caso de uso = um handler: fácil de achar e testar.
- Preocupações transversais (validação, log, transação, idempotência) escritas **uma vez** como behaviors.
- Controller fino; casa com **CQRS** (command altera, query lê).
- **Por que próprio e não MediatR?** Estudo + MediatR passou a ter licença comercial.

### 8.2 Contratos

| Interface | Papel |
|---|---|
| `IRequest<TResponse>` + `IRequestHandler<,>` | 1 mensagem → **exatamente 1** handler |
| `INotification` + `INotificationHandler<>` | 1 mensagem → **0..N** handlers |
| `ISender` / `IPublisher` / `IMediator` | ISP: depende só do que usa |
| `IPipelineBehavior<,>` | middleware em volta do handler |

### 8.3 Pipeline = cebola

```mermaid
flowchart LR
    S["Send"] --> L["Logging"] --> V["Validation"] --> T["Transaction"] --> H["Handler"]
```

```csharp
public async Task<TResponse> Handle(TRequest req, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
{
    // antes
    var resp = await next();   // próximo behavior ou o handler
    // depois
    return resp;
}
```

Um behavior pode **não** chamar `next()` (ex.: validação falhou → retorna `Result.Failure`).

### 8.4 Como funciona por dentro

**Problema:** `Send` recebe `IRequest<TResponse>`, mas o DI precisa de `IRequestHandler<TRequest, TResponse>` — e `TRequest` só é conhecido em runtime.

**Solução:** wrapper genérico criado por reflection **uma vez por tipo** e cacheado em `ConcurrentDictionary`:

```csharp
RequestWrappers.GetOrAdd(request.GetType(), t =>
    Activator.CreateInstance(typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(t, typeof(TResponse)))!);
```

Montagem do pipeline (em `RequestHandlerWrapper.cs`):

```csharp
RequestHandlerDelegate<TResponse> pipeline = () => handler.Handle(typedRequest, ct);
foreach (var behavior in behaviors.Reverse())
{
    var next = pipeline;   // captura obrigatória: sem ela a closure vira recursão infinita
    pipeline = () => behavior.Handle(typedRequest, next, ct);
}
```

- `.Reverse()`: monta de dentro para fora, então o **primeiro registrado** fica **mais externo**.
- `Publish`: se um handler falhar, os demais executam e as falhas são reunidas em `AggregateException`.
- Registro no DI é **idempotente** (`TryAdd*`): chamar `AddMediator` duas vezes não duplica handlers.

### 8.5 Domain não depende do Mediator

`IDomainEvent` (SharedKernel) ≠ `INotification` (Mediator). A ponte fica na Application:

```csharp
public sealed record DomainEventNotification(IDomainEvent Evento) : INotification;
```

> Demonstração executável: `CriarSolicitacaoFluxoTests.Publish_EventosDoAggregate_ChegamAosHandlersViaPonte`.

### 8.6 Críticas conhecidas

| Crítica | Resposta |
|---|---|
| Esconde dependências (F12 não leva ao handler) | Convenção `XCommand` → `XCommandHandler` |
| Indireção desnecessária em CRUD | O valor aqui está nos behaviors |
| Reflection é lenta | Só na 1ª chamada por tipo; depois cache |

### 8.7 Logs e observabilidade: `LogDefinitions` + `LoggingBehavior`

**Onde:** `src/Core/FileForge.Application/Logging/LogDefinitions.cs` e `Behaviors/LoggingBehavior.cs`.

```csharp
[LoggerMessage(EventId = 1002, Level = LogLevel.Warning,
    Message = "{RequestName} falhou por regra de negócio em {ElapsedMs} ms: {ErrorCode} - {ErrorMessage}")]
public static partial void RequisicaoFalhouNegocio(this ILogger logger, ...);
```

| Por que um catálogo central | Explicação |
|---|---|
| `[LoggerMessage]` (source generator) | O compilador gera o código: sem boxing, sem parse do template em runtime, checa `IsEnabled` antes. É a forma recomendada pela Microsoft para alto desempenho |
| `EventId` fixo por log | Dá para filtrar/alertar por ID no Seq, Grafana ou App Insights ("alerta se o 1003 subir") |
| Log estruturado (`{ErrorCode}`) | Vira campo pesquisável, não texto solto |
| Tudo num lugar | Mensagens consistentes; fácil revisar o que o sistema loga |

**Regra:** logar **uma vez, na borda**. O `LoggingBehavior` é o behavior mais externo e loga início, fim, duração e desfecho de **toda** requisição. Handlers e Domain não logam (o Domain nem pode: o teste de arquitetura proíbe `Microsoft.Extensions.Logging` no Domain).

| Desfecho | Nível | EventId |
|---|---|---|
| Sucesso | Information | 1001 |
| `Result.Failure` (negócio) | **Warning** — é esperado, não é bug | 1002 |
| Exceção | **Error** + stack trace, e relança | 1003 |
| Cancelamento | não loga como erro | — |
| Acima de 500 ms | Warning extra | 1004 |

**Por que não Serilog/OpenTelemetry já?** O código depende só de `ILogger` (abstração). Serilog e OpenTelemetry são configurados no *host* (API/Worker) e **não exigem mudar nenhum log**. Então: logs agora; exportação (Serilog → Seq, OTel → traces/métricas) quando existir a API (etapas 10 e 15).

### 8.8 Registro de dependências (IoC): um `AddX()` por camada

```csharp
// API/Worker (composition root) — único lugar que conhece todas as camadas
builder.Services
    .AddApplication()                 // FileForge.Application/DependencyInjection.cs
    .AddInfrastructure(configuration); // etapa 9
```

- Cada camada sabe registrar **o que ela tem** (handlers, behaviors, repositórios).
- O `Program.cs` fica com poucas linhas e não conhece detalhes internos.
- **Evitamos** um projeto único `CrossCutting.IoC` que referencia tudo: ele vira um "buraco" por onde qualquer camada enxerga qualquer outra, quebrando a regra das dependências.
- Testado: `DependencyInjectionTests` monta o container com `ValidateOnBuild` — erro de registro aparece no teste, não em produção.

### 8.9 Estamos prontos para Outbox, filas e consumidores?

**A base sim; a implementação é das etapas 9 e 11.** O que já existe e o que falta:

| Peça | Status | Onde / quando |
|---|---|---|
| Aggregates acumulam eventos sem saber quem consome | ✅ | `AggregateRoot.DomainEvents` |
| Eventos imutáveis, com `EventId` e horário determinístico | ✅ | `DomainEvent` (`EventId` serve para deduplicar) |
| `Unit of Work` sem commit nos repositórios | ✅ (contrato) | `IUnitOfWork` |
| Status `Pendente → Processando`, `Tentativas` | ✅ | `SolicitacaoArquivo` (base para retry e DLQ) |
| **Outbox**: salvar eventos na mesma transação | ⏳ etapa 9 | interceptor do EF Core grava `OutboxMessage` no `SaveChanges` |
| **Publicador do Outbox** (background) | ⏳ etapa 9/11 | lê pendentes → publica no broker → marca como enviada |
| **Broker** (RabbitMQ) + consumidor | ⏳ etapa 11 | Worker consome `SolicitacaoArquivoCriada` |
| **Consumidor idempotente** (Inbox) | ⏳ etapa 11 | tabela de mensagens já processadas pelo `EventId` |
| Retry + dead-letter queue | ⏳ etapa 11/12 | Polly + limite de `Tentativas` |
| Abstração `IEventBus` na Application | ⏳ etapa 8 | para não acoplar a Application ao RabbitMQ |

```mermaid
flowchart LR
    H["Handler"] -->|"SaveChanges"| DB[("Banco<br/>Solicitacao + OutboxMessage<br/>MESMA transação")]
    DB --> P["OutboxPublisher<br/>(background)"]
    P -->|"publica"| Q[["RabbitMQ"]]
    Q --> W["Worker<br/>consumidor idempotente"]
    W -->|"falhou N vezes"| DLQ[["Dead-letter"]]
```

**Garantia:** *at-least-once* (pelo menos uma vez). Uma mensagem pode chegar **duplicada**, por isso o consumidor precisa ser idempotente — e o `EventId` e a `SolicitacaoArquivo` com status já foram desenhados para isso.

### 8.10 Repositórios: onde ficam as interfaces e as implementações

```plaintext
FileForge.Domain/
  Clientes/       Cliente.cs, Conta.cs, IClienteRepository.cs
  Transacoes/     Transacao.cs, ITransacaoRepository.cs
  Solicitacoes/   SolicitacaoArquivo.cs, SolicitacaoArquivoSpecs.cs, ISolicitacaoArquivoRepository.cs
FileForge.Infrastructure/      ← etapa 9
  Persistence/Repositories/     implementações com EF Core
```

- **Interface no Domain, implementação na Infrastructure.** Quem *usa* define o contrato (Inversão de Dependência). Se a interface ficasse junto do EF Core, o Domain dependeria do banco.
- **Pasta por aggregate**, não uma pasta `Repositories/`: tudo de `SolicitacaoArquivo` fica junto.
- **Um repositório por aggregate root.** Não existe `IContaRepository`: `Conta` só é acessada via `Cliente`.
- **Sem projeto `FileForge.Repository` separado:** repositórios, Unit of Work e Outbox compartilham o mesmo `DbContext` e a mesma transação; ficam juntos em `FileForge.Infrastructure`.
- **Repositório não conhece outro aggregate.** `ITransacaoRepository.ListarPorContasEDataAsync` recebe `ContaId`s, e não `ClienteId`: a `Transacao` só conhece a conta. O handler carrega o `Cliente`, pega os Ids das contas e repassa. Evolução futura: uma consulta de leitura dedicada (`IExtratoQuery`, estilo CQRS) para gerar o arquivo.

### 8.11 Autorização: três níveis

| Nível | Pergunta | Onde | Resposta |
|---|---|---|---|
| Autenticação | O token é válido? | API (`AddJwtBearer`, etapa 10) | 401 |
| Permissão | Pode **fazer** isso? | `[RequerPermissao]` + `AuthorizationBehavior` | 403 |
| Recurso | Pode fazer isso **com este dado**? | No handler (ex.: `ObterSolicitacaoHandler`) | **404**, para não revelar que o id existe (anti-IDOR) |

- A Application não conhece JWT: depende de `IUsuarioAtual` (`Id`, `ClienteId`, `PossuiPermissao`). A API implementa lendo as claims; os testes usam um fake.
- `CriarSolicitacaoCommand` **não tem `ClienteId`**: ele vem do token. A Ana não consegue pedir o extrato do Bruno trocando um campo do JSON.
- A solicitação grava **quem pediu** (`SolicitadoPor`). O Worker não recebe o token do usuário pela fila.
- Permissões centralizadas em `Permissoes` (`extratos:solicitar`, `extratos:reprocessar`...): um nome errado quebraria a autorização.

### 8.12 Pipeline completo e validação

```mermaid
flowchart LR
    S["Send"] --> L["Logging"] --> A["Autorização"] --> V["Validação"] --> H["Handler"]
```

- **Autorização antes da validação:** quem não tem permissão não descobre quais campos são válidos.
- **Validação (FluentValidation) = formato da entrada.** Regra de negócio continua no Domain. Ex.: "data informada" é validação; "data não está no futuro" é regra do `DataReferencia`.
- **`ValidationError`** junta vários erros num só `Error`, então o `Result` continua com um erro e a API devolve 400 com todos os campos de uma vez.
- **`ResultFactory`:** o behavior só conhece `TResponse` genérico; a fábrica cria `Result` ou `Result<T>` de falha. Reflection uma vez por tipo, depois delegate em cache (mesma técnica do Mediator).
- Novos `ErrorType`: `Unauthorized` (401) e `Forbidden` (403).

---

## 9. Detalhes de código que parecem estranhos

| Código | Motivo |
|---|---|
| `private Cliente() { }` vazio | EF Core reconstrói o objeto do banco por ele; privado para ninguém usar |
| `IDateTimeProvider` em vez de `DateTime.UtcNow` | Teste determinístico ("futuro" não depende de quando o teste roda) |
| `static Criar(...)` em vez de `new` | Construtor não retorna `Result`; o método estático pode falhar sem exceção |
| `sealed` nas classes | Não foram desenhadas para herança; evita uso inesperado |
| `ValorComSinal` | Valor sempre positivo; o **tipo** dá o sinal (evita "débito de -50") |
| `Math.Round(..., AwayFromZero)` | Padrão monetário (10,125 → 10,13); o default do .NET é arredondamento bancário |

---

## 10. Testes: como rodar e debugar para estudar

### 10.1 Projetos

| Projeto | Tipo | O que cobre |
|---|---|---|
| `FileForge.SharedKernel.UnitTests` | unitário | `Result`, `Map/Bind/Match` |
| `FileForge.Mediator.UnitTests` | unitário | Send, Publish, pipeline, DI |
| `FileForge.Domain.UnitTests` | unitário | Value Objects, aggregates, máquina de status |
| `FileForge.Domain.UnitTests/Builders` | **apoio** | Test Data Builders: `Um.Cliente().ComConta().Build()`, `Um.Solicitacao().Concluida().Build()` |
| `FileForge.Application.UnitTests` | unitário | `LoggingBehavior` (níveis e EventIds), `AddApplication` |
| `FileForge.ArchitectureTests` | **arquitetura** | regras do guia viram testes: direção das dependências, `sealed`, sem setter/construtor público |
| `FileForge.Domain.UnitTests/Estudo` | **didático** | demonstra os conceitos deste guia |
| `FileForge.IntegrationTests` | integração | DI real + Mediator + Behavior + Domain juntos |

> Integração **hoje** = componentes reais conectados via DI, com repositório em memória.
> Na etapa 17 ganhará banco real com **Testcontainers**.

### 10.2 Rodar

```powershell
dotnet test                                                  # tudo
dotnet test tests/FileForge.Domain.UnitTests                 # só domínio
dotnet test --filter "FullyQualifiedName~Estudo"             # só os didáticos
dotnet test --filter "FullyQualifiedName~IntegrationTests"   # só integração
```

No Visual Studio: **Test Explorer** (`Ctrl+E, T`) → botão direito no teste → **Debug**.

### 10.3 Roteiro de estudo com breakpoints

| # | Arquivo / teste | Onde colocar breakpoint | O que observar |
|---|---|---|---|
| 1 | `Estudo/IgualdadeEInvariantesTests` → `ValueObject_DuasInstancias...` | `ValueObject.Equals` | comparação pelos componentes |
| 2 | `Estudo/...` → `Entity_MesmosDados_IdsDiferentes...` | `Entity<TId>.Equals` | comparação só pelo Id |
| 3 | `Estudo/...` → `ArmadilhaDoRecord...` | linha do `with` | o record aceita valor inválido |
| 4 | `Aggregates/ClienteTests` → `AdicionarConta_Duplicada...` | `Cliente.AdicionarConta` | invariante protegida pelo root |
| 5 | `Aggregates/SolicitacaoArquivoTests` → `Falhar_EReprocessar...` | cada método de transição | status, `Tentativas`, eventos |
| 6 | `IntegrationTests` → `Send_ComandoValido...` | `Mediator.Send`, `RequestHandlerWrapperImpl.Handle`, `RastreamentoBehavior`, `CriarSolicitacaoHandler` | caminho completo da "cebola" |
| 7 | `IntegrationTests` → `Send_MesmoClienteEData...` | `CriarSolicitacaoHandler` (`ExisteAtiva`) | regra entre aggregates na Application |
| 8 | `IntegrationTests` → `Publish_EventosDoAggregate...` | `AuditoriaHandler.Handle` | ponte Domain Event → Notification |

### 10.4 Exercícios (tente quebrar as regras)

1. Tente criar `Dinheiro` negativo sem usar `Criar`. Não compila? Ótimo — é o objetivo.
2. Tente fazer `cliente.Contas.Add(...)`. Por que não existe?
3. Mude `Conta.Criar` para `public` e escreva um teste que burla a regra de duplicidade. Depois desfaça.
4. Registre um segundo behavior no teste de integração e veja a ordem em `Rastro.Passos`.
5. Remova `var next = pipeline;` no wrapper e rode os testes do Mediator. Entenda o estouro.

---

## 11. Histórico de decisões (ADR resumido)

> Formato: **contexto → decisão → consequência**. Acrescente novas entradas no fim.

| # | Etapa | Decisão | Por quê | Consequência |
|---|---|---|---|---|
| 1 | 1 | SharedKernel separado | peças de DDD são genéricas | reutilizável, Domain fica enxuto |
| 2 | 2 | Mediator próprio | estudo + licença do MediatR | precisamos manter e testar |
| 3 | 4 | `Publish` com wrapper cacheado e `AggregateException` | sem reflection por chamada; falha isolada | todos os handlers rodam |
| 4 | 4 | DI idempotente (`TryAdd*`) | evitar handlers duplicados | `AddMediator` seguro de chamar 2x |
| 5 | 5 | Result para negócio, exceção para bug/infra | falha esperada deve ser explícita | assinaturas honestas, ProblemDetails na API |
| 6 | 5 | `Transacao` é aggregate próprio | volume e concorrência | referência por `ContaId` |
| 7 | 5 | `SolicitacaoArquivo` é o centro | idempotência, status, eventos | máquina de status no aggregate |
| 8 | 5 | Domain não depende do Mediator | Domain puro | ponte `DomainEventNotification` na Application |
| 9 | 6 | VO com regra = `class : ValueObject`; IDs = `record struct` | `record` permite burlar invariantes | só `Criar` constrói VO |
| 10 | 6 | Catálogo `DomainErrors` | códigos estáveis e testáveis | um lugar para todos os erros |
| 11 | 6 | Conversão implícita `Error → Result` | menos ruído no domínio | `return DomainErrors.X;` |
| 12 | 6 | `IDateTimeProvider` no domínio | testes determinísticos | relógio injetado nos métodos |
| 13 | 6 | Unicidade de solicitação ativa fica na Application | envolve vários aggregates | + índice único na etapa 9 |
| 14 | 6 | Projeto `FileForge.IntegrationTests` | ver as peças juntas e debugar | protótipo de handler; vira Testcontainers na etapa 17 |
| 15 | Review | `IDateTimeProvider.Hoje` no fuso do negócio (São Paulo) | às 22h em SP, em UTC já é amanhã: "data no futuro" era aceita | regras de calendário usam `Hoje`; `UtcNow` só para registrar instantes |
| 16 | Review | `DomainEvent` recebe `OccurredOnUtc` no construtor | ele lia `DateTime.UtcNow`, contrariando a decisão 12 | eventos determinísticos nos testes |
| 17 | Review | Validar todas as entradas **antes** de alterar o estado | exceção no meio deixava o aggregate pela metade | métodos começam com guardas, depois mutam |
| 18 | Review | `Publish` não agrega cancelamento; 1 falha = exceção original | cancelamento ficava escondido em `AggregateException` | `catch` específico para `OperationCanceledException` |
| 19 | Review | Dois handlers para o mesmo request = erro na inicialização | `TryAdd` descartava o segundo sem avisar | falha rápida (*fail fast*) na configuração do DI |
| 20 | Review | Todos os arquivos em UTF-8 | acentos quebrados em 2 arquivos | `.editorconfig` com `charset = utf-8` |
| 21 | Qualidade | `Directory.Build.props` com `TreatWarningsAsErrors` + `AnalysisLevel=latest-recommended` | qualidade garantida pelo compilador, não pela memória | o analisador já achou `ToString` dependente de cultura em `Dinheiro`/`DataReferencia` |
| 22 | Qualidade | Central Package Management (`Directory.Packages.props`) | versões repetidas em cada `.csproj` | uma versão por pacote na solução; `.csproj` só com referências |
| 23 | Qualidade | `global.json` + `nuget.config` com fonte única | build igual em qualquer máquina e no CI | fontes NuGet da máquina não interferem |
| 24 | Qualidade | Supressões de analisador documentadas no `.editorconfig` | CA1716/CA1711 não se aplicam (C#-only, convenção do Mediator) | toda exceção à regra tem justificativa escrita |
| 25 | CI | GitHub Actions: format → build Release → test + cobertura | feedback automático a cada push/PR | badge no README; cobertura no resumo do workflow |
| 26 | Observabilidade | `LogDefinitions` com `[LoggerMessage]` + `LoggingBehavior` | logs consistentes, performáticos e pesquisáveis por `EventId` | handlers não logam; exportadores (Serilog/OTel) só no host |
| 27 | Observabilidade | Falha de negócio = Warning; exceção = Error; cancelamento não é erro | alertas só para o que é bug de verdade | menos ruído em produção |
| 28 | IoC | `AddX()` por camada, sem projeto `CrossCutting.IoC` | cada camada registra o que é dela; composition root fica na API/Worker | `AddApplication()` testado com `ValidateOnBuild` |
| 29 | Testes | Testes de arquitetura com reflection + NetArchTest | as regras do guia deixam de depender de disciplina | teste de sanidade garante que os filtros não "passam no vazio" |
| 30 | Testes | Test Data Builders (`Um.X()`) | testes declaram só o que importa; builders usam as transições reais | mudança no construtor de um aggregate afeta um lugar só |
| 31 | 7 | Interfaces de repositório no Domain, por pasta de aggregate; implementação em `FileForge.Infrastructure` | Inversão de Dependência; Unit of Work e Outbox dividem o `DbContext` | sem projeto `Repository` isolado |
| 32 | 7 | `ITransacaoRepository` recebe `ContaId`s, não `ClienteId` | `Transacao` só conhece a conta; evitar um repositório que conhece outro aggregate | handler carrega o `Cliente` e repassa as contas; `IExtratoQuery` como evolução |
| 33 | 7 | Specifications (`SolicitacaoArquivoSpecs`) como `Expression` | a regra "ativa" precisa virar SQL no EF Core | mesma regra em memória e no banco |
| 34 | 8 | `IUsuarioAtual` + `[RequerPermissao]` + `AuthorizationBehavior` | Application sem JWT/HttpContext; permissão declarada no próprio command | `ClienteId` vem do token, não do corpo |
| 35 | 8 | Autorização por recurso no handler, respondendo 404 | anti-IDOR sem revelar a existência do recurso | `consultar-todos` para operador/admin |
| 36 | 8 | `ValidationBehavior` (FluentValidation) + `ValidationError` com lista | vários erros de entrada numa resposta só | formato na Application; regra de negócio no Domain |
| 37 | 8 | `IEventBus` na Application, chamado só pelo publicador do Outbox | evitar dupla escrita | handlers nunca publicam direto |
| 38 | Segurança | Defesa em camadas: identidade, dados e borda | uma camada só (JWT) não protege dados nem endpoints | rate limiting e mascaramento de documento entram na etapa 10 |
| 39 | Segurança | API só valida JWT (`iss`, `aud`, `exp`, `nbf`, assinatura RS256, clock skew 30 s); Keycloak emite | separar emissor de consumidor; padrão OIDC permite trocar de provedor | audience mapper no realm; testes geram tokens próprios |

---

## 12. Segurança: as três camadas

> Pergunta comum em entrevista: *"Descreva três medidas para proteger endpoints e dados corporativos."*
> Erro comum: responder três itens da **mesma** camada (token, permissões, scope). A resposta esperada cobre **camadas diferentes**.

### 12.1 Conceitos que costumam ser confundidos

| Termo | O que é | Exemplo |
|---|---|---|
| **JWT** | O token em si: cabeçalho + claims + assinatura | `eyJhbGciOi...` |
| **Validar o token** | Conferir assinatura, emissor (`iss`), audiência (`aud`) e expiração (`exp`) | `AddJwtBearer` faz isso |
| **Scope** | O que um **aplicativo cliente** pode fazer **em nome do usuário** (delegação OAuth) | o app mobile só pede `extratos.read` |
| **Role / permissão** | O que o **usuário** pode fazer | `operador` tem `extratos:reprocessar` |
| **Autorização por recurso** | Pode fazer isso **com este dado específico**? | a Ana só vê as solicitações dela |
| **IDOR** | Falha em que trocar um id na URL expõe dados de outra pessoa | `GET /solicitacoes/{id-do-bruno}` |

Scope e role se **somam**: o acesso só é liberado se o app tem o scope **e** o usuário tem a permissão.

### 12.2 As três camadas

**1. Identidade (autenticação e autorização)**
- Validar o JWT; tokens de vida curta.
- Scopes, roles e permissões com **menor privilégio**.
- Autorização por recurso (anti-IDOR).

**2. Dados**
- HTTPS/TLS em trânsito; criptografia em repouso (banco e bucket).
- Segredos fora do código (user-secrets em dev; Key Vault/variáveis de ambiente em produção).
- Minimizar exposição: mascarar CPF/CNPJ em logs e respostas (LGPD); nunca logar tokens.

**3. Borda (endpoints)**
- Validação de toda entrada.
- Rate limiting contra abuso e força bruta.
- CORS restrito e cabeçalhos de segurança.
- Erros padronizados (ProblemDetails) sem stack trace.
- Auditoria: logar acessos negados e alertar quando aumentarem.

### 12.3 Status no FileForge

| Medida | Camada | Status | Onde |
|---|---|---|---|
| Permissões declaradas no command | Identidade | ✅ | `[RequerPermissao]` + `AuthorizationBehavior` |
| Autorização por recurso (responde 404) | Identidade | ✅ | `ObterSolicitacaoHandler` |
| `ClienteId` vem do token, não do corpo | Identidade | ✅ | `CriarSolicitacaoCommand` + `IUsuarioAtual` |
| Validação de entrada | Borda | ✅ | `ValidationBehavior` |
| Log de acesso negado | Borda | ✅ | `LogDefinitions` |
| Validar JWT (`iss`, `aud`, `exp`, assinatura) | Identidade | ⏳ 10.1 | `AddJwtBearer` + Keycloak |
| Scopes × roles; *client credentials* do Worker | Identidade | ⏳ 10.2 | políticas do ASP.NET Core |
| HTTPS, CORS, cabeçalhos, ProblemDetails | Borda | ⏳ 10.2 | API |
| Rate limiting | Borda | ⏳ 10.2 | `AddRateLimiter` |
| Segredos fora do código | Dados | ⏳ 10.2 | user-secrets / Aspire |
| Mascarar CPF/CNPJ | Dados | ⏳ 10.3 | método no `Documento` + teste |
| Criptografia em repouso | Dados | ⏳ 9 / 12 | PostgreSQL e bucket |

### 12.4 O que a API verifica no JWT

O Keycloak **emite** o token; a API só **confere** e **usa**. São duas verificações em sequência:

1. **Autenticação** (o token é legítimo?) → feita pelo `AddJwtBearer` → falha = **401**.
2. **Autorização** (o que este usuário pode fazer?) → nossas regras sobre as claims → falha = **403** ou **404**.

#### Exemplo de token (payload decodificado)

```json
{
  "iss": "http://localhost:8080/realms/fileforge",
  "aud": "fileforge-api",
  "sub": "8f3c1a2e-...-ana",
  "azp": "fileforge-scalar",
  "exp": 1767312000,
  "nbf": 1767311700,
  "scope": "openid extratos.read extratos.write",
  "permissions": ["extratos:solicitar", "extratos:consultar"],
  "cliente_id": "a1b2c3d4-..."
}
```

#### 1. Autenticação (401)

| Item | O que confere | Por quê |
|---|---|---|
| Assinatura | Chave pública do Keycloak, localizada pelo `kid` | Ninguém altera o token sem invalidar a assinatura |
| Algoritmo (`alg`) | Só `RS256` | Bloqueia `alg: none` e troca por algoritmo fraco |
| Emissor (`iss`) | É o nosso realm | Rejeita tokens de outro servidor/realm |
| Audiência (`aud`) | Emitido para `fileforge-api` | Token de outro sistema não serve aqui |
| Expiração (`exp`) | Não venceu | Limita o uso de token vazado |
| Início (`nbf`) | Já pode ser usado | Rejeita tokens "do futuro" |
| Tolerância de relógio | 30 s (padrão é 5 min) | Pequena diferença de horário sem abrir janela grande |

> O Keycloak põe `aud: "account"` por padrão: é preciso um **audience mapper** no realm para incluir `fileforge-api`.

#### 2. Autorização (403/404)

| Claim | Uso | Falha | Onde |
|---|---|---|---|
| `scope` | O **aplicativo** pode fazer isso em nome do usuário? | 403 | Política da API (10.2) |
| `permissions` / roles | O **usuário** pode fazer isso? | 403 | `[RequerPermissao]` + `AuthorizationBehavior` |
| `cliente_id` | **De quem** são os dados | 404 (anti-IDOR) | `ObterSolicitacaoHandler`, `CriarSolicitacaoCommand` |
| `sub` | **Quem** fez a ação | — | `SolicitadoPor` e logs |
| `azp` | Qual aplicativo pediu o token | — | Auditoria (usuário × Worker) |

Scope **e** permissão se somam: os dois precisam estar presentes.

#### 3. O que o JWT não resolve sozinho

| Limitação | Tratamento |
|---|---|
| Não dá para revogar antes do `exp` | Vida curta (5 min) + refresh token |
| É assinado, **não criptografado** (qualquer um lê) | Nada sensível no token; só HTTPS; nunca logar o token |
| Permissão pode estar desatualizada | Vida curta; ações críticas consultam o estado atual |

<!-- Próximas entradas: Etapa 7 (repositórios + Unit of Work) ... -->
