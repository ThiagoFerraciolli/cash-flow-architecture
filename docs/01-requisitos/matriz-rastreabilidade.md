# Matriz de Rastreabilidade

## Objetivo

Esta matriz conecta requisitos de negócio, requisitos funcionais e não funcionais, ASRs, decisões arquiteturais, documentos de desenho e as futuras evidências de implementação/teste.

A cadeia de governança adotada é:

`Requisito → RF/RNF → ASR → ADR/decisão → desenho → implementação → teste/evidência`

A documentação arquitetural já possui decisões suficientes para orientar a implementação. As colunas de implementação e evidência permanecem pendentes até existirem código e resultados reais.

| Origem | Necessidade | ASR | Decisão arquitetural | Desenho relacionado | Implementação/Evidência |
|---|---|---|---|---|---|
| RN-001 / RF-001 | Registrar débito ou crédito | ASR-002, ASR-004 | ADR-003 PostgreSQL; ADR-004 Outbox; ADR-005 idempotência | DAS §4–6; SDD — Transactions | API/caso de uso + testes unitários e integração |
| RN-001 / RF-002/003 | Consultar lançamentos | — | Persistência pertencente ao contexto de Lançamentos | SDD — consultas de Transactions; C4 Component | API de consulta + testes de API |
| RN-002 / RF-004 | Produzir consolidado diário | ASR-001, 003, 004, 005 | ADR-001 decomposição; ADR-002 Kafka; ADR-004 Outbox; ADR-005 consumidor idempotente | DAS §3–7; visão de integração; SDD — Consolidation | consumidor + testes de convergência/reprocessamento |
| RN-002 / RF-005/006 | Consultar consolidado | ASR-006 | projeção própria no contexto de Consolidação | DAS §4–6; SDD; C4 Component | API de consulta + testes funcionais/carga |
| RNF-001 | Queda da consolidação não interrompe lançamentos | ASR-001, ASR-008 | ADR-001, ADR-002, ADR-004 | DAS §7; diagramas dinâmicos; arquitetura AWS | teste com consolidação indisponível |
| RNF-002/003 | 50 req/s com no máximo 5% de perda | ASR-006 | escala independente; operação exata declarada como hipótese de teste | DAS §10; estratégia de testes | teste de carga + relatório |
| RNF-004 | Lançamento confirmado permanece recuperável | ASR-002 | ADR-003 + ADR-004 | DAS §6–7; visão de dados | integração com falha do broker e recuperação |
| RNF-005 | Consolidado converge após defasagem | ASR-003 | ADR-002 + ADR-004 + ADR-005 | DAS §6–7; visão de integração/dinâmica | teste de atraso, retomada e convergência |
| RNF-006 | Evitar duplicidade/dupla contabilização | ASR-004 | ADR-005 | DAS §6; SDD — idempotência | testes de repetição/redelivery |
| RNF-007 | Recuperar trabalho pendente | ASR-005 | retenção no Kafka + Outbox + processamento idempotente | DAS §7; visão dinâmica | interrupção, backlog e recuperação |
| RNF-008 | Escalabilidade | ASR-006, ASR-008 | ADR-001 + ADR-006 | DAS §10–11; arquitetura AWS | carga local e métricas |
| RNF-009 | Segurança | ASR-007 | controles na borda, TLS, identidade corporativa e menor privilégio | DAS §8; visão de segurança; arquitetura AWS | configuração e testes de acesso |
| RNF-010 | Observabilidade | ASR-009 | OpenTelemetry + CloudWatch na arquitetura-alvo | DAS §9; visão de observabilidade; arquitetura AWS | logs, métricas, traces e health checks |
| RNF-011 | Auditabilidade | ASR-009 | correlação ponta a ponta e persistência de fatos/eventos técnicos | visão de dados + segurança/observabilidade | evidência de correlação sem dados sensíveis indevidos |
| RNF-012 | Configuração e segredos | ASR-007 | configuração externa; Secrets Manager/KMS na AWS | arquitetura AWS + SDD | configuração externa e ausência de segredo no Git |
| RNF-013 | Isolamento de falhas | ASR-001, ASR-008 | dois serviços/deployments independentes | ADR-001; DAS; C4; AWS | teste de indisponibilidade independente |
| RNF-014 | Baixo acoplamento de infraestrutura | — | dependências internas orientadas por portas; contratos de integração separados | SDD + C4 Component | testes e estrutura de projetos |
| QRNF-001/002/003/008 | SLA/RTO/RPO/DR não informados | ASR-010 | não inventar metas; mecanismos de HA/backup condicionados à validação | DAS §11; AWS; questões abertas | decisão produtiva posterior |

## Rastreabilidade das decisões principais

| Decisão | Motivação principal | Requisitos/ASRs atendidos |
|---|---|---|
| Dois serviços independentes | impedir acoplamento de disponibilidade entre lançamentos e consolidação | RNF-001, RNF-013, ASR-001, ASR-008 |
| Integração assíncrona via Kafka | desacoplamento temporal, retenção, recuperação de backlog e reprocessamento | RNF-005, RNF-007, ASR-003, ASR-005 |
| PostgreSQL | consistência transacional e modelo financeiro estruturado | RNF-004, ASR-002 |
| Transactional Outbox | evitar dual write entre banco e broker | RNF-004, RNF-007, ASR-002, ASR-005 |
| Idempotência na API e consumidor | evitar duplicação e dupla contabilização | RNF-006, ASR-004 |
| ECS/Fargate na referência AWS | execução containerizada com menor sobrecarga operacional que EKS para este escopo | RNF-008, RNF-013, ASR-008 |
| Bancos pertencentes a cada contexto | evitar acoplamento por tabelas compartilhadas/transações distribuídas | RNF-013, ASR-008 |

## Pontos deliberadamente não decididos

Não são lacunas da arquitetura documental: são informações que o desafio não fornece e que dependem de decisão de negócio/operação real:

- SLA/SLO produtivo;
- RTO e RPO;
- estratégia de recuperação entre regiões;
- região AWS produtiva;
- retenção produtiva de dados, eventos e telemetria;
- volume diário e crescimento;
- identidade corporativa/IdP específico;
- necessidade definitiva de Kafka versus alternativa de fila gerenciada após análise de custo e requisitos corporativos.

Esses itens permanecem em `questoes-em-aberto.md` e não devem ser convertidos em números fictícios.