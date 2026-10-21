# SDD — Software Design Document

## 1. Objetivo

Detalhar a implementação C# da arquitetura aprovada sem alterar as decisões de solução: Lançamentos e Consolidação permanecem deployables independentes, com persistência própria e integração assíncrona.

## 2. Stack de referência

- .NET 10 LTS / C#;
- ASP.NET Core para APIs HTTP;
- EF Core 10 para persistência;
- PostgreSQL;
- Kafka como broker de referência conforme ADR-002/ADR-007;
- Docker/Docker Compose para execução local;
- xUnit para testes.

.NET 10 é escolhido por ser LTS e evitar iniciar a solução em uma versão próxima do fim de suporte. A escolha não é requisito do negócio e pode ser alterada por restrição corporativa.

## 3. Princípios

- dependências apontam para dentro: API/Infrastructure dependem de Application/Domain, nunca o inverso;
- Domain não conhece ASP.NET Core, EF Core, Kafka ou PostgreSQL;
- Application orquestra casos de uso e define ports necessários;
- Infrastructure implementa persistência, mensageria e detalhes técnicos;
- API traduz HTTP para casos de uso e respostas;
- cada bounded context mantém seu próprio modelo e ownership de dados.

## 4. Estrutura da solution

```text
src/
  BuildingBlocks/
    CashFlow.Contracts/
  Transactions/
    CashFlow.Transactions.Domain/
    CashFlow.Transactions.Application/
    CashFlow.Transactions.Infrastructure/
    CashFlow.Transactions.Api/
  Consolidation/
    CashFlow.Consolidation.Domain/
    CashFlow.Consolidation.Application/
    CashFlow.Consolidation.Infrastructure/
    CashFlow.Consolidation.Api/
    CashFlow.Consolidation.Worker/
tests/
  CashFlow.Transactions.UnitTests/
  CashFlow.Transactions.IntegrationTests/
  CashFlow.Consolidation.UnitTests/
  CashFlow.Consolidation.IntegrationTests/
```

`CashFlow.Contracts` contém somente contratos de integração versionáveis. Não é um projeto de domínio compartilhado.

## 5. Transactions Context

### Domain

`FinancialTransaction`
- `Id`
- `Type`: Credit | Debit
- `Amount`
- `Description`
- `OccurredAt`
- `CreatedAt`

Invariantes do MVP:
- `Amount > 0`;
- tipo obrigatório e válido;
- lançamento confirmado é imutável no escopo atual.

### Application

Casos de uso:
- `CreateTransaction`;
- `GetTransactionById`;
- `ListTransactions`.

Ports principais:
- `ITransactionRepository`;
- `IUnitOfWork`;
- abstração de relógio quando necessária para testes determinísticos.

### Infrastructure

- `TransactionsDbContext`;
- mapeamentos EF Core;
- `FinancialTransactionRepository`;
- `OutboxMessage` e persistência no mesmo DbContext;
- publisher de Outbox executado fora da requisição HTTP.

### API

- `POST /transactions`;
- `GET /transactions/{id}`;
- `GET /transactions`.

`POST` aceita `Idempotency-Key`. A resposta de sucesso só ocorre após commit local de lançamento + Outbox.

## 6. Consolidation Context

### Domain

`DailyConsolidation`
- `Date`
- `TotalCredits`
- `TotalDebits`
- `Balance`
- `TransactionCount`
- `LastUpdatedAt`

`Balance = TotalCredits - TotalDebits`.

### Application

- `ApplyTransactionCreated`;
- `GetDailyConsolidation`;
- `ListConsolidations`.

### Infrastructure

- `ConsolidationDbContext`;
- `DailyConsolidation` projection;
- `ProcessedEvent` com unicidade por `EventId`;
- consumer Kafka;
- atualização da projeção + marcação do evento processado na mesma transação local.

### API / Worker

API:
- `GET /consolidations/{date}`;
- `GET /consolidations?from=&to=`.

Worker:
- consome `TransactionCreated`;
- valida contrato;
- aplica idempotência;
- atualiza projeção;
- confirma offset somente depois da persistência bem-sucedida.

## 7. Contrato de integração

`TransactionCreatedV1`:

```text
EventId
EventType
OccurredAt
TransactionId
TransactionType
Amount
TransactionOccurredAt
SchemaVersion
```

Não compartilhar entidades de domínio entre serviços.

## 8. Transactional Outbox

No `POST /transactions`:
1. validar comando;
2. verificar `Idempotency-Key`;
3. criar `FinancialTransaction`;
4. criar `OutboxMessage(TransactionCreatedV1)`;
5. persistir ambos no mesmo commit PostgreSQL;
6. retornar sucesso;
7. publisher assíncrono publica pendências no Kafka;
8. após confirmação do broker, marca a mensagem como publicada.

Falha após publicação e antes da marcação pode gerar redelivery. Isso é esperado e tratado pela idempotência do consumidor.

## 9. Idempotência

### Escrita HTTP

A chave de idempotência deve ter restrição única. Repetição da mesma operação não cria novo lançamento.

A política de retenção da chave permanece configurável porque o case não informa janela de idempotência.

### Consumo

`ProcessedEvent.EventId` possui restrição única. O consumer verifica/persiste o identificador dentro da mesma unidade transacional da atualização do consolidado.

## 10. Concorrência do consolidado

A atualização de uma mesma data pode sofrer concorrência. A implementação deve evitar lost update por atualização atômica ou controle otimista com retry limitado. Não será introduzido lock distribuído sem evidência de necessidade.

## 11. Tratamento de erros

- erro de validação → 400;
- recurso inexistente → 404;
- conflito de idempotência → resposta determinística sem duplicar efeito;
- falha de infraestrutura → 5xx e log correlacionado;
- falha de publicação → permanece na Outbox para retry;
- falha de consumo → retry/backoff e tratamento operacional após limite configurado.

## 12. SOLID e padrões usados com propósito

- SRP: API, Application, Domain e Infrastructure têm responsabilidades separadas;
- DIP: Application depende de interfaces, não de EF/Kafka;
- Repository: somente como boundary de persistência dos aggregates/casos de uso;
- Unit of Work: commit transacional explícito onde lançamento + Outbox e projeção + ProcessedEvent precisam atomicidade;
- Outbox: resolve dual write;
- Idempotent Consumer: resolve redelivery at-least-once.

Não serão adicionados CQRS framework, Mediator, Event Sourcing ou abstrações genéricas sem necessidade demonstrada.

## 13. Segurança

No local, autenticação pode ser simplificada para não impedir execução do desafio. Na arquitetura-alvo, autenticação/autorização ocorre no boundary HTTP e credenciais de infraestrutura ficam fora do código. Validação de entrada, TLS em produção e princípio de menor privilégio permanecem requisitos.

## 14. Observabilidade

Cada processo expõe health/readiness e logs estruturados. Correlation/trace id deve atravessar requisição, Outbox e consumo quando tecnicamente disponível. Métricas principais seguem a Observability View aprovada.

## 15. Testabilidade

A separação permite testar:
- invariantes do domínio sem infraestrutura;
- casos de uso com ports substituíveis;
- persistência/outbox/idempotência em integração;
- APIs via host de teste;
- indisponibilidade da Consolidação sem interromper Transactions;
- recuperação de backlog e redelivery.

## 16. Limites conscientes

O design não inventa regras de saldo disponível, conta, caixa, estorno, fechamento, tenant, moeda múltipla, SLA, RTO ou RPO. Essas decisões exigem informação de negócio ausente no case.