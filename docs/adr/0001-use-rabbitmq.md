# ADR 0001 - Usar RabbitMQ

## Contexto

O desafio exige desacoplamento entre lançamentos e consolidado.

## Decisão

Usar RabbitMQ como broker de mensagens.

## Consequências

A consolidação fica assíncrona e resiliente a falhas temporárias, com maior complexidade operacional.
