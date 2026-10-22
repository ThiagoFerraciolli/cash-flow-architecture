# Visões dinâmicas — fluxos críticos

## Registro e concorrência de chave

```mermaid
sequenceDiagram
    actor U as Cliente
    participant A as API de Lancamentos
    participant D as PostgreSQL Lancamentos
    U->>A: POST autenticado com Idempotency-Key
    A->>A: Validar e canonicalizar comando
    A->>D: BEGIN e reserva de chave unica
    alt chave nova
        A->>D: Fato + eventId + Outbox + resposta minima
        A->>D: COMMIT
        A-->>U: 201 Created
    else chave existente
        A->>D: Ler resultado vencedor apos resolver conflito
        A->>D: Encerrar transacao
        A-->>U: Replay 201 ou conflito 409
    end
```

Falha em qualquer passo antes do commit reverte tudo. Timeout pode deixar cliente sem resposta após commit; retry com mesma chave recupera a resposta. Ausência de confirmação nunca autoriza gerar uma nova chave automaticamente.

## Publicação com reserva

```mermaid
sequenceDiagram
    participant P as Publicador
    participant D as Banco Lancamentos
    participant Q as Broker
    P->>D: BEGIN, SKIP LOCKED, gravar lease/token, COMMIT
    P->>Q: Publicar fora da transacao
    alt confirmado
        Q-->>P: Confirmacao
        P->>D: Atualizar published_at condicionado ao token
    else falha
        P->>D: Registrar tentativa/backoff ou suspensao
    end
```

Queda após envio permite republicação. Reserva vencida admite captura por outra instância; atualização por token antigo é rejeitada. Item suspenso preserva trabalho e exige operação.

## Consumo idempotente

```mermaid
sequenceDiagram
    participant Q as Broker
    participant W as Consumidor
    participant D as Banco Consolidacao
    Q->>W: Evento
    W->>D: BEGIN, FOR SHARE em projection_control
    alt em manutencao
        W->>D: Encerrar sem alteracao
        W->>W: Pausar recepcao e nao confirmar entrega
    else habilitado
        W->>D: Inserir identidade na geracao ativa
        alt fato novo
            W->>D: UPSERT relativo dos totais
        else identidade repetida
            W->>D: Conferir hash e tratar conflito
        end
        W->>D: COMMIT se fato valido
        W->>Q: Ack ou DeleteMessage somente apos commit
    end
```

Identidade divergente provoca rollback e fluxo de erro definitivo; não confirmar como duplicata válida. Falha transitória permite retry; permanente segue DLQ. O ADR-010 detalha repasse confirmado RabbitMQ e política SQS.

## Reconstrução e eventos em trânsito

```mermaid
sequenceDiagram
    participant O as Operador
    participant C as Banco Consolidacao
    participant T as Banco Lancamentos
    participant W as Consumidor
    O->>C: FOR UPDATE, esperar consumos, maintenance=true, COMMIT
    W->>C: Detectar manutencao e pausar sem ack
    O->>T: Snapshot consistente de todos os fatos
    Note over T: Novos lancamentos continuam permitidos
    O->>C: Construir geracao nova: totais e identidades
    O->>O: Validar contra o mesmo snapshot
    O->>C: Trocar geracao e remover manutencao atomicamente
    W->>C: Retomar: anteriores ao snapshot sao duplicatas
    W->>C: Aplicar fatos posteriores ao snapshot
```

A pausa precede o snapshot para não perder eventos já confirmados durante a construção. Crash mantém estado persistente recuperável. A API lê geração antiga durante manutenção e nova após troca, sem mistura. Reconciliação e restore seguem o [procedimento completo](../07-infraestrutura/recuperacao-e-reconciliacao.md).

## Falhas de disponibilidade

Se toda Consolidação parar, Lançamentos continua e o transporte acumula. Se broker parar, Outbox acumula. Se retenção expirar, reconstrução usa fatos da origem. Se o banco de Lançamentos falhar, API não confirma e devolve erro retentável. Alertas de capacidade evitam interpretar acumulação como armazenamento infinito.
