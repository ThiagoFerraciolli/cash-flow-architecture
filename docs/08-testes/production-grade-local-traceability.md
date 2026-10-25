# Rastreabilidade de testes da entrega

Este registro separa a [estratégia normativa T01–T20](../05-arquitetura-software/estrategia-testes.md), a cobertura automatizada e a evidência operacional efetivamente executada no Docker Compose local. `PARTIAL` significa que o cenário normativo mais amplo não foi integralmente ensaiado. Não houve execução em AWS nem GitHub CI.

| Cenário | Cobertura da entrega | Evidência executada e limite |
|---|---|---|
| T01–T05 | AUTOMATED | Suíte .NET com PostgreSQL/RabbitMQ reais cobre HTTP, Outbox, projeção, concorrência e idempotência; E2E local disponível. |
| T06 | OPERATIONAL PASS / PARTIAL | Consolidation API, Worker e PostgreSQL parados; Transactions live/ready 200; CREDIT e DEBIT 201; após retorno, 100/20/80 e contagem 2. Não executou 1.000 fatos a 10/s. |
| T07 | PARTIAL | Cobertura automatizada de circuito/falha de publicação; cenário operacional completo de outage do broker não executado. |
| T08 | AUTOMATED | Lease/publicação e crash após commit antes do ACK com redelivery. |
| T09 | AUTOMATED + OPERATIONAL PASS / PARTIAL | Mensagem inválida → DLQ +1, `message_id` encontrado, projeção inalterada. Redrive completo não executado. |
| T10–T12 | AUTOMATED + T10 OPERATIONAL PASS / PARTIAL | T10 altera geração e reconstrói data isolada 100/20/80, contagem 2. Testes cobrem expiração/tomada/fencing. Não demonstra toda reconciliação T10, crash windows T11 nem retenção T12. |
| T13 | OPERATIONAL PASS local / PARTIAL | 401 sem token; 403 reader em escrita; 201 demo em escrita; 200 reader em leitura. A regressão automatizada final também cobre GET anônimo → 401 e token somente de escrita → 403 nas rotas de leitura de Transactions. Cognito/segurança AWS não executados. |
| T14 | OPERATIONAL PASS local / PARTIAL | Métricas locais e trace Jaeger cruzando API → DB/Outbox → publisher → RabbitMQ → consumer → DB. Sem stack produtiva. |
| T15 | OPERATIONAL PASS local / PARTIAL | 6.000/6.000 GETs em 120 s a 50 req/s; 0 falhas/dropped; P50 2,964 ms, P95 4,372 ms, P99 6,052 ms. Não executou 10 min/30.000 requests/100.000 fatos. |
| T16 | NOT EXECUTED | Backlog de 10.000 eventos não ensaiado. |
| T17 | NOT EXECUTED / AWS TARGET | Restore e disponibilidade AWS não ensaiados; cloud não provisionada. |
| T18 | OPERATIONAL PASS local / PARTIAL | Bootstrap sem `runtime-data`, quatro imagens, serviços saudáveis, migrations exit 0, token e primeiro POST 201. Não executado por terceiro independente. |
| T19–T20 | AUTOMATED / PARTIAL | Testes de contratos/estrutura; não representam todos os cenários operacionais normativos. |

## Recovery

A ferramenta administrativa lê `financial_transactions` em snapshot PostgreSQL somente leitura, constrói/verifica nova geração e só então a ativa. `projection_control` usa ownership token e expiração; lease de 60 s renovada a cada 15 s. Renovação, liberação e ativação exigem dono válido. Se o processo morrer, a lease expira, o Worker retoma e outro Recovery pode adquirir nova lease. O rebuild deve ser reiniciado pelo operador; não existe restart automático ou reconciliação contínua.

## Gate final

O golden path final executado em 23/10/2026 registrou:

```text
dotnet restore --locked-mode  PASS
dotnet build --no-restore     PASS — 0 warnings / 0 errors
dotnet test --no-build        90 passed / 0 failed / 0 skipped
```

Execuções anteriores com 80, 84 e 89 testes são histórico de evolução e não o gate corrente da entrega. Detalhes dos ensaios estão em [validação local final](validacao-local-final.md).