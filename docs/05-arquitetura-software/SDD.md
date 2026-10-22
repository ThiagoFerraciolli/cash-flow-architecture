# SDD — Documento de Design de Software

- **Status:** PRONTA PARA VALIDAÇÃO HUMANA

## 1. Objetivo

Detalhar a implementação C# coerente com os requisitos, ASRs e ADRs. A solução possui **2 serviços de negócio** (Lançamentos e Consolidação) executados por **4 processos independentes**: Transactions API, Transactions Worker/Outbox Publisher, Consolidation API e Consolidation Worker.

## 2. Stack de referência

- .NET 10 LTS / C#;
- ASP.NET Core para APIs e Worker Services;
- EF Core 10 e Npgsql;
- PostgreSQL;
- integração assíncrona por fila; Amazon SQS Standard na arquitetura AWS (ADR-002/ADR-007);
- Docker/Docker Compose para execução local;
- OpenTelemetry para telemetria;
- xUnit, WebApplicationFactory, Testcontainers e k6 para testes.

## 3. Princípios

- API/Infrastructure/Worker dependem de Application/Domain, nunca o inverso;
- Domain não conhece ASP.NET Core, EF Core, SQS ou PostgreSQL;
- Application orquestra casos de uso e define portas;
- Infrastructure implementa adaptadores;
- cada contexto mantém modelo e ownership de dados próprios;
- tecnologia de mensageria fica atrás de uma porta de publicação/consumo.

## 4. Estrutura da solução

```text
src/
  BuildingBlocks/
    CashFlow.Contracts/
  Transactions/
    CashFlow.Transactions.Domain/
    CashFlow.Transactions.Application/
    CashFlow.Transactions.Infrastructure/
    CashFlow.Transactions.Api/
    CashFlow.Transactions.Worker/
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
  CashFlow.EndToEndTests/
performance/
  k6/
```

`CashFlow.Contracts` contém apenas DTOs/contratos de integração versionados; não contém domínio compartilhado.

## 5. Contexto de Lançamentos

`FinancialTransaction`: `Id`, `Type` (`CREDIT` ou `DEBIT`), `Amount`, `Description`, `OccurredAt`, `BusinessDate`, `CreatedAt`.

Invariantes: `Amount > 0`, tipo válido e lançamento confirmado imutável no MVP.

Casos de uso: `CreateTransaction`, `GetTransactionById`, `ListTransactions`.

A API expõe `POST /transactions`, `GET /transactions/{id}` e `GET /transactions`.

### Regra de data

`BusinessDate` é derivado de `OccurredAt` usando um fuso configurável. Como o desafio não fornece fuso nem fechamento, o valor configurado para a demonstração é uma **premissa técnica do MVP**, não requisito de negócio. O valor calculado é persistido e enviado no evento para impedir interpretações divergentes entre serviços.

## 6. Contexto de Consolidação

`DailyConsolidation`: `BusinessDate`, `TotalCredits`, `TotalDebits`, `Balance`, `TransactionCount`, `LastUpdatedAt`.

`Balance = TotalCredits - TotalDebits`.

API:
- `GET /consolidations/{date}`;
- `GET /consolidations?from=&to=`.

Para data válida sem lançamentos, `GET /consolidations/{date}` retorna `200` com totais e contagem iguais a zero e `lastUpdatedAt = null`. Isso evita tratar ausência de movimento como falha e torna o teste de carga semanticamente estável.

O Worker recebe `TransactionCreatedV1`, aplica idempotência e atualiza a projeção.

## 7. Contrato de integração

O contrato é formalizado no ADR-008. Formato v1:

```json
{
  "schemaVersion": 1,
  "eventId": "uuid",
  "eventType": "TransactionCreated",
  "eventOccurredAt": "2026-10-22T12:00:00Z",
  "transactionId": "uuid",
  "transactionType": "CREDIT",
  "amount": "100.00",
  "businessDate": "2026-10-22"
}
```

`amount` é string decimal para evitar ambiguidade de ponto flutuante no JSON. Contexto técnico de rastreamento (`traceparent` e `correlation-id`) viaja em metadados da mensagem e também é persistido na Outbox.

Mudanças compatíveis adicionam campos opcionais. Mudança incompatível cria nova versão do contrato. Entidades de domínio nunca são compartilhadas.

## 8. Transactional Outbox e concorrência do Publisher

No `POST /transactions`:
1. validar requisição e idempotência;
2. criar `FinancialTransaction`;
3. criar `OutboxMessage(TransactionCreatedV1)`;
4. persistir lançamento, Outbox e registro de idempotência no mesmo commit;
5. retornar `201`;
6. Transactions Worker publica pendências na fila;
7. após confirmação do broker, marca `published_at`.

Múltiplas instâncias do Worker podem operar simultaneamente. A captura de lotes usa bloqueio transacional com `FOR UPDATE SKIP LOCKED` (ou mecanismo PostgreSQL equivalente), evitando que publishers processem a mesma linha deliberadamente. Ainda assim, falha após envio e antes de `published_at` pode duplicar a mensagem; por isso o consumidor permanece idempotente.

Outbox publicada possui retenção configurável e rotina de limpeza por lote. O período exato é parâmetro operacional, não requisito de negócio. Registros ainda não publicados nunca são removidos pela limpeza.

Campos operacionais: `attempts`, `next_attempt_at`, `last_error`, `published_at`, `headers`.

## 9. Idempotência HTTP

`Idempotency-Key` é obrigatório no `POST /transactions`.

