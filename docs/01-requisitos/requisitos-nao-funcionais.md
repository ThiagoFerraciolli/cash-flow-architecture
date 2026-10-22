# Requisitos não funcionais

| ID | Classificação | Requisito | Verificação futura |
|---|---|---|---|
| RNF-001 | EXPLÍCITO | Queda da Consolidação não torna Lançamentos indisponível | T06: parar API, Worker e banco da Consolidação |
| RNF-002 | EXPLÍCITO | Consolidado recebe 50 req/s em pico | T15: carga de chegada constante em GET por data, hipótese documentada |
| RNF-003 | EXPLÍCITO | Perda de requisições no pico ≤ 5% | T15: timeouts, transporte, HTTP inesperado ou resposta inválida; carga emitida comprovada |
| RNF-004 | REFINADO | Commit confirma fato e trabalho de integração atomicamente | T03/T07/T08; recuperação de desastre tem RPO próprio |
| RNF-005 | REFINADO | Projeção eventualmente consistente converge após recuperação | T10/T11/T16; contrato não promete completude corrente |
| RNF-006 | REFINADO | Retry/reprocessamento não duplica fato ou efeito | T02/T05/T08/T12, inclusive retry tardio e rebuild |
| RNF-007 | REFINADO | Recuperar pendências e perda do transporte pela fonte financeira | T09–T12 |
| RNF-008 | REFINADO | Escalar processos e medir limites de banco/integração | T04/T05/T15/T16 |
| RNF-009 | REFINADO | Autenticar, autorizar, criptografar e limitar acessos | T13 e configuração AWS |
| RNF-010 | REFINADO | Diagnosticar fluxo, falhas, backlog e manutenção | T14 |
| RNF-011 | REFINADO | Preservar autoria técnica e auditoria de recuperação | T01/T10/T11/T13 |
| RNF-012 | REFINADO | Externalizar configuração e segredos | T13/T18; IAM/OIDC na cloud |
| RNF-013 | REFINADO | Isolar processo, banco e operação por contexto | T06/T17 |
| RNF-014 | REFINADO | Domínio desacoplado de persistência e broker | T19/T20; adaptadores RabbitMQ e SQS |

A tolerância de 5% refere-se às requisições do consolidado, não à perda de lançamentos confirmados. Falhas de integração são recuperáveis pela fonte; perda física da fonte depende de backup e RPO observado. Não afirmar RPO zero para desastre.

## Metas operacionais aprovadas

A [tabela de parâmetros](../07-infraestrutura/parametros-operacionais.md) fixa latência, convergência, disponibilidade interna, RTO/RPO alvo, retenção, limites e alarmes. [Estratégia de testes](../05-arquitetura-software/estrategia-testes.md) define duração, massa, ambiente e critérios. Escalabilidade não exige cache: consulta indexada à projeção é a decisão inicial; adicionar cache só após medição e revisão da consistência.

## Limites de aplicação

Interpretação de 50 req/s como consulta é hipótese de avaliação explícita; ensaio complementar mede escrita/backlog. Volume produtivo real, regulação e residência de dados são condições de adoção em organização real, não impedimentos para fechar o desenho do desafio. As escolhas de referência estão aprovadas nas premissas e ADRs.
