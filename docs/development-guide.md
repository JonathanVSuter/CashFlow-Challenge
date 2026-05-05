# Guia de Desenvolvimento

Este guia cobre tudo que um desenvolvedor precisa para entender, rodar, testar e evoluir o projeto CashFlow Challenge.

---

## Sumário

1. [Pré-requisitos](#1-pré-requisitos)
2. [Configuração do ambiente](#2-configuração-do-ambiente)
3. [Entendendo a estrutura](#3-entendendo-a-estrutura)
4. [Fluxos principais](#4-fluxos-principais)
5. [Adicionando um novo endpoint](#5-adicionando-um-novo-endpoint)
6. [Adicionando um novo evento de domínio](#6-adicionando-um-novo-evento-de-domínio)
7. [Rodando os testes](#7-rodando-os-testes)
8. [Escrevendo novos testes](#8-escrevendo-novos-testes)
9. [Troubleshooting](#9-troubleshooting)
10. [Checklist para produção](#10-checklist-para-produção)

---

## 1. Pré-requisitos

| Ferramenta | Versão mínima | Link |
|---|---|---|
| .NET SDK | 8.0 | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Docker Desktop | 4.x | https://www.docker.com/products/docker-desktop/ |
| Git | 2.x | https://git-scm.com/ |
| IDE recomendada | Visual Studio 2022 / Rider / VS Code | — |

Verificar instalações:

```bash
dotnet --version   # deve retornar 8.x.x
docker --version   # deve retornar 4.x.x
```

---

## 2. Configuração do Ambiente

### Opção A: Docker Compose (mais simples)

```bash
# Clonar o repositório
git clone <url-do-repositorio>
cd CashFlow-Challenge

# Subir todos os serviços
docker compose up --build

# Verificar se está rodando
curl http://localhost:8080/health
```

### Opção B: Desenvolvimento local (mais rápido para iterar)

```bash
# 1. Subir apenas infraestrutura
docker run -d \
  --name cashflow-pg \
  -e POSTGRES_USER=cashflow \
  -e POSTGRES_PASSWORD=cashflow \
  -e POSTGRES_DB=cashflow \
  -p 5432:5432 \
  postgres:16-alpine

docker run -d \
  --name cashflow-rmq \
  -e RABBITMQ_DEFAULT_USER=cashflow \
  -e RABBITMQ_DEFAULT_PASS=cashflow \
  -p 5672:5672 \
  -p 15672:15672 \
  rabbitmq:3-management-alpine

# 2. Rodar a API (terminal 1)
cd src/CashFlow.Api
dotnet run

# 3. Rodar o Worker (terminal 2)
cd src/CashFlow.Worker
dotnet run
```

### Verificar o ambiente

```bash
# API respondendo
curl http://localhost:8080/health

# RabbitMQ Management
open http://localhost:15672
# login: cashflow / cashflow

# Swagger UI
open http://localhost:8080/swagger
```

---

## 3. Entendendo a Estrutura

### Onde cada responsabilidade vive

| Você quer... | Arquivo |
|---|---|
| Criar/modificar entidade de domínio | `src/CashFlow.Domain/Entities/` |
| Adicionar regra de negócio | Dentro da entidade (ex: `DailyBalance.Apply`) |
| Criar caso de uso (escrita) | `src/CashFlow.Application/Features/.../Commands/` |
| Criar caso de uso (leitura) | `src/CashFlow.Application/Features/.../Queries/` |
| Modificar como dados são salvos | `src/CashFlow.Infrastructure/Persistence/ApplicationDbContext.cs` |
| Modificar mensageria | `src/CashFlow.Infrastructure/Messaging/` |
| Adicionar endpoint HTTP | `src/CashFlow.Api/Controllers/` |
| Configurar DI da Application | `src/CashFlow.Application/DependencyInjection.cs` |
| Configurar DI da Infrastructure | `src/CashFlow.Infrastructure/DependencyInjection.cs` |

### Regra de ouro

> **Nunca importe `Infrastructure` ou `Api` dentro de `Domain` ou `Application`.**

O domínio não sabe que existe EF Core, RabbitMQ ou ASP.NET. Ele conhece apenas suas próprias classes e interfaces definidas na camada de Application.

---

## 4. Fluxos Principais

### Criar um lançamento (POST /api/entries)

```
1. EntriesController.Create(request)
   │
2. CreateCashEntryCommand(type, amount, description, occurredAt)
   │
3. CreateCashEntryCommandHandler.HandleAsync(command)
   │
   ├─ new CashEntry(...)         ← validações no construtor da entidade
   ├─ new CashEntryCreatedEvent  ← evento de domínio
   ├─ new OutboxMessage(payload) ← serializa evento para Outbox
   │
   ├─ db.CashEntries.Add(entry)
   ├─ db.OutboxMessages.Add(outbox)
   └─ db.SaveChangesAsync()      ← persiste atomicamente
   │
4. Retorna CashEntryDto → 201 Created

5. [Background] OutboxPublisherBackgroundService
   ├─ Lê outbox_messages WHERE status=Pending
   ├─ BasicPublish → RabbitMQ
   └─ UPDATE status=Published

6. [Background] CashEntryCreatedConsumerHostedService (Worker)
   ├─ Consome mensagem do RabbitMQ
   ├─ UPSERT daily_balances
   ├─ cache.Remove(...)
   └─ BasicAck
```

### Consultar saldo diário (GET /api/daily-balances/{date})

```
1. DailyBalancesController.GetByDate(date)
   │
2. GetDailyBalanceByDateQuery(date)
   │
3. GetDailyBalanceByDateQueryHandler.HandleAsync(query)
   │
   ├─ cache.TryGetValue("daily-balance:2026-05-05")
   │   ├─ HIT  → retorna DTO cacheado (< 1ms)
   │   └─ MISS → SELECT daily_balances WHERE date=?
   │              → cache.Set(dto, TTL=30s)
   └─ Retorna DailyBalanceDto → 200 OK
      (ou null → 404 Not Found)
```

---

## 5. Adicionando um Novo Endpoint

Exemplo: `GET /api/entries/{id}` — buscar lançamento por ID.

### Passo 1: Criar a Query

```csharp
// src/CashFlow.Application/Features/CashEntries/Queries/GetCashEntryById/GetCashEntryByIdQuery.cs
namespace CashFlow.Application.Features.CashEntries.Queries.GetCashEntryById;

public sealed record GetCashEntryByIdQuery(Guid Id) : IQuery<CashEntryDto?>;
```

### Passo 2: Criar o Handler

```csharp
// src/CashFlow.Application/Features/CashEntries/Queries/GetCashEntryById/GetCashEntryByIdQueryHandler.cs
public sealed class GetCashEntryByIdQueryHandler : IQueryHandler<GetCashEntryByIdQuery, CashEntryDto?>
{
    private readonly IApplicationDbContext _dbContext;
    public GetCashEntryByIdQueryHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<CashEntryDto?> HandleAsync(GetCashEntryByIdQuery query, CancellationToken cancellationToken)
    {
        var entry = await _dbContext.CashEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        return entry is null
            ? null
            : new CashEntryDto(entry.Id, entry.Type, entry.Amount, entry.Description, entry.OccurredAt, entry.CreatedAt);
    }
}
```

### Passo 3: Registrar no DI

```csharp
// src/CashFlow.Application/DependencyInjection.cs
services.AddScoped<IQueryHandler<GetCashEntryByIdQuery, CashEntryDto?>, GetCashEntryByIdQueryHandler>();
```

### Passo 4: Adicionar o endpoint no Controller

```csharp
// src/CashFlow.Api/Controllers/EntriesController.cs
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetById(
    [FromServices] IQueryHandler<GetCashEntryByIdQuery, CashEntryDto?> handler,
    [FromRoute] Guid id,
    CancellationToken cancellationToken)
{
    var result = await handler.HandleAsync(new GetCashEntryByIdQuery(id), cancellationToken);
    return result is null ? NotFound() : Ok(result);
}
```

### Passo 5: Escrever o teste

```csharp
// tests/CashFlow.UnitTests/Application/GetCashEntryByIdHandlerTests.cs
[Fact]
public async Task Should_Return_Null_When_Entry_Not_Found()
{
    var dbContext = Substitute.For<IApplicationDbContext>();
    // ... configurar mock
    var handler = new GetCashEntryByIdQueryHandler(dbContext);
    var result = await handler.HandleAsync(new GetCashEntryByIdQuery(Guid.NewGuid()), default);
    result.Should().BeNull();
}
```

---

## 6. Adicionando um Novo Evento de Domínio

Exemplo: Notificar quando um lançamento for estornado.

### Passo 1: Criar o evento de domínio

```csharp
// src/CashFlow.Domain/Events/CashEntryReversedEvent.cs
namespace CashFlow.Domain.Events;

public sealed record CashEntryReversedEvent(
    Guid OriginalEntryId,
    Guid ReversalEntryId,
    DateTime ReversedAt);
```

### Passo 2: Criar o comando

```csharp
// src/CashFlow.Application/Features/CashEntries/Commands/ReverseCashEntry/ReverseCashEntryCommand.cs
public sealed record ReverseCashEntryCommand(Guid EntryId) : ICommand<CashEntryDto>;
```

### Passo 3: Implementar o handler

```csharp
public sealed class ReverseCashEntryCommandHandler : ICommandHandler<ReverseCashEntryCommand, CashEntryDto>
{
    private readonly IApplicationDbContext _dbContext;
    public ReverseCashEntryCommandHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<CashEntryDto> HandleAsync(ReverseCashEntryCommand command, CancellationToken cancellationToken)
    {
        var original = await _dbContext.CashEntries.FindAsync(command.EntryId, cancellationToken)
            ?? throw new InvalidOperationException("Entry not found.");

        // Cria lançamento de estorno (tipo oposto)
        var reversalType = original.Type == EntryType.Credit ? EntryType.Debit : EntryType.Credit;
        var reversal = new CashEntry(reversalType, original.Amount, $"Estorno: {original.Description}", DateTime.UtcNow);

        var evt = new CashEntryReversedEvent(original.Id, reversal.Id, DateTime.UtcNow);
        var outbox = new OutboxMessage(nameof(CashEntryReversedEvent), JsonSerializer.Serialize(evt));

        _dbContext.CashEntries.Add(reversal);
        _dbContext.OutboxMessages.Add(outbox);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CashEntryDto(reversal.Id, reversal.Type, reversal.Amount, reversal.Description, reversal.OccurredAt, reversal.CreatedAt);
    }
}
```

### Passo 4: Criar consumidor no Worker

```csharp
// src/CashFlow.Infrastructure/Messaging/ — adicionar handler para o novo evento
// O Worker precisa processar CashEntryReversedEvent da mesma forma que processa CashEntryCreatedEvent
```

---

## 7. Rodando os Testes

```bash
# Todos os testes
dotnet test

# Com verbosidade
dotnet test --logger "console;verbosity=detailed"

# Apenas um projeto de teste
dotnet test tests/CashFlow.UnitTests/

# Apenas um arquivo/classe
dotnet test --filter "FullyQualifiedName~DailyBalanceTests"

# Com cobertura de código (requer coverlet)
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Gerar relatório HTML (requer reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"coverage/**/*.xml" -targetdir:"coverage/html" -reporttypes:Html
open coverage/html/index.html
```

---

## 8. Escrevendo Novos Testes

### Testes de domínio (puro, sem mocks)

```csharp
[Fact]
public void DailyBalance_Should_Compute_Net_Balance()
{
    // Arrange
    var balance = new DailyBalance(new DateOnly(2026, 5, 5));

    // Act
    balance.Apply(EntryType.Credit, 1000m);
    balance.Apply(EntryType.Credit, 500m);
    balance.Apply(EntryType.Debit, 300m);

    // Assert
    balance.TotalCredits.Should().Be(1500m);
    balance.TotalDebits.Should().Be(300m);
    balance.Balance.Should().Be(1200m);
}
```

### Testes de handlers (com mock do DbContext)

Para testar handlers que dependem do DbContext, use `NSubstitute` ou `Moq` com `InMemory` provider:

```bash
# Adicionar pacotes ao projeto de teste
dotnet add tests/CashFlow.UnitTests/ package Microsoft.EntityFrameworkCore.InMemory
dotnet add tests/CashFlow.UnitTests/ package NSubstitute
```

```csharp
// Usando InMemory Database
private static ApplicationDbContext CreateInMemoryContext()
{
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;
    return new ApplicationDbContext(options);
}

[Fact]
public async Task CreateCashEntryHandler_Should_Persist_Entry_And_OutboxMessage()
{
    // Arrange
    using var db = CreateInMemoryContext();
    var handler = new CreateCashEntryCommandHandler(db);
    var command = new CreateCashEntryCommand(EntryType.Credit, 100m, "Venda", DateTime.UtcNow);

    // Act
    var result = await handler.HandleAsync(command, CancellationToken.None);

    // Assert
    db.CashEntries.Should().HaveCount(1);
    db.OutboxMessages.Should().HaveCount(1);
    db.OutboxMessages.First().Status.Should().Be(OutboxMessageStatus.Pending);
    result.Amount.Should().Be(100m);
}
```

### Boas práticas de teste

- **Arrange / Act / Assert**: estruture cada teste nestas três seções.
- **Um assert por teste** (ou um grupo coeso): facilita identificar a falha.
- **Nomes descritivos**: `Should_[Resultado]_When_[Condição]`.
- **Dados isolados**: cada teste cria seu próprio estado (sem compartilhar banco entre testes).
- **Testes de happy path E error path**: valide tanto o caminho feliz quanto as exceções esperadas.

---

## 9. Troubleshooting

### API não inicia no Docker

```bash
# Ver logs detalhados
docker compose logs api

# Problema comum: PostgreSQL ainda não está pronto
# Solução: a API aguarda o healthcheck do postgres via depends_on
# Se persistir, verificar se a porta 5432 está ocupada
lsof -i :5432
```

### Worker não consome mensagens

```bash
# Verificar se a fila existe no RabbitMQ
open http://localhost:15672
# Ir em Queues → cash-entry-created

# Ver logs do worker
docker compose logs worker

# Causa comum: topologia não inicializada
# O RabbitMqTopologyInitializer cria exchange/fila no startup
# Reiniciar o worker resolve se o RabbitMQ estava indisponível no início
docker compose restart worker
```

### Consolidado não atualiza

O saldo diário é atualizado **assincronamente**. Após criar um lançamento:
1. Aguardar o Outbox Publisher (loop de 1s) publicar no RabbitMQ.
2. Aguardar o Worker consumir e persistir o saldo.
3. O cache expira em 30s — forçar miss ao consultar após esse tempo.

```bash
# Ver mensagens na fila
# RabbitMQ Management → Queues → cash-entry-created → Get Messages

# Ver Outbox
curl http://localhost:8080/api/outbox
```

### Erro de migração de banco

```bash
# EnsureCreated cria as tabelas automaticamente no startup
# Se o schema mudar, dropar o banco e recriar:
docker compose down -v
docker compose up --build
```

### JWT expirado

```bash
# Token expira em 2h
# Obter novo token:
curl -X POST http://localhost:8080/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'
```

---

## 10. Checklist para Produção

Antes de levar este código para produção, os seguintes itens devem ser endereçados:

### Banco de dados
- [ ] Substituir `EnsureCreated` por **EF Migrations** formais
- [ ] Adicionar **connection pooling** configurado (Npgsql default: max 100)
- [ ] Configurar **réplica de leitura** para queries de consolidado
- [ ] Revisar índices com base no volume real de dados

### Cache
- [ ] Substituir **MemoryCache** por **Redis** (cache distribuído entre instâncias)
- [ ] Configurar TTL adequado por tipo de dado
- [ ] Implementar **cache warming** no startup para datas recentes

### Mensageria
- [ ] Configurar **Dead Letter Queue** no RabbitMQ
- [ ] Implementar **idempotência** no Worker (tabela `processed_events`)
- [ ] Configurar **prefetch** adequado baseado no throughput esperado
- [ ] Adicionar **DLQ alerts** quando mensagens acumularem

### Segurança
- [ ] Substituir autenticação hardcoded por **Identity Provider externo**
- [ ] Implementar **refresh tokens**
- [ ] Habilitar **HTTPS obrigatório**
- [ ] Configurar **CORS** restritivo
- [ ] Mover secrets para **secrets manager** (Key Vault / Secrets Manager)
- [ ] Revisar rate limits por endpoint e perfil de usuário

### Observabilidade
- [ ] Adicionar **OpenTelemetry** (traces + metrics)
- [ ] Configurar **Serilog** com structured logging para Elasticsearch
- [ ] Implementar **health checks** detalhados (banco, RabbitMQ, cache)
- [ ] Criar **dashboards Grafana** com alertas configurados

### Testes
- [ ] Aumentar cobertura de **testes unitários** para >80%
- [ ] Adicionar **testes de integração** com TestContainers
- [ ] Adicionar **testes de carga** com k6 ou NBomber (validar 50 req/s)
- [ ] Implementar **testes de contrato** de API (Pact)

### DevOps
- [ ] Configurar **pipeline CI/CD** (GitHub Actions / Azure DevOps)
- [ ] Adicionar **análise estática** (SonarQube / Roslyn analyzers)
- [ ] Configurar **vulnerability scanning** de imagens Docker
- [ ] Implementar **blue-green deployment** ou **canary releases**
