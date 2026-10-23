# Evidências da validação local final

Resultados observados no ambiente **Docker Compose local real**, inicialmente na branch `feat/final-validation` e depois na extensão `feat/production-grade-local`. Este registro documenta execuções locais; não representa resultado do GitHub CI, teste em AWS nem provisionamento de infraestrutura AWS. A [rastreabilidade da extensão](production-grade-local-traceability.md) separa cobertura automatizada e cenários operacionais limitados da estratégia normativa T01–T20.

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
- Execução inicial da validação local: P50 `3,21 ms`, P95 `4,52 ms`, P99 `5,97 ms`.

## Evidências operacionais posteriores

- **T09 — PASS, cenário limitado de mensagem permanentemente inválida:** mensagem publicada e roteada; contagem da DLQ aumentou em um; o `message_id` exato foi encontrado na DLQ; projeção de negócio permaneceu inalterada. Redrive completo não foi executado.
- **T10 — PASS, rebuild local:** migration `002_recovery_lease.sql` aplicada; API e Worker saudáveis; POST CREDIT `100.00` e POST DEBIT `20.00`; nova geração ativada. Na data isolada, créditos `100.00`, débitos `20.00`, saldo `80.00` e contagem `2`. Testes automatizados adicionais cobrem expiração, tomada de posse e fencing da lease; o ensaio operacional não simulou todos os modos de falha.
- **T13 — PASS, segurança local:** escrita sem token HTTP `401`; `cashflow-reader` tentando escrever HTTP `403`; escrita autorizada de `cashflow-demo` HTTP `201`; leitura de Consolidation por `cashflow-reader` HTTP `200`. Prova client credentials/JWT e permissões locais, não Cognito ou segurança cloud.
- **T14 — PASS, observabilidade local:** `/metrics` expôs métricas; Jaeger recebeu traces dos quatro processos. Um trace de transação atravessou Transactions API → PostgreSQL/Outbox → Transactions Worker → fronteira assíncrona RabbitMQ → Consolidation Worker → PostgreSQL de Consolidação. O GET posterior da Consolidation API é outra requisição HTTP e não integra necessariamente o mesmo trace. Sem servidor Prometheus ou stack de produção.
- **T15 — PASS, última medição local:** warm-up separado de `10 s` a `10 req/s`; janela medida de `120 s` a `50 req/s`; `6000` esperadas e `6000` executadas; `0` falhas, failure rate `0%`, `0` dropped iterations; P50 `2,964 ms`, P95 `4,372 ms`, P99 `6,052 ms`; metas internas de latência atendidas. A projeção estática antes da carga tinha créditos `100.00`, débitos `20.00`, saldo `80.00` e contagem `2`. Este ensaio local limitado satisfaz o requisito explícito de 50 req/s e falha ≤ 5%; não executou o ensaio normativo de 10 min, 30.000 consultas e 100.000 fatos sintéticos.
- **T18 — PASS, bootstrap local limpo:** `runtime-data` ausente antes da inicialização; `docker compose up -d --build` construiu as quatro imagens de aplicação. Ambos os PostgreSQL, RabbitMQ, Jaeger, Keycloak, APIs e Workers ficaram saudáveis; `migrate` e `migrate-consolidation` encerraram com código `0`. Liveness/readiness das duas APIs responderam com sucesso, um token client credentials foi obtido e o primeiro POST na base limpa criou CREDIT `100.00` com HTTP `201`. Não é validação AWS nem execução por terceiro independente.

## Suíte de regressão

Após o T15 inicial, a suíte com PostgreSQL e RabbitMQ reais teve **80 passed, 0 failed, 0 skipped**. Antes do endurecimento final da lease, o usuário confirmou **84 passed, 0 failed, 0 skipped**. Após as mudanças de lease/fencing e o bootstrap limpo, a regressão final foi executada novamente pelo usuário: `dotnet restore --locked-mode` **PASS**, `dotnet build --no-restore` **PASS** e `dotnet test --no-build` com **89 passed, 0 failed, 0 skipped**. O resultado é evidência de execução local; não corresponde a GitHub CI.
