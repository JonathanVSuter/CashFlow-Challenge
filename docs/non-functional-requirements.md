# Requisitos Não Funcionais

## Contexto dos Requisitos

O desafio define dois requisitos não funcionais explícitos:

1. **O serviço de controle de lançamento não deve ficar indisponível se o sistema de consolidado diário cair.**
2. **Em dias de picos, o serviço de consolidado diário recebe 50 requisições por segundo, com no máximo 5% de perda de requisições.**

---

## 1. Disponibilidade — Independência entre Serviços

### Requisito
O serviço de **lançamentos** deve permanecer disponível independentemente do estado do serviço de **consolidação**.

### Solução implementada

```
┌──────────────────────────────────────────────────────────┐
│  API (Lançamentos)                                        │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ CreateCashEntryCommandHandler                       │ │
│  │   1. INSERT cash_entries    ┐                       │ │
│  │   2. INSERT outbox_messages ┘  TRANSAÇÃO ATÔMICA    │ │
│  └─────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
         ↓ Async (background, não bloqueia a API)
┌──────────────────────────────────────────────────────────┐
│  OutboxPublisherBackgroundService                         │
│  Loop a cada 1s → publica no RabbitMQ                    │
└──────────────────────────────────────────────────────────┘
         ↓
┌──────────────────────────────────────────────────────────┐
│  Worker (Consolidação)                                    │
│  Consome fila → UPSERT daily_balances                    │
└──────────────────────────────────────────────────────────┘
```

### Cenário de falha do Worker

| Evento | Comportamento |
|---|---|
| Worker cai | API continua aceitando lançamentos normalmente |
| Mensagens no RabbitMQ | Permanecem na fila durável (até o Worker voltar) |
| Mensagens não publicadas | Ficam com `status=Pending` na tabela `outbox_messages` |
| Worker volta | Consome mensagens pendentes; saldo é atualizado |
| Consistência final | Eventual (o saldo pode estar desatualizado enquanto o Worker estiver fora) |

### SLA — Lançamentos

| Métrica | Meta |
|---|---|
| Disponibilidade | 99,9% (8,7h de downtime/ano) |
| Latência p95 | < 100ms |
| Latência p99 | < 200ms |

---

## 2. Escalabilidade — 50 req/s no Consolidado

### Requisito
O serviço de consolidado diário deve suportar **50 requisições por segundo** com **máximo 5% de perda** (≤ 2,5 req/s descartadas).

### Análise de capacidade

O endpoint de consolidado (`GET /api/daily-balances/{date}`) é **somente leitura** e altamente cacheável:

- O saldo de um dia só muda quando novos lançamentos são processados pelo Worker.
- Mesmo durante picos, a maioria das leituras pode ser servida pelo cache.

### Estratégia em camadas

#### Camada 1: Cache em Memória (TTL 30s)

```
50 req/s → MemoryCache → resposta < 1ms
               │
               └── MISS (raro): PostgreSQL → Set cache → resposta < 10ms
```

Com TTL de 30s, em um cenário de 50 req/s para a mesma data:
- **1 requisição/30s** atinge o banco (miss de cache).
- **~1499 requisições/30s** são servidas do cache.
- Taxa de hit esperada: **> 99,9%** em picos.

#### Camada 2: Rate Limiting (proteção)

```csharp
// FixedWindowRateLimiter: 100 req/min por IP
// Previne que um único cliente monopolize os recursos
```

#### Camada 3: Escalabilidade horizontal da API

```
Load Balancer
     ├── API Instance 1 (MemoryCache próprio)
     ├── API Instance 2 (MemoryCache próprio)
     └── API Instance N (MemoryCache próprio)
```

> **Nota:** Com múltiplas instâncias, o MemoryCache local pode resultar em cache miss na primeira requisição de cada instância. Para eliminar esse risco, usar **Redis** como cache distribuído.

#### Camada 4: Índice no banco

```sql
CREATE UNIQUE INDEX ix_daily_balances_date ON daily_balances(date);
```

