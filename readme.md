# Arquitetura de Fluxo de Caixa

Solução para controle de lançamentos financeiros e consolidação diária, construída para demonstrar independência entre capacidades, consistência, resiliência e rastreabilidade entre requisitos, decisões, código e testes.

## Solução

Dois serviços de negócio — **Lançamentos** e **Consolidação** — executados em quatro processos: Transactions API, Transactions Worker/Outbox Publisher, Consolidation API e Consolidation Worker. Cada contexto possui seu PostgreSQL. A integração é assíncrona, com Transactional Outbox, entrega at-least-once e consumidor idempotente. O lançamento é confirmado após commit no banco de Transactions e não depende da disponibilidade da Consolidação nem do broker.

```mermaid
flowchart LR
    U[Consumidor autenticado] --> TA[Transactions API]
    U --> CA[Consolidation API]
    TA --> TDB[(Transactions DB)]
    TW[Outbox Publisher] --> TDB
    TW --> Q[RabbitMQ local]
    Q --> CW[Consolidation Worker]
    CW --> CDB[(Consolidation DB)]
    CA --> CDB
    Q -. falha permanente .-> D[DLQ]
```

## O que está implementado

O corte local executável usa Docker Compose, dois PostgreSQL, RabbitMQ, Keycloak e Jaeger. As APIs validam JWT e permissões por cliente. Os quatro processos emitem logs estruturados, métricas e traces OpenTelemetry. O fluxo de mensageria implementa publisher confirms, ack após commit, retry/DLQ, circuit breaker e idempotência financeira. Um [Recovery administrativo](docs/07-infraestrutura/recuperacao-e-reconciliacao.md) recompõe a projeção em nova geração e realiza a troca da geração ativa sob lease/fencing PostgreSQL.

RabbitMQ é o único transporte implementado nesta entrega. **SQS Standard é a escolha para a arquitetura-alvo AWS, mas o adaptador SQS não foi implementado nem homologado.** O [ADR-007](docs/06-adr/adr-007-estrategia-de-mensageria.md) registra a escolha tecnológica e o [ADR-010](docs/06-adr/adr-010-mensageria-local-rabbitmq-e-aws-sqs.md) descreve as diferenças semânticas entre o transporte local e o alvo AWS.

## Evidência executada

As [evidências locais finais](docs/08-testes/validacao-local-final.md) registram os limites de cada ensaio. Entre os resultados observados:

- E2E autenticado: CREDIT 100 + DEBIT 20 → créditos 100, débitos 20, saldo 80, contagem 2.
- T06: com Consolidation API, Worker e banco parados, Transactions permaneceu live/ready e aceitou dois POSTs HTTP 201; após restauração, a projeção convergiu.
- T09: mensagem permanentemente inválida chegou à DLQ; redrive completo não foi executado.
- T13: 401 sem token, 403 para cliente sem permissão de escrita, 201 para escrita autorizada e 200 para leitura autorizada.
- T14: trace transacional atravessou API → Outbox → Worker → RabbitMQ → Worker → banco de Consolidação no Jaeger local.
- T15: 6.000/6.000 GETs em 120 s a 50 req/s, 0 falhas, 0 dropped iterations, P95 4,372 ms e P99 6,052 ms.
- T18: bootstrap local limpo com migrations, serviços saudáveis, token e primeiro POST 201.
- Regressão final local: `dotnet restore --locked-mode` PASS, `dotnet build --no-restore` PASS e `dotnet test --no-build` com **89 passed, 0 failed, 0 skipped**.

Esses resultados são evidência **local**, não GitHub CI e não validação AWS. A [rastreabilidade de testes](docs/08-testes/production-grade-local-traceability.md) distingue estratégia normativa, cobertura automatizada e ensaios operacionais limitados.

## Como executar

Crie um `.env` local com os segredos exigidos pelo Compose; o arquivo é ignorado pelo Git. Em seguida:

```sh
docker compose up -d --build
docker compose ps
```

Transactions API: `127.0.0.1:8081`; Consolidation API: `127.0.0.1:8082`; Keycloak: `127.0.0.1:8080`; Jaeger: `127.0.0.1:16686`. O [guia de execução local](docs/07-infraestrutura/execucao-local.md) contém configuração, obtenção de token e comandos de validação. HTTP/AMQP sem TLS e a dependência de startup do Jaeger são conveniências do ambiente demonstrativo, não decisões de disponibilidade para produção.

## Arquitetura-alvo AWS

O **corte distribuído local** está implementado e validado com os componentes descritos acima. A [arquitetura-alvo AWS](docs/07-infraestrutura/arquitetura-aws.md) projeta ECS/Fargate, RDS, SQS Standard, ALB, Cognito, IAM, KMS e observabilidade gerenciada. Nenhuma infraestrutura AWS foi provisionada ou testada neste estudo de caso. A adoção dessa topologia exige implementação do adaptador SQS, infraestrutura, configuração de identidade e homologação no ambiente AWS.

## Guia de avaliação

Para **Arquitetura de Soluções**: [conformidade](docs/01-requisitos/matriz-conformidade.md) → [requisitos](docs/01-requisitos/requisitos-funcionais.md) e [RNFs](docs/01-requisitos/requisitos-nao-funcionais.md) → [ASRs](docs/02-asr/asr.md) → [capacidades](docs/03-arquitetura-solucao/capacidades-e-dominios.md) → [DAS](docs/03-arquitetura-solucao/das.md) → [C4 e visões](docs/04-visoes/c4.md) → [ADRs](docs/06-adr) → [AWS](docs/07-infraestrutura/arquitetura-aws.md), [custos](docs/07-infraestrutura/estimativa-custos.md) e [evidências](docs/08-testes/validacao-local-final.md).

Para **Arquitetura de Software**: [SDD](docs/05-arquitetura-software/sdd.md) → [componentes](docs/05-arquitetura-software/c4-componentes.md) → [contratos HTTP](docs/05-arquitetura-software/contratos-http.md) → [dados](docs/04-visoes/visao-dados.md) e [fluxos](docs/04-visoes/visoes-dinamicas.md) → ADRs 003–005 e 008–011 → [estratégia de testes](docs/05-arquitetura-software/estrategia-testes.md) → [evidências finais](docs/08-testes/validacao-local-final.md).

O [escopo de implementação](docs/01-requisitos/escopo-implementacao.md) é a referência para distinguir **implementado**, **demonstrado localmente**, **arquitetura-alvo** e **evolução**.
