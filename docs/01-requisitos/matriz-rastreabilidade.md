# Matriz de Rastreabilidade

- **Status:** PRONTA PARA VALIDAÇÃO HUMANA — evidências de execução ainda pendentes

Cadeia: `Requisito → RF/RNF → ASR → decisão → desenho → teste/evidência`.

| Origem | Necessidade | ASR | Decisão/desenho | Evidência futura |
|---|---|---|---|---|
| RN-001 / RF-001 | registrar débito/crédito | ASR-002,004 | PostgreSQL + idempotência + Outbox | unitário/API/integração |
| RF-002/003 | consultar lançamentos | — | ownership Transactions + paginação | API |
| RN-002 / RF-004 | consolidado diário | ASR-001,003,004,005 | 2 serviços; SQS; `TransactionCreatedV1`; consumidor idempotente; `BusinessDate` | E2E/convergência/contrato |
| RF-005/006 | consultar consolidado | ASR-006 | projeção própria; data sem movimento = zero (PRE-009) | API/carga |
| RNF-001 | queda da Consolidação não interrompe lançamentos | ASR-001,008 | ADR-001/002/004; 2 serviços/4 processos | fault test |
| RNF-002/003 | 50 req/s e <=5% falha/perda | ASR-006 | escala independente; hipótese explícita `GET /consolidations/{date}` | k6 + relatório |
| RNF-004 | lançamento confirmado recuperável | ASR-002 | PostgreSQL + Outbox | falha SQS + recuperação |
| RNF-005 | convergência após defasagem | ASR-003 | SQS backlog + ADR-009 + reconciliação | atraso/retomada |
| RNF-006 | sem dupla contabilização | ASR-004 | ADR-005/008; idempotência HTTP + `ProcessedEvent` | replay/redelivery/concorrência/contrato |
| RNF-007 | recuperar pendências | ASR-005 | ADR-004/009; Outbox + SQS + DLQ/redrive + rebuild pela fonte | fault/DLQ/rebuild |
| RNF-008 | escalabilidade | ASR-006,008 | ECS/Fargate; escala por processo; UPSERT/SKIP LOCKED | carga/concorrência |
| RNF-009 | segurança | ASR-007 | TLS/OIDC quando definido/IAM mínimo/secrets | configuração/testes |
| RNF-010 | observabilidade | ASR-009 | OTel/CloudWatch + métricas Outbox/SQS/DLQ | sinais em cenários de falha |
| RNF-011 | auditabilidade técnica | ASR-009 | correlation/transaction/event ids + fonte financeira persistida | traces/logs sanitizados |
| RNF-012 | configuração/segredos | ASR-007 | externalização + Secrets Manager/KMS | revisão de config |
| RNF-013 | isolamento de falhas | ASR-001,008 | 2 serviços; bancos com ownership; 4 processos | indisponibilidade independente |
| RNF-014 | baixo acoplamento | — | ports/adapters; domínio sem SDK AWS; contrato independente do broker | architecture tests/revisão |
| QRNF-001/002/003/008 | metas produtivas ausentes | ASR-010 | não inventar SLA/RTO/RPO/DR | decisão futura |

## Decisões principais

| Decisão | Por quê | Referência |
|---|---|---|
| 2 serviços / 4 processos | isolamento de disponibilidade e processamento assíncrono independente | ADR-001 |
| SQS Standard + DLQ | atende desacoplamento/retry/backlog com menor complexidade; Kafka não é requisito | ADR-002/007 |
| PostgreSQL por contexto | atomicidade, constraints e ownership | ADR-003 |
| Transactional Outbox | elimina dual write ingênuo | ADR-004 |
| idempotência na entrada e consumo | retries/redelivery seguros | ADR-005 |
| ECS/Fargate | containerização sem complexidade de EKS não requerida | ADR-006 |
| contrato `TransactionCreatedV1` | versionamento explícito, precisão monetária e baixo acoplamento | ADR-008 |
| retry/DLQ/reconciliação/rebuild | recuperação observável sem tratar fila como histórico financeiro | ADR-009 |
| `BusinessDate` calculado uma vez | consistência temporal entre serviços | PRE-008 / ADR-008 / SDD |

## Itens deliberadamente abertos

SLA/SLO, RTO/RPO, DR/cross-region, região produtiva, retenções produtivas, volume diário/crescimento, IdP/RBAC corporativo, timezone/calendário/fechamento definitivos e limites operacionais de retry/retention.

Kafka não é mais uma decisão pendente do MVP: SQS é a escolha vigente. Kafka só é reavaliado se novos requisitos justificarem suas capacidades. Defaults específicos sugeridos no material alternativo não foram promovidos a requisitos sem evidência do case.

Nenhuma linha de evidência deve ser marcada como validada antes da execução correspondente.