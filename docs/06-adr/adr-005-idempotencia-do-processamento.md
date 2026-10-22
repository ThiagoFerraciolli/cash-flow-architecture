# ADR-005 — Idempotência no registro e no processamento

- **Status:** ACEITO PARA A ARQUITETURA DO DESAFIO
- **Drivers:** RNF-006, ASR-004

## Contexto

Retry de cliente e entrega at-least-once podem repetir operações. Em fluxo financeiro, repetição não pode criar efeito adicional.

## Decisão — entrada HTTP

`Idempotency-Key` é obrigatório em `POST /transactions` e persistido em `IdempotencyRecord`, separado da entidade financeira. O registro contém chave, SHA-256 do payload canônico, `transaction_id`, resposta lógica e expiração.

No MVP, sem identidade de cliente definida, a chave é global. Se surgir `client_id` confiável, a evolução é unicidade `(client_id, key)`.

Semântica:
- chave ausente → 400;
- primeira operação → 201;
- mesma chave + mesmo hash → replay da resposta original, sem novo lançamento, com `Idempotent-Replayed: true`;
- mesma chave + hash diferente → 409;
- concorrência é arbitrada por unique constraint e releitura determinística.

A janela de retenção é configuração explícita do MVP, não regra de negócio fornecida.

## Decisão — consumo

Cada evento possui `eventId`. Na mesma transação do consolidado:
1. `INSERT processed_event ... ON CONFLICT DO NOTHING`;
2. se já existir, no-op bem-sucedido;
3. se novo, UPSERT atômico da projeção;
4. commit;
5. somente depois, `DeleteMessage` na SQS.

Rollback desfaz deduplicação e projeção juntas.

## Consequências

- redelivery e crash depois do commit são seguros;
- deduplicação não depende de memória;
- não se promete exactly-once ponta a ponta;
- retenção de registros técnicos deve ser operacionalmente definida;
- testes de concorrência são obrigatórios antes de afirmar validação.