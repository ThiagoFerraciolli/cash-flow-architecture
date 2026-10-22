# C4 — Componentes de software

Cada diagrama decompõe os processos de um serviço. Interfaces são portas; adaptadores de infraestrutura realizam acesso externo.

## Lançamentos

```mermaid
flowchart LR
    HTTP[API: autenticacao e contrato HTTP] --> UC[CriarLancamento]
    HTTP --> QUERY[ConsultarLancamentos]
    UC --> DOM[Entidade e invariantes]
    UC --> IDEM[Porta de idempotencia]
    UC --> REPO[Porta de fatos financeiros]
    UC --> OUT[Porta de Outbox]
    IDEM --> DB[(PostgreSQL)]
    REPO --> DB
    OUT --> DB
    QUERY --> DB
    WORK[Worker publicador] --> LEASE[Reserva e retentativa]
    LEASE --> DB
    WORK --> PUB[Porta de publicacao]
    PUB --> ADP[Adaptador RabbitMQ ou SQS]
```

CriarLancamento usa uma Unit of Work para chave, fato e Outbox. Reserva do publicador é transação diferente e curta; chamada de rede ocorre fora dela. Autenticação e autorização antecedem o caso de uso, e a identidade confiável é passada para auditoria/idempotência.

## Consolidação

```mermaid
flowchart LR
    ADP[Adaptador RabbitMQ ou SQS] --> WORK[Worker consumidor]
    WORK --> APPLY[AplicarLancamento]
    APPLY --> CONTROL[Controle de manutencao e geracao]
    APPLY --> DEDUP[Registro de identidade financeira]
    APPLY --> SUM[Atualizacao relativa dos totais]
    CONTROL --> DB[(PostgreSQL)]
    DEDUP --> DB
    SUM --> DB
    HTTP[API autenticada] --> QUERY[Consulta da geracao ativa]
    QUERY --> DB
```

Controle, identidade e totais participam da mesma transação. O adaptador confirma somente após resultado comprometido no banco; erro transitório/permanente é traduzido em operação de transporte específica. Ferramenta administrativa usa portas próprias para snapshots/gerações, sem expor acesso cruzado no serviço.

## Dependências e componentes transversais

API/Worker → Application → Domain. Infrastructure → Application/Domain quando necessário ao mapeamento. A raiz de composição referencia Infrastructure para injeção. Contratos versionados não contêm regras compartilhadas. Clock, serialização canônica, configuração validada, Problem Details e OpenTelemetry são componentes transversais. Testes de arquitetura verificam ausência de dependência de SDK/ORM no domínio.

Detalhamento no [SDD](sdd.md); contratos e decisões são referenciados em vez de reproduzidos integralmente nesta visão.
