# ADR 0003 - Outbox + RabbitMQ

## Contexto

Publicar diretamente no broker pode gerar inconsistência se o banco salvar e a publicação falhar.

## Decisão

Gravar evento na Outbox e publicar depois.

## Consequências

A escrita fica resiliente, mas o sistema passa a ser eventualmente consistente.
