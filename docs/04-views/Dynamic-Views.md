# Dynamic Views — Fluxos críticos

- **Status:** EM REVISÃO FINAL — aguardando validação humana

## 1. Registro de lançamento

```mermaid
sequenceDiagram
    actor Client
    participant T as Transactions API
    participant DB as Transactions DB
    participant O as Transactions Worker / Outbox Publisher
    participant Q as SQS

    Client->>T: POST /transactions + Idempotency-Key
    T->>DB: BEGIN
    T->>DB: INSERT idempotency_record
    T->>DB: INSERT financial_transaction
    T->>DB: INSERT outbox_message
    T->>DB: COMMIT
    T-->>Client: 201 Created
    O->>DB: captura lote pendente (SKIP LOCKED)
    O->>Q: SendMessage(TransactionCreatedV1)
    Q-->>O: ack
    O->>DB: marca published_at
```

O sucesso HTTP depende apenas do commit local. A indisponibilidade da fila ou da Consolidação não invalida um lançamento já confirmado.

## 2. Consolidação normal

```mermaid
sequenceDiagram
    participant Q as SQS
    participant C as Consolidation Worker
    participant DB as Consolidation DB

    Q->>C: TransactionCreatedV1(eventId)
    C->>DB: BEGIN
    C->>DB: INSERT processed_event ON CONFLICT DO NOTHING
    alt evento novo
        C->>DB: UPSERT atômico daily_consolidation
        C->>DB: COMMIT
        C->>Q: DeleteMessage
    else evento duplicado
        C->>DB: COMMIT
        C->>Q: DeleteMessage
    end
```

A remoção da mensagem ocorre somente depois do commit. Se o processo cair entre commit e `DeleteMessage`, a mensagem pode reaparecer e será neutralizada por `processed_event`.

## 3. Consolidação indisponível

```mermaid
sequenceDiagram
    actor Client
    participant T as Transactions API
    participant DB as Transactions DB
    participant O as Transactions Worker / Outbox Publisher
    participant Q as SQS
    participant C as Consolidation Worker

    Note over C: indisponível
    Client->>T: POST /transactions + Idempotency-Key
    T->>DB: transaction + idempotency + outbox
    DB-->>T: commit
    T-->>Client: 201 Created
    O->>DB: lê Outbox pendente
    O->>Q: publica evento
    Note over Q: mensagens permanecem disponíveis para consumo
    Note over C: processo recupera
    Q->>C: entrega pendências
    C->>C: processa idempotentemente
```

Esse fluxo evidencia RNF-001/ASR-001: a indisponibilidade da Consolidação não interrompe a confirmação de novos lançamentos. A API de Lançamentos nunca publica diretamente na fila.

## 4. Fila indisponível

```mermaid
sequenceDiagram
    participant O as Transactions Worker / Outbox Publisher
    participant DB as Transactions DB
    participant Q as SQS

    O->>DB: captura mensagem pendente
    O-xQ: SendMessage falha
    O->>DB: registra tentativa/erro e next_attempt_at
    Note over O: backoff com jitter
    O->>DB: tenta novamente quando elegível
    O->>Q: SendMessage
    Q-->>O: ack
    O->>DB: marca published_at
```

Falha de integração posterior ao commit nunca reverte o lançamento financeiro.

## 5. Falha após envio e antes de marcar Outbox

```mermaid
sequenceDiagram
    participant O as Outbox Publisher
    participant Q as SQS
    participant DB as Transactions DB

    O->>Q: SendMessage(eventId)
    Q-->>O: ack
    Note over O: processo cai antes de published_at
    O->>DB: reinicia e relê mensagem pendente
    O->>Q: republica mesmo eventId
```

Duplicação é permitida pela semântica at-least-once e tratada no consumidor.

## 6. Falha transitória no consumidor

```mermaid
sequenceDiagram
    participant Q as SQS
    participant C as Consolidation Worker
    participant DB as Consolidation DB

    Q->>C: mensagem
    C-xDB: falha transitória
    Note over C: não executa DeleteMessage
    Note over Q: visibility timeout expira
    Q->>C: redelivery
```

Após o limite configurado de recebimentos, a redrive policy move a mensagem para a DLQ e dispara alerta operacional.

## 7. Redrive da DLQ

```mermaid
sequenceDiagram
    participant OP as Operação
    participant D as DLQ
    participant Q as Fila principal
    participant C as Consolidation Worker

    OP->>OP: corrige causa e valida mensagem
    OP->>D: inicia redrive controlado
    D->>Q: retorna mensagem
    Q->>C: entrega
    C->>C: processamento idempotente
```

## 8. Reconciliação

```mermaid
sequenceDiagram
    participant R as Processo/Runbook de Reconciliação
    participant TDB as Transactions DB
    participant CDB as Consolidation DB

    R->>TDB: soma/count por BusinessDate
    R->>CDB: lê daily_consolidation da mesma data
    R->>R: compara créditos, débitos e contagem
    alt divergência
        R->>R: alerta e inicia investigação/rebuild controlado
    else consistente
        R->>R: registra sucesso
    end
```

## 9. Limites

- não se promete exactly-once ponta a ponta;
- não se exige ordenação global para soma de créditos/débitos;
- `BusinessDate` viaja no evento para manter a mesma regra temporal nos dois serviços;
- eventual regra futura dependente de sequência exige nova decisão arquitetural;
- reconstrução integral não depende de retenção histórica da fila: a fonte de verdade é o banco de Lançamentos.