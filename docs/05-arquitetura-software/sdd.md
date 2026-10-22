# SDD — Documento de design de software

**Estado: DOCUMENTAÇÃO APROVADA.** Desenho a implementar; nenhuma classe ou teste foi desenvolvido nesta branch.

## Estrutura e dependências

C#/.NET 10 LTS, ASP.NET Core, Worker Services, EF Core 10/Npgsql, PostgreSQL 17, OpenTelemetry. RabbitMQ.Client e AWS SDK ficam em adaptadores. A implementação fixará versões de patch suportadas em lockfiles/digests e verificará compatibilidade; [política de suporte .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

```text
src/
  BuildingBlocks/CashFlow.Contracts/
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
tools/CashFlow.Recovery/
tests/
  CashFlow.Transactions.UnitTests/
  CashFlow.Transactions.IntegrationTests/
  CashFlow.Consolidation.UnitTests/
  CashFlow.Consolidation.IntegrationTests/
  CashFlow.ContractTests/
  CashFlow.EndToEndTests/
performance/k6/
```

API e Worker dependem de Application e compõem Infrastructure na raiz de composição. Infrastructure implementa portas de Application; Application depende de Domain; Domain não conhece frameworks ou broker. Contracts contém DTOs versionados. A ferramenta administrativa é execução eventual, não quinto serviço permanente de negócio.

## Casos de uso

Lançamentos: CreateTransaction, GetTransactionById e ListTransactions. Consolidação: ApplyTransaction, GetDailyConsolidation e ListDailyConsolidations. Interfaces de repositório representam operações do contexto, sem repositório genérico obrigatório. Unit of Work delimita transações; serialização e transporte ficam fora do domínio.

CreateTransaction valida token/permissão e comando, canonicaliza payload, reserva chave, cria fato e eventId, grava Outbox e resposta mínima no mesmo commit. Em conflito de chave, relê resultado e compara hash conforme ADR-005. Não há publicação na requisição HTTP.

O publicador reserva Outbox em transação curta, envia fora da transação e atualiza por token de reserva. ApplyTransaction obtém trava compartilhada do controle, verifica manutenção, insere identidade na geração ativa e atualiza totais com UPSERT relativo no mesmo commit; duplicata exige comparação do fato. Confirmação do broker ocorre após commit. Banco de leitura usa uma consulta com a geração ativa para evitar mistura durante troca.

## Modelo e precisão

Modelo normativo: [visão de dados](../04-visoes/visao-dados.md). Montantes individuais `numeric(14,2)`, totais `numeric(28,2)`, contagem bigint; C# decimal, com validação de overflow e limites explícitos. Saldo é derivado de créditos menos débitos, não atualizado independentemente. Limite de agregado excedido causa rollback/alerta e mensagem não confirmada; ampliar precisão exige revisão e migração, nunca truncamento.

BusinessDate é calculado em Lançamentos pelo fuso aprovado. Entidade imutável após confirmação; configuração de fuso não pode mudar retroativamente datas existentes. Identidade responsável: issuer e clientId do token; novos fluxos de usuário exigirão evolução auditável.

## Contratos e confiabilidade

[HTTP](contratos-http.md) é a fonte normativa de endpoints, DTOs, erros e paginação. [ADR-008](../06-adr/adr-008-contrato-e-versionamento-de-eventos.md) define o evento. [ADR-010](../06-adr/adr-010-mensageria-local-rabbitmq-e-aws-sqs.md) define os dois transportes. Portas distinguem publicar, receber e confirmar; não fingem que ack RabbitMQ é receipt handle SQS. A aplicação recebe fatos normalizados e resultado de processamento; adaptador decide confirmação e retry.

[ADR-005](../06-adr/adr-005-idempotencia-do-processamento.md) define retenção de identidades. [Procedimento de recuperação](../07-infraestrutura/recuperacao-e-reconciliacao.md) define pausa, snapshots, geração e troca atômica. Não é permitido implementar rebuild apagando a projeção ativa.

## Segurança e operação

JWT obrigatório com validação na API, permissões técnicas e claims definidos por provedor no [ADR-011](../06-adr/adr-011-autenticacao-e-autorizacao-das-apis.md). Keycloak local e Cognito AWS. Secrets fora do código, processos sem root, limites de entrada, rate limiting, TLS na AWS. Controles complementares na [visão de segurança](../04-visoes/seguranca-resiliencia-observabilidade.md).

Liveness verifica processo; readiness das APIs verifica seu próprio banco/schema/configuração, sem consultar o outro serviço ou broker. Workers expõem sinais privados de dependências e manutenção. Shutdown interrompe recebimentos, permite até 30 s para trabalho em andamento e deixa entregas não confirmadas voltarem. Reservas Outbox expiram com segurança.

## Padrões com propósito

SRP separa registro, publicação, projeção e consulta. DIP mantém domínio sem SDK. Ports & Adapters permite RabbitMQ/SQS com testes independentes. Outbox resolve dual write; idempotência resolve redelivery; Unit of Work resolve atomicidade; leitura por projeção reduz acoplamento. Não adotar framework de CQRS, Event Sourcing ou Mediator sem benefício demonstrado. Quatro processos elevam custo operacional, aceito para independência de escala e falha.

## Critérios de implementação

Seguir [estratégia de testes](estrategia-testes.md), contratos e parâmetros. Produzir migrations versionadas, OpenAPI, Compose, dois adaptadores e ferramenta administrativa apenas na etapa seguinte. Evidências devem registrar commit/ambiente/comandos e não podem ser substituídas por afirmações nesta documentação.
