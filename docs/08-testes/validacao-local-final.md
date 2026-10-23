# Evidências da validação local final

Este registro consolida resultados observados no ambiente **Docker Compose local real**. Não representa GitHub CI, execução em AWS nem provisionamento de infraestrutura cloud. A [rastreabilidade da entrega](production-grade-local-traceability.md) separa a estratégia normativa T01–T20 daquilo que foi efetivamente automatizado ou exercitado operacionalmente.

## E2E completo

- Resultado: **PASS local**.
- Teste: `tests/CashFlow.EndToEndTests/ComposeTransactionFlowTests.cs`.
- Fluxo: Keycloak → Transactions API → Outbox → RabbitMQ → Consolidation Worker → Consolidation API.
- Fatos: CREDIT `100.00` e DEBIT `20.00`.
- Projeção: créditos `100.00`, débitos `20.00`, saldo `80.00`, contagem `2`.

## T06 — independência durante indisponibilidade de Consolidation

- Resultado: **PASS local, escopo limitado**.
- Parados: `consolidation-api`, `consolidation-worker`, `postgres-consolidation`.
- Transactions live/ready durante a indisponibilidade: HTTP `200`/`200`.
- Dois POSTs durante a indisponibilidade: CREDIT e DEBIT, ambos HTTP `201`.
- Após recuperação: projeção `100.00 / 20.00 / 80.00`, contagem `2`.
- Limite: o cenário normativo mais amplo de 1.000 fatos a 10/s não foi executado.

## T09 — DLQ

- Resultado: **PASS local, escopo limitado**.
- Mensagem permanentemente inválida publicada/roteada.
- DLQ aumentou em uma mensagem e o `message_id` exato foi localizado.
- Projeção permaneceu inalterada.
- **Redrive operacional completo não foi executado.**

## T10 — rebuild

- Resultado: **PASS local, escopo limitado**.
- Migration de recovery aplicada; nova geração ativada sob lease/fencing.
- Data isolada após rebuild: créditos `100.00`, débitos `20.00`, saldo `80.00`, contagem `2`.
- Testes automatizados cobrem expiração, tomada de posse e fencing.
- O ensaio operacional não simula todos os crash windows, reconciliação contínua ou restore produtivo.

## T13 — segurança local

- Sem token em escrita: `401`.
- `cashflow-reader` tentando escrever: `403`.
- `cashflow-demo` escrevendo: `201`.
- `cashflow-reader` lendo Consolidation: `200`.
- Demonstra Keycloak/client credentials/JWT/permissões locais; não demonstra Cognito, IAM ou segurança AWS.

## T14 — observabilidade local

- `/metrics` expôs métricas locais.
- Jaeger recebeu traces dos quatro processos.
- Um trace transacional atravessou Transactions API → PostgreSQL/Outbox → Transactions Worker → RabbitMQ → Consolidation Worker → PostgreSQL de Consolidação.
- O GET posterior da Consolidation API é requisição independente e não integra necessariamente o mesmo trace.
- Não há servidor Prometheus/Grafana/Collector nem plataforma de observabilidade produtiva no Compose.

## T15 — 50 req/s no Consolidado

- Resultado: **PASS na janela local executada**; não é benchmark AWS.
- Warm-up separado: `10 s` a `10 req/s`.
- Janela medida: `120 s` a `50 req/s`.
- Esperadas/executadas: `6000 / 6000`.
- Falhas: `0`; failure rate `0%`; dropped iterations `0`.
- Última medição: P50 `2,964 ms`, P95 `4,372 ms`, P99 `6,052 ms`.
- Projeção estática usada: créditos `100.00`, débitos `20.00`, saldo `80.00`, contagem `2`.

**Conclusão permitida:** durante 120 segundos no ambiente local medido, o Consolidado sustentou 50 req/s com 0% de falhas e sem dropped iterations. Isso atende o critério explícito dentro dessa janela de ensaio. **Não** prova SLA produtivo, capacidade AWS nem o ensaio normativo de 10 minutos/30.000 consultas/100.000 fatos.

## T18 — bootstrap local limpo

- Resultado: **PASS local, escopo limitado**.
- `runtime-data` ausente antes da inicialização.
- `docker compose up -d --build` construiu as quatro imagens de aplicação.
- PostgreSQLs, RabbitMQ, Jaeger, Keycloak, APIs e Workers ficaram saudáveis.
- `migrate` e `migrate-consolidation` terminaram com código `0`.
- Token client credentials obtido e primeiro POST CREDIT `100.00` retornou `201`.
- Não foi execução por terceiro independente e não valida AWS.

## Regressão final

Execução local final registrada:

```text
dotnet restore --locked-mode  PASS
dotnet build --no-restore     PASS
dotnet test --no-build        89 passed / 0 failed / 0 skipped
```

Execuções anteriores de 80 e 84 testes fazem parte da evolução da implementação e não são o gate final. O número de referência da entrega é **89 passed, 0 failed, 0 skipped**, sempre qualificado como execução local e não CI.

## O que não foi demonstrado

- AWS/SQS/Cognito/ECS/RDS Multi-AZ/IAM/KMS/WAF.
- GitHub CI.
- T07 operacional completo.
- Redrive completo da DLQ.
- T16 backlog de 10.000 eventos.
- RTO/RPO produtivos.
- Ensaio T15 normativo de 10 minutos/30.000 consultas/100.000 fatos.
- Execução independente por terceiro.

Esses limites são intencionais na documentação para que arquitetura-alvo, cobertura automatizada e evidência executada não sejam confundidas.