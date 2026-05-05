# ADR 0002 - Usar PostgreSQL no Docker Compose

## Contexto

API e Worker rodam em containers diferentes.

## Decisão

Usar PostgreSQL para persistência compartilhada.

## Consequências

A aplicação deixa de depender de banco em memória e passa a funcionar corretamente em ambiente distribuído.