A chave fica em tabela própria, e não na entidade financeira, permitindo política de retenção independente. O registro contém chave, hash SHA-256 do payload canônico, `transaction_id`, resposta lógica e expiração.

Sem autenticação de cliente definida pelo desafio, a chave é global no MVP. Se um identificador confiável de cliente for introduzido, a evolução recomendada é unicidade composta `(client_id, key)`.

Semântica:
- ausência da chave → `400`;
- primeira requisição válida → `201 Created`;
- mesma chave + mesmo hash dentro da janela → replay da resposta original, mantendo o status lógico original e header `Idempotent-Replayed: true`;
- mesma chave + hash diferente → `409 Conflict`;
- duas requisições simultâneas: unicidade no banco define a vencedora; a outra relê o registro e segue uma das regras anteriores.

A duração da janela é configuração do MVP e deverá ser documentada junto à implementação/configuração executável; não é requisito do case.

## 10. Idempotência e concorrência no consumo

A operação ocorre em uma única transação PostgreSQL:
1. tentar `INSERT` em `processed_events(event_id)` com `ON CONFLICT DO NOTHING`;
2. se nenhuma linha for inserida, tratar como duplicata bem-sucedida e remover a mensagem da fila;
3. se inserida, executar `UPSERT` atômico de `daily_consolidations`, incrementando crédito/débito/contagem;
4. commit;
5. somente após commit, remover a mensagem da fila.

Rollback desfaz tanto a reserva de `event_id` quanto a atualização da projeção. O UPSERT relativo evita lost update quando múltiplos consumidores atualizam a mesma data.

## 11. Falhas, retentativa, DLQ e reconciliação

O tratamento é formalizado no ADR-009.

Falha transitória (timeout, banco indisponível, deadlock) não remove a mensagem; SQS aplica nova entrega após `VisibilityTimeout`. Limites e intervalos são configuração operacional.

Após `maxReceiveCount` configurado, a redrive policy isola a mensagem na DLQ e gera sinal operacional. O runbook permite **redrive da DLQ** após correção da causa. Idempotência torna o redrive seguro.

Para detectar divergência silenciosa, existe procedimento de reconciliação por `BusinessDate`, comparando contagem e somatórios de Lançamentos com a projeção de Consolidação. No MVP é procedimento/teste E2E; automação periódica é evolução.

A fonte de verdade para eventual reconstrução é `financial_transactions`, não a fila. O mecanismo físico de rebuild será definido no runbook/implementação e deve controlar concorrência com o consumo normal e terminar com reconciliação. Não se fixa antecipadamente replay de fila nem geração obrigatória de novos `eventId`.

## 12. Tratamento HTTP

- validação → `400`;
- autenticação ausente/inválida, quando segurança estiver habilitada → `401`;
- autorização insuficiente → `403`;
- recurso individual inexistente → `404`;
- conflito de idempotência → `409`;
- infraestrutura → `5xx` com correlação.

Erros usam `application/problem+json`.

## 13. Segurança

O desafio pede critérios de segurança, mas não fornece IdP, usuários, scopes ou perfis. Portanto:
- arquitetura-alvo: OAuth2/OIDC/JWT no limite HTTP, TLS, menor privilégio e segredos externalizados;
- local: autenticação é configurável e pode usar emissor de desenvolvimento, sem segredo real versionado;
- não se inventa RBAC de negócio nem scopes como requisito do case;
- acesso do Transactions Worker à SQS: somente `SendMessage` na fila principal;
- Consolidation Worker: `ReceiveMessage/DeleteMessage/ChangeMessageVisibility` na fila principal; redrive administrativo separado quando possível;
- filas e bancos não são públicos;
- descrições, tokens e segredos não são registrados em logs.

## 14. Observabilidade

Todos os processos produzem logs estruturados e telemetria OpenTelemetry. APIs expõem health/readiness em interface não pública na arquitetura-alvo.

Métricas: taxa/erros/latência HTTP, idade do item mais antigo da Outbox, quantidade de pendências, tentativas/falhas do Publisher, `ApproximateAgeOfOldestMessage`, profundidade da fila/DLQ, falhas de consumo, tempo de processamento e idade do evento no momento da consolidação.

`lastUpdatedAt` é informação de negócio/diagnóstico, mas não é usada sozinha como indicador de defasagem.

## 15. SOLID e padrões

SRP, DIP, Repository quando útil, Unit of Work, Transactional Outbox, Idempotent Consumer e Ports & Adapters são usados para resolver necessidades concretas. Não serão introduzidos CQRS framework, Mediator, Event Sourcing ou abstrações genéricas sem necessidade.

## 16. Testabilidade

Além dos projetos unitários e de integração por serviço, `CashFlow.EndToEndTests` cobre o fluxo entre os dois serviços. Casos obrigatórios incluem:
- Consolidação indisponível enquanto novos lançamentos continuam;
- fila indisponível e posterior publicação da Outbox;
- redelivery sem dupla contabilização;
- dois POSTs simultâneos com a mesma chave;
- dois consumidores atualizando a mesma data;
- dois publishers capturando pendências;
- mensagem em DLQ e redrive;
- reconciliação/rebuild controlado;
- contrato/versionamento de `TransactionCreatedV1`;
- 50 req/s no endpoint de consolidado com taxa de falha <= 5%, declarando explicitamente a operação testada.

## 17. Limites conscientes

Não são inventadas regras de saldo disponível, conta, caixa, estorno, fechamento, tenant, moeda múltipla, SLA, RTO ou RPO. Fuso, janelas de retenção, número de tentativas e limites operacionais necessários para uma demonstração serão configurações identificadas como premissas técnicas, nunca requisitos do desafio.