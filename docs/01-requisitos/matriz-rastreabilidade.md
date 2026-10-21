# Matriz de Rastreabilidade

## Objetivo

A rastreabilidade conecta requisito, ASR, decisão arquitetural, implementação e evidência. ADRs, componentes e testes serão preenchidos progressivamente à medida que forem criados.

| Origem | Necessidade | ASR relacionado | Decisão/ADR | Implementação planejada | Evidência planejada |
|---|---|---|---|---|---|
| RF-002/003/004 | Registrar débito/crédito | ASR-002, ASR-004 | A definir | Transaction Service | Testes unitários e integração |
| RF-010/011 | Publicar evento sem dual-write inconsistente | ASR-002 | ADR Transactional Outbox | Transaction + Outbox na mesma transação | Teste de integração/falha |
| RF-012 | Evitar dupla contabilização | ASR-004 | ADR Idempotência | ProcessedEvents + consolidação atômica | Teste de reentrega |
| RNF-001 | Consolidação não bloqueia lançamentos | ASR-001, ASR-008 | ADR separação de serviços / integração assíncrona | Transaction e Consolidation independentes | Derrubar Consolidation e continuar lançando |
| RNF-002 | 50 req/s no consolidado | ASR-006 | A definir | API stateless e persistência adequada | Load test |
| RNF-005 | Consistência eventual | ASR-003 | ADR consistência eventual | Projeção DailyConsolidation | Teste de atraso e convergência |
| RNF-007 | Recuperar backlog | ASR-005 | ADR Kafka | Kafka consumer | Teste stop/start e backlog |
| RNF-009 | Segurança | ASR-007 | ADR autenticação local/produção | JWT local; IdP target | Testes de autenticação + documentação |
| RNF-010/011 | Observabilidade/auditoria | ASR-009 | A definir | logs, métricas, tracing/correlation | dashboards/logs/testes |
| RNF-012 | Persistência Docker | ASR-002 | A definir | volumes PostgreSQL/Kafka | restart/recreate sem perda esperada |

## Fluxo de governança

A cadeia desejada para decisões significativas é:

`RF/RNF → ASR → ADR → DAS/C4/SDD → Código → Teste → Evidência`

A matriz será atualizada em Pull Requests posteriores, evitando decisões arquiteturais desconectadas dos requisitos.