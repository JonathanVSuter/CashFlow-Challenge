# Arquitetura do Sistema CashFlow

## Estilo Arquitetural

**Monólito modular com Worker separado**, seguindo os princípios da **Clean Architecture** (Robert C. Martin).

A separação em camadas garante:
- **Inversão de dependências**: a camada de domínio não conhece infraestrutura.
- **Testabilidade**: casos de uso testáveis em isolamento via mocks.
- **Evolução incremental**: substituição de componentes externos sem alterar o domínio.

---

## Camadas

```
┌──────────────────────────────────────────────────────┐
│                      CashFlow.Api                    │  ← Entry point HTTP
│              CashFlow.Worker                         │  ← Entry point Background
├──────────────────────────────────────────────────────┤
│               CashFlow.Infrastructure                │  ← EF Core, RabbitMQ, Cache
├──────────────────────────────────────────────────────┤
│               CashFlow.Application                   │  ← Casos de uso (CQRS)
├──────────────────────────────────────────────────────┤
│                  CashFlow.Domain                     │  ← Entidades, Regras de negócio
└──────────────────────────────────────────────────────┘
```

### Regra de dependência

```
Api/Worker → Infrastructure → Application → Domain
                                    ↑
                               (via interfaces)
```

O `Domain` não depende de nada externo.
O `Application` depende apenas do `Domain` e de abstrações (interfaces).
O `Infrastructure` implementa as abstrações e referencia `Application` + `Domain`.
Os entry points (`Api`, `Worker`) referenciam todas as camadas para fazer composição raiz (DI).

---

## Domínio

### Entidades

#### `CashEntry` (Aggregate Root)

Representa um lançamento no fluxo de caixa.

| Propriedade | Tipo | Regra |
|---|---|---|
| `Id` | `Guid` | Gerado no construtor |
| `Type` | `EntryType` | `Credit` ou `Debit` |
| `Amount` | `decimal` | Deve ser > 0 |
| `Description` | `string` | Obrigatória, máx. 250 chars |
| `OccurredAt` | `DateTime` | Data/hora do lançamento |
| `CreatedAt` | `DateTime` | Momento da criação do registro |

#### `DailyBalance` (Projeção/Read Model)

Representa o saldo consolidado de um dia.

| Propriedade | Tipo | Regra |
|---|---|---|
| `Date` | `DateOnly` | Único por data |
| `TotalCredits` | `decimal` | Soma de créditos do dia |
| `TotalDebits` | `decimal` | Soma de débitos do dia |
| `Balance` | `decimal` | `TotalCredits - TotalDebits` (computed) |
| `LastUpdatedAt` | `DateTime` | Atualizado a cada Apply |

Método `Apply(EntryType, decimal)`: aplica um lançamento ao saldo.

#### `OutboxMessage`

Implementa o Outbox Pattern.

| Propriedade | Tipo | Regra |
|---|---|---|
| `Type` | `string` | Nome do tipo do evento |
| `Payload` | `string` | JSON serializado do evento |
| `Status` | `enum` | `Pending → Published` ou `Pending → Failed` |
| `Attempts` | `int` | Contador de tentativas; `Failed` após 5 |
| `Error` | `string?` | Última mensagem de erro |

---

## Padrão CQRS

Cada operação segue o contrato:

```csharp
// Comando (escrita)
ICommandHandler<TCommand, TResult>
    HandleAsync(TCommand, CancellationToken) → Task<TResult>

// Query (leitura)
IQueryHandler<TQuery, TResult>
    HandleAsync(TQuery, CancellationToken) → Task<TResult>
```

| Operação | Tipo | Handler |
|---|---|---|
| Criar lançamento | Command | `CreateCashEntryCommandHandler` |
| Listar lançamentos | Query | `GetCashEntriesQueryHandler` |
| Consultar saldo diário | Query | `GetDailyBalanceByDateQueryHandler` |
| Listar outbox | Query | `GetOutboxMessagesQueryHandler` |

---

## Outbox Pattern

Garante atomicidade entre a persistência do lançamento e a publicação do evento.

```
┌─ Transação Atômica ──────────────────────────────────┐
│  INSERT INTO cash_entries (...)                       │
│  INSERT INTO outbox_messages (status = 'Pending')     │
└───────────────────────────────────────────────────────┘
         ↓ (background loop a cada 1s)
┌─ OutboxPublisherBackgroundService ───────────────────┐
│  SELECT * FROM outbox_messages WHERE status='Pending' │
│  BasicPublish → RabbitMQ (persistent=true)            │
│  UPDATE outbox_messages SET status='Published'        │
└───────────────────────────────────────────────────────┘
```

Se o RabbitMQ estiver indisponível:
- O lançamento já foi salvo com sucesso.
- A mensagem fica com status `Pending`.
- Quando o RabbitMQ voltar, o publicador retoma automaticamente.
- Após 5 falhas consecutivas, a mensagem passa para `Failed` (Dead Letter manual).

---

## Mensageria

### Topologia RabbitMQ

```
Exchange: cashflow.direct (Direct, durable)
    └── Routing Key: cash-entry-created
            └── Queue: cash-entry-created (durable, autoDelete=false)
                    └── Consumer: CashEntryCreatedConsumerHostedService
```

