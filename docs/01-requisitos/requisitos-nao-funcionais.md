# Requisitos não funcionais

| ID | Classificação | Requisito | Verificação / evidência |
|---|---|---|---|
| RNF-001 | EXPLÍCITO | Queda da Consolidação não torna Lançamentos indisponível | T06 executado localmente em escopo limitado: API, Worker e banco da Consolidação parados; dois lançamentos aceitos e convergidos após retorno |
| RNF-002 | EXPLÍCITO | Consolidado recebe 50 req/s em pico | T15 executado localmente por 120 s: 6.000/6.000 requisições a 50 req/s |
| RNF-003 | EXPLÍCITO | Perda de requisições no pico ≤ 5% | T15 local: 0 falhas e 0 dropped iterations na janela executada; não constitui SLA produtivo |
| RNF-004 | REFINADO | Commit confirma fato e trabalho de integração atomicamente | Testes automatizados de transação/Outbox; recuperação de desastre possui RPO próprio |
| RNF-005 | REFINADO | Projeção eventualmente consistente converge após recuperação | T10 local cobre rebuild/lease/fencing em escopo limitado; T11/T16 permanecem cenários normativos |
| RNF-006 | REFINADO | Retry/reprocessamento não duplica fato ou efeito | Cobertura automatizada de idempotência; cenários operacionais adicionais permanecem na estratégia de testes |
| RNF-007 | REFINADO | Recuperar pendências e perda do transporte pela fonte financeira | T09 demonstra DLQ limitada; T10 demonstra rebuild limitado; redrive completo e restore produtivo não foram executados |
| RNF-008 | REFINADO | Escalar processos e medir limites de banco/integração | T15 mede o endpoint do Consolidado localmente; escalabilidade AWS permanece arquitetura-alvo |
| RNF-009 | REFINADO | Autenticar, autorizar, criptografar e limitar acessos | T13 demonstra JWT/permissões com Keycloak local; Cognito/IAM/TLS produtivo são arquitetura-alvo |
| RNF-010 | REFINADO | Diagnosticar fluxo, falhas, backlog e manutenção | T14 demonstra traces distribuídos e métricas no ambiente local |
| RNF-011 | REFINADO | Preservar autoria técnica e auditoria de recuperação | Cobertura local parcial; auditoria operacional produtiva permanece alvo |
| RNF-012 | REFINADO | Externalizar configuração e segredos | `.env` local ignorado pelo Git e configuração externa no Compose; Secrets Manager/IAM/OIDC são alvo AWS |
| RNF-013 | REFINADO | Isolar processo, banco e operação por contexto | Implementado localmente; T06 demonstra independência de Lançamentos diante da parada da Consolidação |
| RNF-014 | REFINADO | Domínio desacoplado de persistência e broker | Estrutura Ports & Adapters e RabbitMQ local implementados; adaptador SQS não faz parte da entrega |

A tolerância de 5% refere-se às requisições do consolidado, não à perda de lançamentos confirmados. O T15 demonstrou localmente 50 req/s por 120 segundos com 0% de falhas na janela medida; não se extrapola esse resultado para SLA, disponibilidade ou capacidade produtiva. Falhas de integração são recuperáveis pela fonte enquanto os fatos persistidos estiverem íntegros; perda física da fonte depende de backup e RPO observado.

## Metas operacionais da arquitetura

A [tabela de parâmetros](../07-infraestrutura/parametros-operacionais.md) separa requisitos explícitos do desafio de metas internas e parâmetros da arquitetura-alvo. A [estratégia de testes](../05-arquitetura-software/estrategia-testes.md) contém cenários normativos, inclusive ensaios que não foram executados integralmente. Evidência real fica em [docs/08-testes](../08-testes/validacao-local-final.md).

Escalabilidade não exige cache: consulta indexada à projeção é a decisão inicial; adicionar cache só após medição e revisão da consistência.

## Limites de aplicação

Interpretação de 50 req/s como consulta ao Consolidado é hipótese de avaliação explícita. Volume produtivo real, regulação e residência de dados são condições de adoção em organização real, não fatos fornecidos pelos PDFs. Valores adicionais de latência, RTO/RPO, retenção e disponibilidade são decisões/metas da arquitetura e não requisitos atribuídos ao cliente.