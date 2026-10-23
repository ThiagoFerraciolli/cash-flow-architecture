# Escopo de implementação

Este documento separa a arquitetura-alvo do corte final demonstrado localmente. Uma decisão arquitetural documentada não implica que toda a infraestrutura ou todos os mecanismos operacionais estejam implementados nesta entrega.

## Classificação

- **Implementar**: necessário para demonstrar o núcleo funcional e os atributos arquiteturais centrais da solução.
- **Implementar incrementalmente**: necessário na entrega, mas pode entrar após o fluxo funcional principal.
- **Implementado e demonstrado localmente**: capacidade implementada com evidência executada no ambiente Docker Compose local.
- **Projetado / homologação**: arquitetura e contrato devem permanecer coerentes; validação real depende de ambiente externo e não bloqueia a execução local.
- **Evolução**: decisão arquitetural preservada, sem obrigação de implementação no corte local deste case.

| Capacidade | Classificação | Evidência esperada |
|---|---|---|
| Estrutura .NET, APIs, Workers e testes | Implementar | build e testes automatizados |
| Transactions API e PostgreSQL | Implementar | criação/consulta e testes |
| Idempotência HTTP | Implementar | replay e conflito sem duplicação |
| BusinessDate | Implementar | testes de regra temporal |
| Transactional Outbox | Implementar | commit atômico e publicação posterior |
| RabbitMQ local | Implementar | fluxo ponta a ponta local |
| Consolidation Consumer | Implementar | consumo e atualização da projeção |
| ProcessedTransaction / idempotência do consumidor | Implementar | redelivery sem dupla contabilização |
| Daily Consolidation API | Implementar | consulta diária consistente |
| Health/readiness | Implementar | endpoints e dependências próprias |
| Testes funcionais, concorrência e indisponibilidade essenciais | Implementar | suíte automatizada local |
| Meta de 50 req/s e perda máxima de 5% | Implementado e demonstrado localmente | T15: 6.000/6.000 requisições, 50 req/s, failure rate 0%, dropped iterations 0; [evidência local](../08-testes/validacao-local-final.md) |
| Retry, DLQ e circuit breaker locais | Implementado e demonstrado localmente | testes automatizados e T09 limitado; redrive operacional completo permanece pendente |
| Reconciliação contínua/avançada | Evolução | procedimento arquitetural documentado; não implementada localmente |
| Autenticação/autorização | Implementado e demonstrado localmente | Keycloak/JWT, permissões lógicas e E2E autenticado; [evidência local](../08-testes/validacao-local-final.md) |
| Observabilidade local | Implementado e demonstrado localmente | logs estruturados, OpenTelemetry nos quatro processos, `/metrics`, OTLP/Jaeger e T14 local |
| Stack completa de observabilidade de produção | Projetado / homologação | servidores de métricas, dashboards, retenção/alertas e cloud permanecem fora do Compose |
| Adaptador SQS | Projetado / homologação | contrato e design; smoke/suíte real somente quando houver ambiente AWS |
| Cognito, IAM e KMS | Projetado / homologação | arquitetura AWS; validação em homologação futura |
| Terraform completo e pipeline de deployment AWS | Evolução | documentação e desenho suficientes para o MVP |
| Recovery administrativo por gerações | Implementado e demonstrado localmente | T10 local; snapshot read-only, geração nova, verificação, ativação atômica e lease com fencing; [rastreabilidade](../08-testes/production-grade-local-traceability.md) |
| Rebuild/reconciliação operacional avançados | Evolução | restart automático, reconciliação contínua, redrive e todos os cenários normativos T10–T12 não demonstrados |
| Restore/failover AWS e ensaios de infraestrutura | Evolução | homologação operacional futura |

## Testes

A estratégia T01–T20 continua sendo a referência normativa de cobertura arquitetural, distinta dos ensaios limitados executados. E2E e T06/T09/T10/T13/T14/T15/T18 locais estão em [validação local final](../08-testes/validacao-local-final.md) e [rastreabilidade](../08-testes/production-grade-local-traceability.md). Testes que exigem AWS real, IAM, KMS ou SQS real são evidências de homologação futura e não bloqueiam a entrega executável local. Testes locais não certificam comportamento específico da AWS.

## Regra de conclusão

Um item só muda de documentado para implementado quando existe código correspondente. Um item só muda de implementado para demonstrado quando há evidência executada e reproduzível. A documentação não autocertifica desempenho, disponibilidade, segurança ou recuperação.
