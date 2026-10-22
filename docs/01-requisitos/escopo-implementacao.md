# Escopo de implementação

Este documento separa a arquitetura-alvo do que precisa ser codificado e demonstrado na entrega incremental. Uma decisão arquitetural documentada não implica que toda a infraestrutura ou todos os mecanismos operacionais devam ser implementados no primeiro ciclo.

## Classificação

- **Implementar**: necessário para demonstrar o núcleo funcional e os atributos arquiteturais centrais da solução.
- **Implementar incrementalmente**: necessário na entrega, mas pode entrar após o fluxo funcional principal.
- **Projetado / homologação**: arquitetura e contrato devem permanecer coerentes; validação real depende de ambiente externo e não bloqueia a execução local.
- **Evolução**: decisão arquitetural preservada, sem obrigação de implementação no MVP.

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
| Meta de 50 req/s e perda máxima de 5% | Implementar incrementalmente | teste de carga reproduzível no ambiente declarado |
| Retry, DLQ/redrive e circuit breaker necessários ao cenário local | Implementar incrementalmente | testes de falha e recuperação |
| Reconciliação básica | Implementar incrementalmente | comparação origem/projeção |
| Autenticação/autorização | Implementar incrementalmente | mecanismo local compatível com ADR-011; Keycloak pode ser usado quando necessário à demonstração |
| Observabilidade básica | Implementar incrementalmente | logs, métricas/trace essenciais ao diagnóstico |
| Adaptador SQS | Projetado / homologação | contrato e design; smoke/suíte real somente quando houver ambiente AWS |
| Cognito, IAM e KMS | Projetado / homologação | arquitetura AWS; validação em homologação futura |
| Terraform completo e pipeline de deployment AWS | Evolução | documentação e desenho suficientes para o MVP |
| Recovery/rebuild completo por gerações | Evolução | procedimento documentado; implementação posterior |
| Restore/failover AWS e ensaios de infraestrutura | Evolução | homologação operacional futura |

## Testes

A estratégia T01–T20 continua sendo a referência de cobertura arquitetural, mas a disponibilidade de cada evidência acompanha a classificação da capacidade correspondente. Testes que exigem AWS real, IAM, KMS ou SQS real são evidências de homologação e não bloqueiam a entrega executável local. Testes locais não devem ser apresentados como certificação de comportamento específico da AWS.

## Regra de conclusão

Um item só muda de documentado para implementado quando existe código correspondente. Um item só muda de implementado para demonstrado quando há evidência executada e reproduzível. A documentação não autocertifica desempenho, disponibilidade, segurança ou recuperação.
