# ADR 0006 — Autenticação com JWT

## Status

Aceito (implementação demonstrativa)

## Contexto

O desafio menciona segurança como requisito. O endpoint de criação de lançamentos deve ser protegido contra acesso não autorizado.

Para o escopo do desafio, é necessária uma implementação que demonstre o conceito sem introduzir complexidade excessiva de infraestrutura (Identity Provider, banco de usuários, etc.).

## Decisão

Implementar autenticação **JWT HS256** demonstrativa:

- Endpoint `POST /api/auth/token` retorna um token para credenciais hardcoded (`admin` / `admin123`).
- Token expira em 2 horas.
- Endpoint de escrita (`POST /api/entries`) requer `Authorization: Bearer {token}`.
- Endpoints de leitura são públicos (sem autenticação).

```
POST /api/auth/token
    → Valida credenciais (hardcoded)
    → Gera JWT (HS256, claims: Name + scope)
    → Retorna { accessToken }

POST /api/entries
    → [Authorize] middleware valida JWT
    → Extrai claims
    → Executa comando
```

## Consequências

**Positivas:**
- Demonstra o fluxo completo de autenticação e autorização.
- Stateless (sem sessão no servidor).
- Compatível com API Gateway futuro (validação centralizada).
- Zero dependências externas (sem Identity Provider).

**Negativas:**
- Credenciais hardcoded — não adequado para produção.
- Sem refresh token — usuário precisa reautenticar após 2h.
- Sem revogação de token.

## Implementação em produção

Substituir o `AuthController` atual por integração com:

| Opção | Cenário |
|---|---|
| **Keycloak** | On-premises, controle total |
| **Azure AD B2C** | Azure; integração com Microsoft 365 |
| **Auth0** | SaaS; setup rápido |
| **AWS Cognito** | AWS; integração nativa com serviços AWS |

O restante da implementação (validação JWT no middleware) permanece sem alteração.

## Alternativas consideradas

| Alternativa | Motivo da rejeição |
|---|---|
| API Key simples | Não demonstra autenticação moderna; sem expiração |
| OAuth2 completo + Identity Provider | Overhead excessivo para o escopo do desafio |
| Sem autenticação | Não atende o requisito de segurança |
| Cookie-based session | Não adequado para API REST stateless |
