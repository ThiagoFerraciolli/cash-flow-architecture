# Requisitos arquiteturalmente significativos — ASRs

Os ASRs conectam requisitos às decisões aprovadas; não confundem escolha técnica com exigência original do PDF.

| ID | Direcionador e origem | Consequência arquitetural | Fonte da decisão |
|---|---|---|---|
| ASR-001 | Independência de Lançamentos, RNF-001 | Sem chamada à Consolidação/broker no commit; bancos e processos separados | ADR-001/002/004 |
| ASR-002 | Durabilidade do fato e trabalho, RNF-004 | Atomicidade local, Outbox, backup e RPO de desastre explícito | ADR-003/004; recuperação |
| ASR-003 | Convergência e leitura eventual, RNF-005 | Projeção própria, data financeira estável, completude desconhecida no contrato | ADR-002/008; contratos |
| ASR-004 | Efeito financeiro único, RNF-006 | Identidades persistentes, hashes, constraints, transação de deduplicação+totais | ADR-005 |
| ASR-005 | Recuperação após falha, RNF-007 | Retry/DLQ, expiração tratada, snapshot e nova geração | ADR-009/010; recuperação |
| ASR-006 | 50 req/s e perda ≤ 5%, RNF-002/003 | Consulta indexada, escala independente e ensaio de carga emitida comprovada | AWS; estratégia de testes |
| ASR-007 | Segurança e secrets, RNF-009/012 | Identidade técnica, permissões, TLS, IAM, KMS e limites de entrada | ADR-011; segurança; contratos |
| ASR-008 | Isolamento/escala/portabilidade, RNF-008/013/014 | Quatro processos; portas/adaptadores; RabbitMQ local e SQS AWS | ADR-001/006/010; SDD |
| ASR-009 | Operabilidade/auditoria, RNF-010/011 | Correlação, métricas, alarmes e autoria persistida | Observabilidade; parâmetros |
| ASR-010 | Continuidade e recuperação produtiva | Multi-AZ, PITR, corte de escrita, isolamento de transporte e reconstrução coordenada; DR regional fora do escopo | ADR-009/010; AWS; recuperação |

## Metas e trade-offs

Metas internas de disponibilidade, latência, convergência, RTO/RPO e limites estão aprovadas na tabela de parâmetros. Precisam de evidência na implementação; não são SLA contratual. Quatro processos e dois bancos custam mais que um monólito, mas isolam a capacidade que não pode derrubar Lançamentos. Recuperação por pausa controlada é mais simples que rebuild concorrente incremental, ao custo de defasagem temporária da leitura.

A escolha inicial dispensa cache e log de streaming: não há medição ou requisito que compense sua complexidade. A arquitetura permite reavaliá-los por novo ASR, sem adicioná-los preventivamente.
