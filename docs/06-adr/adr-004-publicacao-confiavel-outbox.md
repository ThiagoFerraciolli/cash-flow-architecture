# ADR-004 — Publicação confiável com Transactional Outbox

- **Status:** ACEITO PARA A ARQUITETURA DO DESAFIO
- **Drivers:** RNF-004, ASR-002
- **Dependências:** ADR-002, ADR-003

## Contexto

Persistir um lançamento no PostgreSQL e publicar diretamente em SQS são dois recursos independentes. Um dual write ingênuo pode deixar lançamento sem evento ou evento sem lançamento.

## Decisão

Adotar **Transactional Outbox**. `FinancialTransaction`, `IdempotencyRecord` e `OutboxMessage` são persistidos no mesmo commit local. Um **Transactions Worker / Outbox Publisher** separado da API captura pendências e publica em SQS.

Múltiplos publishers usam captura de lote com `FOR UPDATE SKIP LOCKED` (ou equivalente PostgreSQL). Após `SendMessage` confirmado, o registro recebe `published_at`.

Campos operacionais mínimos: `attempts`, `next_attempt_at`, `last_error`, `published_at` e metadados de tracing.

## Falhas e limites

- falha da fila não afeta lançamento já confirmado;
- publisher aplica retry com backoff/jitter;
- falha após envio e antes de `published_at` pode duplicar publicação;
- consumidor idempotente é obrigatório;
- Outbox publicada tem retenção/limpeza configurável; pendências nunca são removidas pela limpeza;
- Outbox não oferece exactly-once, backup ou DR.

## Evidência posterior

Testes devem cobrir fila indisponível, publishers concorrentes, crash window e recuperação. `ACEITO` descreve a decisão; somente evidência executada valida o comportamento.