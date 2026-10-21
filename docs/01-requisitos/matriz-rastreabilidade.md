# Matriz de Rastreabilidade

## Objetivo

A rastreabilidade conecta requisito, ASR, decisão arquitetural, implementação e evidência. ADRs, componentes e testes serão preenchidos progressivamente à medida que forem criados.

| Origem | Necessidade | ASR relacionado | Decisão/ADR | Implementação planejada | Evidência planejada |
|---|---|---|---|---|---|
| RF-002/003/004 | Registrar débito/crédito | ASR-002, ASR-004 | A definir | Componente de lançamentos | Testes unitários e integração |
| RF-007 | Processar consolidação diária | ASR-001, ASR-003, ASR-005 | A definir | Componente de consolidação | Testes de integração e convergência |
| RF-008/009 | Consultar consolidado | ASR-003, ASR-006 | A definir | API/projeção de consolidação | Testes de API e carga |
| RNF-001 | Consolidação não bloqueia lançamentos | ASR-001, ASR-008 | A definir | Componentes independentes | Derrubar consolidação e continuar lançando |
| RNF-002 | 50 req/s no consolidado | ASR-006 | A definir | API stateless e persistência adequada | Load test |
| RNF-004 | Evitar perda entre lançamento confirmado e processamento do consolidado | ASR-002 | ADR a criar após avaliação de alternativas | A definir | Teste de integração/falha |
| RNF-005 | Consistência eventual | ASR-003 | ADR a definir | Projeção de consolidado | Teste de atraso e convergência |
| RNF-006 | Evitar duplicidade por retry/reentrega | ASR-004 | ADR a definir | Idempotência na entrada e processamento | Testes de retry/reentrega |
| RNF-007 | Recuperar backlog | ASR-005 | ADR a definir | Processamento assíncrono | Teste stop/start e backlog |
| RNF-009 | Segurança | ASR-007 | ADR autenticação local/produção | autenticação local; IdP target | Testes de autenticação + documentação |
| RNF-010/011 | Observabilidade/auditoria | ASR-009 | A definir | logs, métricas, tracing/correlation | dashboards/logs/testes |
| RNF-012 | Persistência Docker | ASR-002 | A definir | volumes persistentes | restart/recreate sem perda esperada |

## Fluxo de governança

A cadeia desejada para decisões significativas é:

`RF/RNF → ASR → ADR → DAS/C4/SDD → Código → Teste → Evidência`

A matriz será atualizada em Pull Requests posteriores, evitando decisões arquiteturais desconectadas dos requisitos.
