# Matriz de rastreabilidade

**Estado: DOCUMENTAÇÃO APROVADA.** Cadeia: origem → requisito → ASR → decisão → desenho → teste. Os IDs Txx referenciam a estratégia de testes; todos dependem de implementação e execução futuras.

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
| RNF-009/012 | 007 | Segurança; IAM/JWT/TLS/secrets | T13/T18 |
| RNF-010/011 | 009 | Observabilidade/auditoria e parâmetros | T01/T10/T11/T14 |
| RNF-014 | 008 | SDD/componentes; ADR-010/adaptadores | T19/T20 |
| Execução local dos PDFs | 001/008 | Guia Compose/RabbitMQ/Keycloak | T18 e suíte local |
| Produção AWS | 008/010 | AWS/implantação/custos/recuperação | Suíte SQS real e T17 |
| Premissas PRE-007/008 | 003/004 | Contratos e limites monetários/temporais | T01/T19 |

## Fontes principais

Regras de API: contratos-http. Evento: ADR-008. Idempotência: ADR-005. Operação de mensageria: ADR-010. Reserva Outbox: ADR-004. Recuperação: recuperacao-e-reconciliacao. Defaults/metas: parametros-operacionais. As visões explicativas não redefinem esses contratos.

Relatórios futuros devem registrar commit, ambiente e resultado em docs/08-testes. Nenhuma execução é declarada validada nesta etapa documental.
