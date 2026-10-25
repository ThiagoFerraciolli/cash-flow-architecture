# Matriz de rastreabilidade

| Origem/requisito | ASR | Decisão e representação | Testes |
|---|---|---|---|
| RN-001 / RF-001 | 002/004/007 | ADR-003/004/005; SDD; contrato POST | T01/T02/T03/T13 |
| RF-002/003 | 007 | Contratos de consulta/paginação; visão de dados | T01/T13/T19 |
| RN-002 / RF-004 | 003/004/005 | ADR-002/005/008/009/010; consumidor e geração | T05/T08–T12 |
| RF-005/006 | 003/006 | Contratos de projeção eventual; consulta por data/período | T01/T15/T19 |
| RNF-001/013 | 001/008 | ADR-001; bancos/processos separados; local/AWS | T06/T07/T17 |
| RNF-002/003 | 006 | Parâmetros de carga e estratégia k6 | T15 |
| RNF-004 | 002 | ADR-003/004; commit e Outbox; backup | T03/T07/T08/T17 |
| RNF-005 | 003 | Contrato completeness UNKNOWN; recuperação | T10/T11/T16 |
| RNF-006 | 004 | ADR-005/008; identidades financeiras sem expiração | T02/T05/T08/T12/T19 |
| RNF-007 | 005/010 | ADR-009; DLQ e snapshot/geração | T09–T12/T17 |
| RNF-008 | 006/008 | ADR-006; autoscaling/UPSERT/reserva | T04/T05/T15/T16 |
| RNF-009/012 | 007 | ADR-011; segurança; IAM/JWT/TLS/secrets | T13/T18 |
| RNF-010/011 | 009 | Observabilidade/auditoria e parâmetros | T01/T10/T11/T14 |
| RNF-014 | 008 | SDD/componentes; ADR-007/010; RabbitMQ local e SQS alvo | T19/T20 |
| Execução local dos PDFs | 001/008 | Guia Compose/RabbitMQ/Keycloak | T18 e suíte local |
| Produção AWS | 008/010 | AWS/implantação/custos/recuperação | Homologação SQS real e T17 — não executados |
| Premissas PRE-007/008 | 003/004 | Contratos e limites monetários/temporais | T01/T19 |

## Fontes principais

Regras de API: contratos-http. Evento: ADR-008. Idempotência: ADR-005. Estratégia de mensageria: ADR-007. Semântica de transporte: ADR-010. Autenticação/autorização: ADR-011. Reserva Outbox: ADR-004. Recuperação: recuperacao-e-reconciliacao. Defaults/metas: parametros-operacionais. As visões explicativas não redefinem esses contratos.

## Estado da evidência

As execuções locais concluídas e seus limites estão registradas em `docs/08-testes/validacao-local-final.md` e `production-grade-local-traceability.md`. O **gate automatizado local corrente, executado em 23/10/2026, possui 90 testes aprovados, 0 falhas e 0 skips, com build 0 warnings / 0 errors**. Inclui testes unitários, contrato, integração e E2E da topologia local. Contagens anteriores são apenas histórico.

T06, T09, T10, T13, T14, T15 e T18 possuem evidência operacional local nos limites documentados. Os cenários T01–T20 continuam sendo o mapa normativo de testes e não devem ser interpretados como uma suíte operacional integralmente executada. T07 completo não foi executado. AWS/SQS/T17 permanecem arquitetura e homologação futuras.

Novas evidências devem registrar ambiente, comando/roteiro, resultado e limite do que foi demonstrado. Documentação arquitetural não substitui evidência executada.
