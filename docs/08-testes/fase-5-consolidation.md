# Evidência de execução — Fase 5

- Data: 2026-10-22, ambiente local com Docker Compose v2.38.2, .NET SDK 10.0.402, PostgreSQL `17.6-bookworm` em duas instâncias e RabbitMQ `4.1.8-management`.
- Base: `e63996bc45989f2881cadc49d07bc04ba6f5a5fc`; branch `feat/consolidation-consumer`.
- Dados sintéticos; JWT RS256 de um emissor de teste externo ao Compose, com issuer `http://host.docker.internal:18080` e permissão `transactions.write`.
- Variáveis de teste: `CASHFLOW_TEST_POSTGRES` para `127.0.0.1:5433`, `CASHFLOW_TEST_CONSOLIDATION_POSTGRES` para `127.0.0.1:5434` e `CASHFLOW_TEST_RABBITMQ` para `127.0.0.1:5672`. Nenhum teste PostgreSQL ou RabbitMQ foi skipped.

## Gate automatizado

Com as três variáveis acima configuradas, foram executados `dotnet restore --locked-mode`, `dotnet build --no-restore` e `dotnet test --no-build --disable-build-servers --maxcpucount:1 /p:UseSharedCompilation=false`. Resultado: **75 passed, 0 failed, 0 skipped**; build com 0 warnings e 0 errors. As opções adicionais do test runner evitam o problema local de sockets do MSBuild e não desativam testes.

| Casos exigidos | Evidência automatizada |
|---|---|
| 1–6: crédito, débito, mesmo dia, dias distintos, contagem e saldo derivado | `ProjectionTests.Credit_creates_daily_total_and_derived_balance`, `Debit_creates_debit_total`, `Multiple_transactions_accumulate_by_day_and_other_days_remain_independent` |
| 7–8: duplicatas com mesmo ou novo eventId | `Same_event_and_new_event_same_fact_are_no_ops_without_touching_timestamp`, `RabbitConsumerTests.New_event_for_same_financial_fact_is_acknowledged_without_second_effect` |
| 9–12: conflitos de valor, tipo, data e eventId | `Same_transaction_with_different_financial_fact_is_permanent_conflict`, `Event_id_reused_for_another_transaction_is_permanent_conflict`, `Conflicting_financial_or_event_identity_is_confirmed_into_dlq` |
| 13–16: versão/tipo inválidos, valor inválido e JSON malformado | `Invalid_envelope_is_rejected`, `Invalid_amount_is_rejected`, `Invalid_contract_is_confirmed_into_dlq_without_projection`, `Invalid_amount_is_confirmed_into_dlq` |
| 17–20: progressão 5/15/30/60, confirms antes do ACK e falha no repasse | `Isolated_failure_is_confirmed_into_retry_with_incremented_attempt`, `Fifth_failed_attempt_is_confirmed_into_dlq`, `Dlq_publication_failure_leaves_original_unacknowledged`, `Retry_publication_failure_leaves_original_unacknowledged` |
| 21–23: outage, retomada e crash após commit antes de ACK | `Database_outage_pauses_without_retry_then_recovers`, `Commit_then_consumer_crash_before_ack_redelivers_without_second_effect` |
| 24–27: concorrência, manutenção e geração ativa | `Concurrent_duplicates_have_one_financial_effect`, `Concurrent_distinct_transactions_accumulate_exactly`, `Maintenance_prevents_new_financial_effect`, `Active_generation_controls_where_new_effect_is_written` |
| 28: persistência | `Projection_remains_after_new_database_connection` e reinício real do Compose abaixo |
| Readiness do schema | `Readiness_requires_the_complete_projection_schema` |

Os testes RabbitMQ criam vhosts isolados, declaram a topologia pelo próprio consumidor e os removem ao final. Os testes PostgreSQL criam schemas isolados, executam a migration e removem os schemas depois. O teste de crash encerra a conexão do consumidor após o commit e antes do ACK; a redelivery real retorna `Duplicate`, com um único registro e um único efeito.

## Docker e fluxo real

`docker compose up -d --build` iniciou os seis workloads permanentes saudáveis. Três POSTs autenticados para `businessDate=2026-08-14` retornaram 201: CREDIT 100.00, DEBIT 20.00, CREDIT 50.00. Consulta direta no PostgreSQL de Consolidação retornou `total_credits=150.00`, `total_debits=20.00`, `transaction_count=3`, saldo derivado `130.00` e três `processed_transactions` para os IDs criados. Republicação real do payload Outbox do primeiro lançamento retornou `routed=true`; totais, contagem e cardinalidade permaneceram iguais. A fila principal convergiu para zero mensagens.

Com somente `postgres-consolidation` parado, `Transactions /health/ready` retornou 200 e um novo POST retornou 201. A Outbox correspondente ficou `PUBLISHED`. A mensagem aguardou na fila principal, sem mensagens nas quatro filas de retry e sem header `retryAttempt`. Após `docker compose start postgres-consolidation`, a identidade foi processada uma vez e a fila convergiu novamente.

Uma publicação RabbitMQ com JSON inválido foi encaminhada à DLQ: contagem de 0 para 1, fila principal em 0, e contagens de `daily_consolidations` e `processed_transactions` inalteradas. A política efetiva da fila principal exibiu `delivery-limit=100`, DLX `cashflow.errors` e `dead-letter-strategy=at-least-once`.

Após `docker compose down` seguido de `docker compose up -d`, sem remover `runtime-data`, a leitura anterior e posterior foi idêntica: lançamento de Transactions = 1, geração ativa = 1, identidade processada = 1, projeção do dia = `150.00|20.00|3` e DLQ = 1. Isso verifica persistência das duas bases, da projeção e do RabbitMQ no cenário local.

O ensaio não mede performance, RTO/RPO, AWS/SQS ou reconstrução operacional.