### Garantias de entrega

| Propriedade | Configuração |
|---|---|
| Exchange durável | `durable: true` |
| Fila durável | `durable: true` |
| Mensagens persistentes | `props.Persistent = true` |
| Prefetch | `BasicQos(0, 10, false)` |
| Confirmação | `BasicAck` apenas após persistir no banco |
| Falha | `BasicNack(requeue: true)` para tentativa posterior |

### Evento publicado

```json
{
  "entryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "type": "Credit",
  "amount": 150.75,
  "occurredAt": "2026-05-05T10:30:00Z"
}
```

---

## Cache

O endpoint `GET /api/daily-balances/{date}` usa **MemoryCache** com TTL de 30 segundos.

```
Request → TryGetValue(key)
            ├─ HIT  → retorna DTO cacheado
            └─ MISS → consulta PostgreSQL → Set(key, dto, TTL=30s) → retorna DTO
```

Quando o Worker processa um novo evento, invalida o cache da data correspondente:

```csharp
cache.Remove($"daily-balance:{date:yyyy-MM-dd}");
```

Para produção com múltiplas instâncias de API, substituir por **Redis** (cache distribuído).

---

## Segurança

| Mecanismo | Implementação |
|---|---|
| Autenticação | JWT HS256 (Microsoft.AspNetCore.Authentication.JwtBearer) |
| Autorização | `[Authorize]` no endpoint de escrita |
| Rate Limiting | Fixed Window: 100 req/min por IP |
| Secrets | Via variáveis de ambiente no Docker Compose |
| HTTPS | Configurado no `launchSettings.json`; obrigatório em produção |

O endpoint de autenticação (`POST /api/auth/token`) é demonstrativo.
Em produção: integrar com Identity Provider externo (Keycloak, Azure AD B2C, Auth0).

---

## Banco de Dados

### Tabelas

```sql
-- Lançamentos
CREATE TABLE cash_entries (
    id          UUID        PRIMARY KEY,
    type        INTEGER     NOT NULL,        -- 0=Credit, 1=Debit
    amount      NUMERIC(18,2) NOT NULL,
    description VARCHAR(250) NOT NULL,
    occurred_at TIMESTAMP   NOT NULL,
    created_at  TIMESTAMP   NOT NULL
);
CREATE INDEX ix_cash_entries_occurred_at ON cash_entries(occurred_at);

-- Saldo consolidado por dia
CREATE TABLE daily_balances (
    id              UUID        PRIMARY KEY,
    date            DATE        NOT NULL UNIQUE,
    total_credits   NUMERIC(18,2) NOT NULL DEFAULT 0,
    total_debits    NUMERIC(18,2) NOT NULL DEFAULT 0,
    last_updated_at TIMESTAMP   NOT NULL
);
CREATE UNIQUE INDEX ix_daily_balances_date ON daily_balances(date);

-- Outbox de eventos
CREATE TABLE outbox_messages (
    id           UUID         PRIMARY KEY,
    type         VARCHAR(200) NOT NULL,
    payload      TEXT         NOT NULL,
    created_at   TIMESTAMP    NOT NULL,
    published_at TIMESTAMP,
    attempts     INTEGER      NOT NULL DEFAULT 0,
    error        TEXT,
    status       INTEGER      NOT NULL        -- 1=Pending, 2=Published, 3=Failed
);
CREATE INDEX ix_outbox_messages_status     ON outbox_messages(status);
CREATE INDEX ix_outbox_messages_created_at ON outbox_messages(created_at);
```

---

## Diagrama de Implantação (Produção Recomendada)

```
Internet
    │
    ▼
┌─────────────────┐
│   API Gateway   │  ← WAF, TLS termination, auth offload
│  (Nginx/YARP)   │
└────────┬────────┘
         │ HTTP
    ┌────┴────┐
    │         │
┌───▼──┐  ┌──▼───┐   ← N instâncias stateless (auto-scale)
│ API  │  │ API  │
│  :1  │  │  :2  │
└───┬──┘  └──┬───┘
    └────┬───┘
         │
┌────────┼────────────────────────┐
│        │                        │
▼        ▼                        ▼
PostgreSQL   RabbitMQ Cluster   Redis
(Primary +   (3 nodes + HA)    (Cache
 Replica)                       distribuído)
         │
    ┌────┘
    ▼
┌──────────┐
│  Worker  │   ← N instâncias (horizontal scale com idempotência)
│    :1    │
├──────────┤
│  Worker  │
│    :2    │
└──────────┘
```

---

## Trade-offs e Limitações Conhecidas

| Item | Situação atual | Ideal em produção |
|---|---|---|
| Schema | `EnsureCreated` no startup | Migrations EF formais |
| Cache | MemoryCache (por instância) | Redis distribuído |
| Dead Letter | Coluna `Status=Failed` | DLQ no RabbitMQ + alertas |
| Idempotência | Não implementada no Worker | Tabela `processed_events` |
| Auth | JWT demo (usuário hardcoded) | Identity Provider externo |
| Observabilidade | Health check básico | OpenTelemetry + Prometheus + Grafana |
| Testes de integração | Não implementados | TestContainers (PostgreSQL + RabbitMQ reais) |
