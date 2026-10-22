# C4 — Component View

- **Status:** EM REVISÃO FINAL — aguardando validação humana

## Serviço de Lançamentos

Dois processos executam componentes do mesmo serviço.

```mermaid
flowchart LR
  Client[Cliente] --> API[Transactions API]
  API --> Create[CreateTransaction]
  API --> Query[Query Use Cases]
  Create --> Domain[FinancialTransaction]
  Create --> Idem[Idempotency Store Port]
  Create --> Repo[Transaction Repository]
  Create --> Outbox[Outbox Store Port]
  Idem --> Db[(Transactions PostgreSQL)]
  Repo --> Db
  Outbox --> Db
  Worker[Transactions Worker] --> Claim[Outbox Claim/Retry]
  Claim --> Db
  Worker --> Pub[Message Publisher Port]
  Pub --> Q[SQS Standard]
```

O boundary transacional de `CreateTransaction` inclui lançamento, idempotência e Outbox. O Worker não participa da resposta HTTP. Publishers concorrentes capturam pendências de forma segura (`SKIP LOCKED` ou equivalente).

## Serviço de Consolidação

```mermaid
flowchart LR
  Q[SQS Standard] --> Worker[Consolidation Worker]
  Worker --> Consumer[TransactionCreatedV1 Handler]
  Consumer --> Dedup[ProcessedEvent Store]
  Consumer --> Apply[Apply Consolidation]
  Dedup --> Db[(Consolidation PostgreSQL)]
  Apply --> Db
  Client[Cliente] --> API[Consolidation API]
  API --> Query[Consolidation Queries]
  Query --> Db
```

`ProcessedEvent` e UPSERT relativo da projeção pertencem ao mesmo commit. `DeleteMessage` ocorre somente depois desse commit.

## Componentes transversais

- `CashFlow.Contracts`: contratos de integração versionados, sem entidades de domínio;
- clock/time-zone abstraction: permite derivar/testar `BusinessDate` deterministicamente;
- OpenTelemetry instrumentation: HTTP, banco e mensageria;
- Problem Details: erros HTTP consistentes;
- configuração: retry, retenções e parâmetros operacionais externalizados.

## Dependências permitidas

```text
Api/Worker ------> Application ---> Domain
Infrastructure --> Application
Infrastructure --> Domain (mapeamento estritamente necessário)
Contracts -------> boundaries de integração
```

Domain não referencia ASP.NET Core, EF Core, SDK AWS ou PostgreSQL. Application depende de portas, não do broker concreto.

## Rastreabilidade

- independência: API confirma por commit local; Workers são assíncronos;
- confiabilidade: Outbox + SQS + DLQ;
- idempotência: registro HTTP + `ProcessedEvent`;
- concorrência: unicidade, `SKIP LOCKED`, UPSERT atômico;
- recuperação: retry/redrive/reconciliação;
- observabilidade: componentes e processos instrumentados independentemente.