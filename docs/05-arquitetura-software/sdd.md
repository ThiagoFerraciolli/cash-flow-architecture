# SDD — Documento de design de software

## Estrutura e dependências

A implementação usa C#/.NET 10, ASP.NET Core, Worker Services, Npgsql, PostgreSQL 17, RabbitMQ.Client e OpenTelemetry. O acesso a dados do corte atual usa Npgsql diretamente; **EF Core não integra a implementação entregue**. Versões restauráveis são fixadas pelos arquivos de projeto/lock e imagens do ambiente local. O AWS SDK não integra o corte executável atual porque o adaptador SQS ainda não foi implementado.

```text
src/
  BuildingBlocks/
    CashFlow.Contracts/
    CashFlow.Observability/
  Transactions/
    CashFlow.Transactions.Domain/
    CashFlow.Transactions.Application/
    CashFlow.Transactions.Infrastructure/
    CashFlow.Transactions.Api/
    CashFlow.Transactions.Worker/
  Consolidation/
    CashFlow.Consolidation.Domain/
    CashFlow.Consolidation.Application/
    CashFlow.Consolidation.Infrastructure/
    CashFlow.Consolidation.Api/
    CashFlow.Consolidation.Worker/
tools/
  CashFlow.Recovery/
tests/
  CashFlow.Transactions.UnitTests/
  CashFlow.Transactions.IntegrationTests/
  CashFlow.Consolidation.UnitTests/
  CashFlow.Consolidation.IntegrationTests/
  CashFlow.ContractTests/
  CashFlow.EndToEndTests/
  operational/
  performance/
```

API e Worker dependem de Application e compõem Infrastructure na raiz de composição. Infrastructure implementa portas de Application; Application depende de Domain; Domain não conhece frameworks ou broker. `CashFlow.Contracts` está reservado como building block, mas não contém DTO compartilhado no corte atual; o evento é serializado no produtor e validado no consumidor conforme ADR-008 e testes de contrato. A ferramenta administrativa é execução eventual, não quinto serviço permanente de negócio.

## Casos de uso

Lançamentos: CreateTransaction, GetTransactionById e ListTransactions. Consolidação: ApplyTransaction, GetDailyConsolidation e ListDailyConsolidations. Interfaces de repositório representam operações do contexto, sem repositório genérico obrigatório. As transações PostgreSQL delimitam atomicidade local; serialização e transporte ficam fora do domínio.

CreateTransaction valida autorização e comando, canonicaliza payload, reserva chave, cria fato e eventId, grava Outbox e resposta de idempotência no mesmo commit. Em conflito de chave, relê resultado e compara hash conforme ADR-005. Não há publicação no broker dentro da requisição HTTP.

O Transactions Worker reserva itens da Outbox em transação curta, publica fora da transação e atualiza o item por token de reserva. O Consolidation Worker valida o evento, aplica idempotência na geração ativa e atualiza a projeção no mesmo commit; o ACK do broker ocorre somente após commit. A leitura usa a geração ativa para evitar mistura durante uma troca de geração.

## Modelo e precisão

O modelo normativo está na [visão de dados](../04-visoes/visao-dados.md). Montantes individuais usam `numeric(14,2)`, totais `numeric(28,2)` e contagem `bigint`; C# usa `decimal`. Saldo é derivado de créditos menos débitos. BusinessDate é calculado em Lançamentos usando o fuso configurado e persistido com o fato; o consumidor não recalcula a data financeira.

## Contratos e confiabilidade

[Contratos HTTP](contratos-http.md) documenta endpoints, DTOs, erros e paginação da solução. Não há OpenAPI/Swagger implementado nesta entrega e isso não é requisito obrigatório do desafio. [ADR-008](../06-adr/adr-008-contrato-e-versionamento-de-eventos.md) define o contrato `TransactionCreated` schemaVersion 1 e explicita como ele é materializado no corte atual.

[ADR-007](../06-adr/adr-007-estrategia-de-mensageria.md) registra a estratégia de mensageria. [ADR-010](../06-adr/adr-010-mensageria-local-rabbitmq-e-aws-sqs.md) define as semânticas de transporte: RabbitMQ está implementado e demonstrado localmente; SQS Standard é arquitetura-alvo AWS e requer implementação/homologação futura. As portas de aplicação não devem assumir semânticas específicas de ack/receipt handle.

[ADR-005](../06-adr/adr-005-idempotencia-do-processamento.md) define idempotência. O [procedimento de recuperação](../07-infraestrutura/recuperacao-e-reconciliacao.md) define snapshot, nova geração, lease/fencing e troca atômica. Rebuild não apaga a projeção ativa antes da nova geração estar verificada.

## Segurança e operação

JWT é obrigatório nas rotas de negócio das APIs com issuer, audience, cliente e permissões. Keycloak implementa o provedor local; Cognito é a escolha da arquitetura-alvo AWS e não foi executado. Secrets ficam fora do código e `.env` não é versionado.

Liveness verifica o processo. Readiness das APIs depende do banco/schema do próprio contexto e não consulta o outro serviço ou broker. A dependência de startup do Jaeger no Compose é conveniência para uma demonstração determinística; observabilidade não deve ser dependência de disponibilidade na produção.

## Padrões com propósito

SRP separa registro, publicação, projeção e consulta. DIP mantém domínio sem SDK de transporte. Ports & Adapters preserva as invariantes de negócio quando o transporte muda. Outbox resolve dual write; idempotência absorve redelivery; transações locais garantem atomicidade; projeção de leitura reduz acoplamento.

CQRS framework, Event Sourcing, Kafka e Mediator não são introduzidos sem requisito que justifique a complexidade. Quatro processos aumentam custo operacional, aceito para permitir independência de implantação, escala e falha entre APIs e processamento assíncrono.

## Estado e validação

O corte local descrito neste SDD está implementado com migrations versionadas, Docker Compose, RabbitMQ, Keycloak, observabilidade e Recovery administrativo. Em **23/10/2026**, o golden path `sh tools/validate-local.sh` executou a topologia completa, restore em locked mode, build e regressão automatizada com **90 passed, 0 failed, 0 skipped**, além de **0 warnings e 0 errors** no build. Esse é o gate automatizado local corrente; execuções anteriores com 80/84/89 testes permanecem apenas como histórico de evolução.

A regressão automatizada não deve ser confundida com todos os ensaios operacionais normativos: evidências como T06, T09, T10, T13, T14, T15 e T18 possuem escopos próprios documentados em `docs/08-testes`.

Não fazem parte da implementação atual: adaptador SQS, infraestrutura AWS, Cognito real, OpenAPI/Swagger e homologação cloud. Esses itens só podem ser tratados como arquitetura-alvo ou evolução até existirem código e evidência correspondentes.
