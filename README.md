# FileForge

> Projeto de **estudos** em .NET 9. Não é um projeto de empresa e não tem levantamento de requisitos formal: a ideia é aplicar o estado da arte em arquitetura, design patterns e ferramentas de mercado em um único projeto completo.

## ?? Objetivo

Uma aplicação que:

1. Recebe uma **solicitação** com `ClienteId` + `DataReferencia`;
2. **Busca dados** do cliente no banco (domínio fictício, ex.: transações/movimentações);
3. **Processa** esses dados (agregações, regras, validações);
4. **Gera um arquivo sequencial** (layout posicional/fixed-width, estilo CNAB: header, detalhes, trailer);
5. **Envia o arquivo para um bucket** (S3/MinIO ou Azure Blob/Azurite);
6. Notifica a conclusão (evento).

O processamento é **assíncrono**: a API aceita o pedido (`202 Accepted`), e workers processam em background.

```
Cliente ??? API ??? (Outbox) ??? Broker ??? Worker ??? SQL/NoSQL ??? Gerador de Arquivo ??? Bucket
                ?                                 ?
                ???? Redis (idempotência/cache)   ???? Evento "ArquivoGerado"
```

## ?? Princípios e práticas

| Tema | O que aplicar |
|---|---|
| Arquitetura | Clean Architecture, DDD (Entities, Value Objects, Aggregates, Domain Events), Vertical Slices na Application |
| Código | Clean Code, SOLID, CQRS |
| Mediator | **Próprio** (`FileForge.Mediator`), sem MediatR, com Pipeline Behaviors |
| Erros | **Result Pattern** (`Result`/`Result<T>`) em vez de exceções para fluxo de negócio |
| Persistência | **Unit of Work**, Repository, EF Core (SQL), MongoDB (NoSQL) |
| Mensageria | **Outbox Pattern**, Inbox/consumidor idempotente, RabbitMQ (ou Kafka) |
| Idempotência | `Idempotency-Key` na API + chave natural (`ClienteId + Data`) + Redis/locks distribuídos |
| Resiliência | **Polly** (retry, circuit breaker, timeout) via `Microsoft.Extensions.Http.Resilience` |
| Cache | **Redis** (cache distribuído, locks, idempotência) |
| Observabilidade | **OpenTelemetry** (traces, métricas, logs), Serilog, Health Checks, Grafana/Seq/Jaeger |
| Escalabilidade | Workers stateless, escala horizontal, processamento por streaming (`IAsyncEnumerable`), backpressure com `Channels` |
| Validação | FluentValidation via Pipeline Behavior |
| Testes | xUnit, NSubstitute, FluentAssertions, Testcontainers, testes de arquitetura (NetArchTest) |
| Infra | Docker Compose, **.NET Aspire** (orquestração local), CI com GitHub Actions |
| Segurança | Autenticação JWT, rate limiting, secrets fora do código |

## ?? Estrutura (alvo)

```
src/
  BuildingBlocks/
    FileForge.SharedKernel      ? Entity, ValueObject, IDomainEvent, Result, IDateTimeProvider
    FileForge.Mediator          ? IMediator, IRequest, INotification, IPipelineBehavior, DI
  FileForge.Domain              ? Cliente, Transacao, SolicitacaoArquivo (aggregate), eventos
  FileForge.Application         ? Commands/Queries, Handlers, Behaviors, abstrações (IFileStorage, IUnitOfWork)
  FileForge.Infrastructure      ? EF Core, MongoDB, Redis, Outbox, Bucket, Polly, Broker
  FileForge.Api                 ? Minimal APIs, idempotência, versionamento, ProblemDetails
  FileForge.Worker              ? Consumidores, processador de outbox, gerador de arquivo
  FileForge.AppHost             ? .NET Aspire
tests/
  *.UnitTests / *.IntegrationTests / *.ArchitectureTests   ?
```

## ?? Fluxo principal

1. `POST /solicitacoes-arquivo` `{ clienteId, dataReferencia }` + header `Idempotency-Key`.
2. Handler cria o aggregate `SolicitacaoArquivo` (status `Pendente`) e grava, **na mesma transação** (UoW), a mensagem no **Outbox**.
3. Um processador de outbox publica `SolicitacaoArquivoCriada` no broker (com Polly).
4. O worker consome a mensagem de forma idempotente, busca os dados (SQL + leitura NoSQL), processa e gera o arquivo via streaming.
5. Faz upload para o bucket, atualiza o status (`Concluida`/`Falhou`) e publica `ArquivoGerado`.
6. `GET /solicitacoes-arquivo/{id}` consulta o status (com cache Redis).

## ??? Roadmap

- [x] SharedKernel (Entity, ValueObject, Domain Events, Result)
- [x] Mediator próprio com pipeline behaviors
- [x] Testes do Mediator e do Result
- [ ] Domain: aggregates, value objects, eventos
- [ ] Application: command de criação + query de status + behaviors (validação, logging)
- [ ] Infrastructure: EF Core + Unit of Work + Outbox
- [ ] API com idempotência
- [ ] Worker + geração do arquivo sequencial
- [ ] Bucket (MinIO/Azurite) + Polly
- [ ] Redis (cache, idempotência, lock distribuído)
- [ ] MongoDB (read model / logs de processamento)
- [ ] OpenTelemetry + Health Checks + dashboards
- [ ] Docker Compose / .NET Aspire
- [ ] Testcontainers + testes de arquitetura
- [ ] CI (GitHub Actions)

## ?? Diário de decisões

- **Mediator próprio:** construído para estudar o padrão (e porque o MediatR passou a ter licença comercial).
- **Result Pattern:** falhas de negócio são valores, não exceções.
- **Assíncrono por padrão:** a API não gera o arquivo; ela só registra a intenção.

> Atualize o roadmap e o diário a cada etapa concluída.
