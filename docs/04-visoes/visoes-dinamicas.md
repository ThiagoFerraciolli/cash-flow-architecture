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

Falha em qualquer passo antes do commit reverte tudo. Timeout pode deixar cliente sem resposta após commit; retry com a mesma chave recupera a resposta. Ausência de confirmação nunca autoriza gerar nova chave automaticamente.

## Publicação com reserva

```mermaid
sequenceDiagram
    participant P as Publicador
    participant D as Banco Lancamentos
    participant Q as RabbitMQ
    P->>D: BEGIN, SKIP LOCKED, gravar lease/token, COMMIT
    P->>Q: Publicar fora da transacao
    alt confirmado
        Q-->>P: Publisher confirm
        P->>D: Atualizar published_at condicionado ao token
    else falha
        P->>D: Registrar tentativa/backoff ou suspensao
    end
```

Queda após envio permite republicação. Reserva vencida admite captura por outra instância; atualização por token antigo é rejeitada. Item suspenso preserva trabalho e exige operação. Este fluxo descreve a implementação RabbitMQ local; SQS pertence à arquitetura-alvo.

## Consumo idempotente

```mermaid
sequenceDiagram
    participant Q as RabbitMQ
    participant W as Consumidor
    participant D as Banco Consolidacao
    Q->>W: Evento
    W->>D: BEGIN, trava compartilhada em projection_control
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
        W->>Q: Ack somente apos commit
    end
```

Identidade divergente provoca rollback e fluxo de erro definitivo; não confirmar como duplicata válida. Falha transitória permite retry; permanente segue DLQ. O ADR-010 distingue esse comportamento RabbitMQ da especificação futura de SQS.

## Reconstrução com lease e fencing

```mermaid
sequenceDiagram
    participant O as Recovery
    participant C as Banco Consolidacao
    participant T as Banco Lancamentos
    participant W as Consumidor
    O->>C: Adquirir lease exclusiva + owner token + expiracao
    O->>C: maintenance=true sob ownership
    W->>C: Detectar manutencao e pausar sem ack
    O->>T: Ler snapshot consistente dos fatos
    Note over T: Novos lancamentos continuam permitidos
    loop durante o rebuild
        O->>C: Renovar lease condicionada ao owner token
    end
    O->>C: Construir nova geracao
    O->>O: Validar resultado contra o snapshot
    O->>C: Ativar nova geracao e liberar manutencao com fencing
    W->>C: Retomar processamento
```

A lease impede dois rebuilds administrativos válidos simultaneamente. Renovação e liberação exigem o mesmo owner token; um processo antigo que perdeu a lease não pode ativar, renovar ou liberar a manutenção depois que outro owner assumiu.

Se o processo Recovery morrer, o estado não é automaticamente retomado pelo próprio processo. Após expirar `maintenance_expires_at`, o Worker deixa de considerar a manutenção válida e volta ao processamento normal. O operador deve iniciar um novo rebuild, que adquire nova lease e novo token. Esse comportamento de expiração/fencing foi exercitado no T10 local em escopo limitado.

A API de Consolidação serve a geração ativa anterior durante a construção e passa à nova somente após a troca válida, evitando mistura entre gerações. O snapshot é materializado em memória na ferramenta local como trade-off aceitável para o estudo de caso; uma implementação produtiva com volume elevado deve processar incrementalmente sem abandonar lease, geração e fencing. Reconciliação e restore seguem o [procedimento completo](../07-infraestrutura/recuperacao-e-reconciliacao.md).

## Falhas de disponibilidade

Se toda a Consolidação parar, Lançamentos continua e o transporte acumula. Se RabbitMQ parar, a Outbox acumula e o caminho HTTP de Lançamentos continua dependente apenas de seu próprio banco e identidade já validável. Se a retenção do transporte produtivo expirar, a estratégia de recuperação depende dos fatos persistidos na origem. Se o banco de Lançamentos falhar, a API não confirma novo lançamento. Alertas de capacidade evitam interpretar acumulação como armazenamento infinito.

A dependência de `jaeger` saudável usada no Docker Compose é conveniência de bootstrap determinístico do ambiente local; observabilidade não deve ser dependência de disponibilidade dos processos na arquitetura de produção.