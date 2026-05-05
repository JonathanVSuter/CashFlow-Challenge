# ADR 0005 — CQRS (Command Query Responsibility Segregation)

## Status

Aceito

## Contexto

O sistema possui dois tipos de operação com características distintas:
- **Escrita** (criar lançamento): precisa de validação, persistência atômica e publicação de evento.
- **Leitura** (consultar saldo): precisa ser rápida, cacheável e independente da escrita.

Misturar essas responsabilidades em um único repositório ou serviço aumenta o acoplamento e dificulta a otimização de cada caminho individualmente.

## Decisão

Aplicar **CQRS** com interfaces explícitas para comandos e queries:

```csharp
// Escrita — altera estado
ICommandHandler<TCommand, TResult>

// Leitura — não altera estado
IQueryHandler<TQuery, TResult>
```

Cada operação tem seu próprio handler, com dependências injetadas individualmente.

**Importante:** Não foi adotado Event Sourcing. O estado é persistido diretamente no banco relacional.

## Consequências

**Positivas:**
- Caminho de leitura pode usar cache sem afetar a escrita.
- Cada handler tem responsabilidade única (Single Responsibility Principle).
- Facilita testes unitários (cada handler testado individualmente).
- Permite otimizar escrita e leitura de forma independente.

**Negativas:**
- Mais classes comparado a um único serviço genérico.
- Consistência eventual: saldo diário pode estar ligeiramente desatualizado até o Worker processar o evento.

## Alternativas consideradas

| Alternativa | Motivo da rejeição |
|---|---|
| Repository Pattern puro (sem CQRS) | Mistura leitura e escrita; dificulta otimização de cada caminho |
| MediatR | Adiciona dependência e indireção desnecessária para o escopo do desafio |
| Event Sourcing | Complexidade excessiva para os requisitos atuais |
