# Escopo de implementação

Este documento separa o que foi implementado, o que foi demonstrado localmente e o que pertence à arquitetura-alvo. Uma decisão arquitetural documentada não implica implementação ou evidência executada.

## Classificação usada na entrega final

- **Implementado**: existe código/configuração correspondente no repositório.
- **Implementado e demonstrado localmente**: além do código, existe evidência executada e reproduzível no Docker Compose local.
- **Arquitetura-alvo / homologação**: decisão de produção documentada cuja implementação ou validação depende de ambiente externo.
- **Evolução**: capacidade útil, mas não necessária para cumprir o corte deste desafio e não implementada integralmente.

| Capacidade | Classificação final | Evidência / limite |
|---|---|---|
| Estrutura .NET, APIs e Workers | Implementado e demonstrado localmente | build 0 warnings/0 errors e suíte corrente de 90 testes |
| Transactions API e PostgreSQL | Implementado e demonstrado localmente | criação/consulta, E2E e testes |
| Idempotência HTTP | Implementado e demonstrado localmente | replay e conflito cobertos por testes |
| BusinessDate e regras monetárias | Implementado e demonstrado localmente | testes automatizados |
| Transactional Outbox | Implementado e demonstrado localmente | commit atômico, publicação posterior e E2E |
| RabbitMQ local | Implementado e demonstrado localmente | fluxo ponta a ponta local |
| Consolidation Consumer | Implementado e demonstrado localmente | consumo, projeção e testes com RabbitMQ/PostgreSQL reais |
| Idempotência do consumidor | Implementado e demonstrado localmente | redelivery sem segundo efeito financeiro |
| Daily Consolidation API | Implementado e demonstrado localmente | consulta diária e E2E |
| Health/readiness | Implementado e demonstrado localmente | endpoints locais e T06/T18 |
| Requisito de pico do consolidado: 50 req/s e perda/falha ≤ 5% | Demonstrado localmente em janela limitada | T15: 6.000/6.000 GETs em 120 s a 50 req/s, 0 falhas e 0 dropped iterations; não é benchmark AWS nem prova ilimitada de capacidade |
| Retry, DLQ e circuit breaker locais | Implementado; evidência operacional parcial | testes automatizados; T09 demonstrou mensagem inválida → DLQ; redrive operacional completo não executado |
| Autenticação/autorização local | Implementado e demonstrado localmente | Keycloak/JWT; permissões de escrita/leitura, T13 e E2E autenticado |
| Observabilidade local | Implementado e demonstrado localmente | logs estruturados, OpenTelemetry, `/metrics`, Jaeger e T14 |
| Recovery administrativo por gerações | Implementado e demonstrado localmente | T10 e testes de lease/fencing; operador reinicia rebuild após crash |
| OpenAPI | Evolução | contratos HTTP estão documentados; geração/publicação OpenAPI não foi implementada e não é requisito obrigatório do desafio |
| Reconciliação contínua | Evolução | procedimento arquitetural documentado; não implementada localmente |
| Redrive operacional completo | Evolução | comportamento esperado documentado; não demonstrado |
| Stack completa de observabilidade de produção | Arquitetura-alvo / homologação | dashboards, retenção, alertas e serviços cloud fora do Compose |
| SQS Standard | Arquitetura-alvo / homologação | escolha de transporte AWS; adaptador SQS não implementado |
| Cognito, IAM e KMS | Arquitetura-alvo / homologação | arquitetura AWS; não executados |
| ECS/Fargate e RDS Multi-AZ | Arquitetura-alvo / homologação | arquitetura AWS; não provisionados |
| Terraform e pipeline AWS | Evolução | ferramenta/processo escolhidos; artefatos executáveis não entregues |
| Restore/failover AWS | Evolução | procedimento arquitetural; não ensaiado |

## Evidência de testes

A estratégia T01–T20 é um mapa normativo e não uma lista de vinte cenários integralmente executados. A cobertura efetiva está em [rastreabilidade de testes](../08-testes/production-grade-local-traceability.md). E2E e T06/T09/T10/T13/T14/T15/T18 executados localmente estão em [validação local final](../08-testes/validacao-local-final.md). Em 23/10/2026, o golden path `sh tools/validate-local.sh` registrou `dotnet restore --locked-mode` PASS, `dotnet build --no-restore` PASS com **0 warnings / 0 errors** e `dotnet test --no-build` com **90 passed, 0 failed, 0 skipped**. Não houve GitHub CI nem execução AWS.

## Regra de conclusão

Um item só é classificado como **implementado** quando existe código ou configuração correspondente. Só é classificado como **demonstrado** quando existe evidência executada e reproduzível no ambiente indicado. Arquitetura-alvo, intenção de produção e estratégia de testes não autocertificam desempenho, disponibilidade, segurança ou recuperação.
