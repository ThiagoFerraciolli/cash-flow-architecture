# Matriz de Rastreabilidade

## Objetivo

A rastreabilidade conecta requisitos de negócio, refinamentos funcionais/não funcionais, ASRs, decisões arquiteturais, implementação e evidências. ADRs e referências de código/teste serão preenchidos conforme os próximos PRs forem concluídos.

| Origem | Necessidade | ASR relacionado | Decisão/ADR | Implementação/Evidência planejada |
|---|---|---|---|---|
| RN-001 / RF-001 | Registrar débito ou crédito | ASR-002, ASR-004 | A definir | API/caso de uso de lançamento + testes unitários/integração |
| RN-001 / RF-002/003 | Consultar lançamentos | — | A definir no SDD | API de consulta + testes de API |
| RN-002 / RF-004 | Produzir consolidado diário | ASR-001, ASR-003, ASR-004, ASR-005 | ADRs de decomposição, integração e consistência | Processamento da consolidação + testes de convergência/reprocessamento |
| RN-002 / RF-005/006 | Consultar consolidado | ASR-006 | A definir no DAS/SDD | API de consulta + testes funcionais e de carga |
| RNF-001 | Consolidação indisponível não interrompe lançamentos | ASR-001, ASR-008 | ADR de estilo/decomposição e integração | Teste de resiliência com consolidação indisponível |
| RNF-002/003 | 50 req/s com no máximo 5% de perda de requisições | ASR-006 | Decisões de capacidade na arquitetura-alvo | Teste de carga reproduzível + relatório de resultados |
| RNF-004 | Preservar lançamento e processamento após confirmação | ASR-002 | ADR após análise de dual write/alternativas | Testes de integração e cenários de falha |
| RNF-005 | Consolidado converge após defasagem temporária | ASR-003 | ADR de consistência/integração | Teste de atraso e convergência |
| RNF-006 | Evitar duplicidade/dupla contabilização | ASR-004 | Estratégia de idempotência a definir | Testes de repetição e reprocessamento |
| RNF-007 | Recuperar trabalho pendente | ASR-005 | ADR de integração/recuperação | Teste de interrupção, recuperação e backlog |
| RNF-008 | Escalabilidade | ASR-006, ASR-008 | DAS + ADR de estilo arquitetural | Arquitetura-alvo + evidência de carga local |
| RNF-009 | Segurança | ASR-007 | Security View + ADRs quando necessário | testes de acesso + documentação de consumo/integração |
| RNF-010 | Observabilidade | ASR-009 | Observability View | logs/métricas/traces/health conforme implementação |
| RNF-011 | Auditabilidade | ASR-009 | Data/Security View | evidência de correlação e auditoria sem exposição indevida |
| RNF-012 | Configuração e segredos | ASR-007 | Infrastructure/Security View | configuração externalizada e ausência de segredos no repositório |
| RNF-013 | Isolamento de falhas | ASR-001, ASR-008 | ADR de estilo/decomposição | teste de indisponibilidade independente |
| QRNF-001/002/003/008 | SLA/RTO/RPO/DR não definidos | ASR-010 | Decisões condicionadas às metas | arquitetura-alvo explicita mecanismos e dependências sem inventar metas |

## Fluxo de governança

Para decisões significativas, a cadeia será:

`Requisito de negócio → RF/RNF → ASR → alternativas → ADR → DAS/C4/SDD → código → teste → evidência`

Nem todo RF precisa gerar um ASR ou ADR. ADR será usado somente para decisões arquiteturais significativas, evitando documentação artificial.