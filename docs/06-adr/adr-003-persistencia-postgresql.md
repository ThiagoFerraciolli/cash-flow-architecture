# ADR-003 — PostgreSQL como persistência transacional de referência

- **Status:** ACEITO PARA A ARQUITETURA DO DESAFIO
- **Drivers:** ASR-002, ASR-004, integridade e auditabilidade

## Contexto

Lançamentos financeiros são estruturados e exigem atomicidade/constraints. A Consolidação precisa atualizar projeção e controle de deduplicação na mesma transação local.

## Alternativas

PostgreSQL oferece ACID, constraints, índices, maturidade e suporte gerenciado. Banco documental ou NoSQL distribuído não possui driver no volume/requisitos conhecidos que compense complexidade adicional.

## Decisão

Usar **PostgreSQL**, com ownership por serviço e sem joins/leitura direta entre contextos.

### Transactions
- `financial_transactions`;
- `idempotency_records`;
- `outbox_messages`.

### Consolidation
- `daily_consolidations`;
- `processed_events`.

Ambiente local pode usar uma instância física com databases separados. Arquitetura-alvo usa isolamento de credenciais/ownership e pode separar instâncias conforme disponibilidade/custo.

## Consequências

- migrations versionadas por contexto;
- `numeric` para valores monetários;
- constraints reforçam invariantes;
- PostgreSQL permite UPSERT atômico e `FOR UPDATE SKIP LOCKED`, usados respectivamente na concorrência da Consolidação e da Outbox;
- backup/PITR/Multi-AZ dependem de RPO/RTO/SLO ainda não fornecidos.

`ACEITO` é status da decisão arquitetural; validação de migrations, concorrência e rollback depende dos testes executados.