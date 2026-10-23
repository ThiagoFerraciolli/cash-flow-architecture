# Evidências da validação local final

Resultados observados no ambiente **Docker Compose local real** na branch `feat/final-validation`. Este registro documenta execuções locais já realizadas; não representa resultado do GitHub CI, teste em AWS nem provisionamento de infraestrutura AWS. A classificação das capacidades e a matriz de conformidade são mantidas nos documentos de requisitos.

## E2E completo

- Resultado: **PASS**.
- Teste: `tests/CashFlow.EndToEndTests/ComposeTransactionFlowTests.cs`.
- Fluxo exercitado: Keycloak → Transactions API → Outbox → RabbitMQ → Consolidation Worker → Consolidation API.
- Lançamentos: CREDIT `100.00` e DEBIT `20.00`.
- Projeção observada: `totalCredits=100.00`, `totalDebits=20.00`, `balance=80.00`, `transactionCount=2`.

## T06 — independência durante indisponibilidade de Consolidation

- Resultado: **PASS**.
- Serviços parados durante os POSTs: `consolidation-api`, `consolidation-worker`, `postgres-consolidation`.
- Transactions `/health/live` durante a indisponibilidade: HTTP `200`.
- Transactions `/health/ready` durante a indisponibilidade: HTTP `200`.
- CREDIT POST durante a indisponibilidade: HTTP `201`.
- DEBIT POST durante a indisponibilidade: HTTP `201`.
- Após a recuperação dos serviços, a projeção convergiu para `totalCredits=100.00`, `totalDebits=20.00`, `balance=80.00`, `transactionCount=2`.

## T15 — carga de leitura de Consolidation

- Resultado: **PASS** no ensaio local bounded; não é benchmark AWS.
- Ambiente: Docker Compose local, k6 em container.
- Warm-up separado: `10 s`.
- Janela medida: `120 s`; endpoint `GET /consolidations/{businessDate}`.
- Requisições esperadas: `6000`; executadas: `6000`; taxa sustentada: `50 req/s`.
- Falhas: `0`; failure rate: `0%`; dropped iterations: `0`.
- Latências: P50 `3.21 ms`, P95 `4.52 ms`, P99 `5.97 ms`.

## Suíte de regressão

Execução local da suíte completa `dotnet test` após o T15, com PostgreSQL e RabbitMQ reais configurados: **80 passed, 0 failed, 0 skipped**.
