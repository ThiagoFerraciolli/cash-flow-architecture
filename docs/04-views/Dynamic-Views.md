# Dynamic Views — Fluxos críticos

## 1. Registro de lançamento

```mermaid
sequenceDiagram
    actor Client
    participant T as Transactions API
    participant DB as Transactions DB
    participant O as Outbox Publisher
    participant K as Kafka

    Client->>T: POST /transactions + Idempotency-Key
    T->>DB: BEGIN
    T->>DB: INSERT FinancialTransaction
    T->>DB: INSERT OutboxEvent
    T->>DB: COMMIT
    T-->>Client: 201 Created
    O->>DB: busca eventos pendentes
    O->>K: publica TransactionCreated
    K-->>O: ack
    O->>DB: marca publicação
```

O sucesso HTTP depende do commit local, não da disponibilidade da Consolidação.

## 2. Consolidação normal

```mermaid
sequenceDiagram
    participant K as Kafka
    participant C as Consolidation Consumer
    participant DB as Consolidation DB

    K->>C: TransactionCreated(eventId)
    C->>DB: BEGIN
    C->>DB: verifica ProcessedEvent
    alt evento novo
        C->>DB: UPSERT DailyConsolidation
        C->>DB: INSERT ProcessedEvent
    else duplicado
        C->>C: não reaplica efeito
    end
    C->>DB: COMMIT
    C-->>K: confirma processamento
```

## 3. Consolidação indisponível

```mermaid
sequenceDiagram
    actor Client
    participant T as Transactions API
    participant DB as Transactions DB
    participant K as Kafka
    participant C as Consolidation Consumer

    Note over C: indisponível
    Client->>T: POST /transactions
    T->>DB: Transaction + Outbox
    DB-->>T: commit
    T-->>Client: sucesso
    T-->>K: publicação assíncrona
    Note over K: backlog retido
    Note over C: serviço recupera
    K->>C: entrega backlog
    C->>C: processa idempotentemente
```

Esse é o fluxo que evidencia RNF-001/ASR-001: falha da consolidação não interrompe confirmação de novos lançamentos.

## 4. Falha de publicação

Se o banco confirma lançamento/outbox e o broker está indisponível, o publisher mantém o evento pendente e tenta novamente com backoff. O lançamento não é revertido por uma falha posterior de integração.

## 5. Redelivery

Se houver entrega repetida após falha entre commit do consumidor e confirmação ao broker, `ProcessedEvent(eventId)` impede que o lançamento seja somado novamente.

## 6. Limites

- não se promete exactly-once ponta a ponta;
- não se exige ordenação global para a operação atual de soma de débitos/créditos;
- eventual necessidade futura de regras dependentes de sequência exigirá nova decisão de particionamento/ordenação.