Garante que a query `WHERE date = ?` seja O(log n) independentemente do volume de dados.

### Meta de tolerância a falhas (5%)

| Cenário | Resposta |
|---|---|
| Worker lento/sobrecarregado | API não é afetada (leitura vai direto ao banco) |
| Banco lento | Cache absorve as requisições (TTL 30s) |
| Rate limit atingido | `429 Too Many Requests` (não conta como perda de dado) |
| Worker fora | Saldo pode estar desatualizado, mas API retorna último valor consolidado |

---

## 3. Resiliência — Outbox + Fila Durável

### Garantias

| Componente | Garantia |
|---|---|
| Outbox Pattern | Nenhum evento perdido mesmo com falha parcial |
| Fila durável | Mensagens sobrevivem a restart do RabbitMQ |
| `persistent=true` | Mensagens são escritas em disco pelo broker |
| `BasicAck` pós-processamento | Mensagem só é removida da fila após o Worker confirmar |
| `BasicNack + requeue` | Falhas transitórias são reenfileiradas automaticamente |
| Contador `Attempts` | Após 5 falhas: `status=Failed` (evita loop infinito) |

### At-least-once delivery

O sistema garante entrega **at-least-once**:
- A mesma mensagem pode ser processada mais de uma vez em caso de falha.
- Em produção, implementar **idempotência** no Worker via tabela `processed_events`.

---

## 4. Segurança

| Ameaça | Mitigação |
|---|---|
| Acesso não autorizado a lançamentos | JWT obrigatório no POST /api/entries |
| Ataques de força bruta | Rate Limiting: 100 req/min por IP |
| Vazamento de secrets | Variáveis de ambiente (não hardcoded no código) |
| Injeção SQL | EF Core com parâmetros — sem SQL raw |
| Man-in-the-middle | HTTPS com TLS (obrigatório em produção) |
| Token comprometido | Expiração em 2h; sem refresh token (demo) |

---

## 5. Métricas e Metas Operacionais (SLOs)

| Serviço | Métrica | Meta |
|---|---|---|
| Lançamentos (POST) | Disponibilidade | 99,9% |
| Lançamentos (POST) | Latência p99 | < 200ms |
| Lançamentos (POST) | Taxa de erro | < 0,1% |
| Consolidado (GET) | Disponibilidade | 99,5% |
| Consolidado (GET) | Latência p95 | < 50ms (cache hit) |
| Consolidado (GET) | Latência p99 | < 200ms (cache miss) |
| Consolidado (GET) | Taxa de perda | < 5% |
| Outbox Publisher | Lag máximo | < 5s (loop de 1s) |
| RabbitMQ Queue | Profundidade máxima | Alerta em > 1.000 mensagens |

---

## 6. Observabilidade (Recomendado para Produção)

```
┌──────────────────────────────────────────────┐
│  OpenTelemetry SDK                            │
│  ├── Traces: Request → Handler → DB/RabbitMQ │
│  ├── Metrics: req/s, latência, fila          │
│  └── Logs: Serilog estruturado               │
└──────────────────────────────────────────────┘
         ↓
┌─────────────┐  ┌──────────────────┐  ┌────────────────┐
│  Jaeger /   │  │  Prometheus +    │  │  Elasticsearch │
│  Zipkin     │  │  Grafana         │  │  + Kibana      │
│  (Tracing)  │  │  (Métricas)      │  │  (Logs)        │
└─────────────┘  └──────────────────┘  └────────────────┘
```

### Alertas críticos recomendados

| Condição | Severidade |
|---|---|
| Fila `cash-entry-created` > 1.000 mensagens | `WARNING` |
| Fila `cash-entry-created` > 10.000 mensagens | `CRITICAL` |
| `outbox_messages` com `status=Failed` > 0 | `WARNING` |
| Worker parado por > 5 minutos | `CRITICAL` |
| API latência p99 > 500ms | `WARNING` |
| Taxa de erro API > 1% | `CRITICAL` |
