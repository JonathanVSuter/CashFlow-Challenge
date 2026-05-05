# ADR 0004 — Clean Architecture

## Status

Aceito

## Contexto

O desafio requer boas práticas de desenvolvimento, SOLID e padrões de arquitetura.

É necessário estruturar o código de forma que:
- As regras de negócio sejam independentes de frameworks, bancos de dados e tecnologias externas.
- Os casos de uso sejam testáveis em isolamento.
- A substituição de componentes externos (banco, mensageria, cache) não afete o domínio.

## Decisão

Adotar **Clean Architecture** (Robert C. Martin) com quatro camadas:

1. **Domain** — Entidades e regras de negócio puras. Sem dependências externas.
2. **Application** — Casos de uso (CQRS). Depende apenas do Domain e de interfaces.
3. **Infrastructure** — Implementações concretas (EF Core, RabbitMQ, Cache). Depende de Application + Domain.
4. **Api / Worker** — Entry points. Composição raiz (DI). Dependem de todas as camadas.

### Regra de dependência

```
Externo → Infraestrutura → Aplicação → Domínio
```

O Domínio não conhece nada fora de si mesmo.

## Consequências

**Positivas:**
- Testabilidade: handlers testados com mocks de `IApplicationDbContext`.
- Flexibilidade: troca de ORM ou broker sem tocar em regras de negócio.
- Clareza: cada camada tem responsabilidade única e bem definida.

**Negativas:**
- Mais arquivos e projetos comparado a uma abordagem simples (ex.: minimal API com tudo em um projeto).
- Curva de aprendizado inicial maior para novos desenvolvedores.

## Alternativas consideradas

| Alternativa | Motivo da rejeição |
|---|---|
| Minimal API com tudo em um projeto | Não demonstra separação de responsabilidades exigida pelo desafio |
| MVC tradicional em camadas (sem inversão) | Acoplamento do domínio a Entity Framework |
| Vertical Slice Architecture | Adequada para projetos maiores; overhead para este escopo |
