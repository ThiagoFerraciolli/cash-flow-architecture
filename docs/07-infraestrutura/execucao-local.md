# Execução local

Este documento descreve **somente o ambiente executável entregue**. Arquitetura de produção AWS, SQS, Cognito, Terraform e demais evoluções ficam nos documentos de infraestrutura/ADRs e não são apresentadas aqui como componentes locais.

## Pré-requisitos

- Docker Engine com Docker Compose v2.
- `curl` e `jq` para os exemplos manuais.
- SDK .NET 10 para a validação automatizada completa.
- Internet na primeira construção/pull das imagens e restore de dependências.

O Compose usa nove serviços permanentes: quatro processos de aplicação (`transactions-api`, `transactions-worker`, `consolidation-api`, `consolidation-worker`) e cinco suportes (`postgres`, `postgres-consolidation`, `rabbitmq`, `identity`/Keycloak e `jaeger`). `migrate` e `migrate-consolidation` são jobs transitórios.

## Caminho recomendado para o avaliador

Em um clone novo, na raiz do repositório:

```sh
cp .env.example .env
sh tools/validate-local.sh
```

Esse é o **golden path** de validação. O `.env.example` contém credenciais exclusivamente locais, não sensíveis e prontas para execução. O script não exige que o avaliador descubra manualmente usuários, senhas, connection strings ou variáveis `CASHFLOW_TEST_*`.

`tools/validate-local.sh` carrega o mesmo `.env` usado pelo Compose, deriva as variáveis de integração com os usuários/bancos/portas reais da topologia, sobe os containers com `--wait`, verifica readiness, restaura dependências em modo locked, compila e executa toda a solução de testes. Qualquer falha encerra o script com status diferente de zero.

O script também cria `.env` automaticamente a partir de `.env.example` quando ele ainda não existe; o `cp` acima permanece explícito para deixar o bootstrap visível ao avaliador.

> As credenciais do `.env.example` existem apenas para localhost. Não devem ser reutilizadas em ambiente compartilhado ou produção.

## 1. Subir somente a solução

Se o objetivo for explorar a aplicação sem executar a regressão:

```sh
cp .env.example .env
docker compose up -d --build --wait
docker compose ps
```

Verifique as APIs:

```sh
curl --fail http://127.0.0.1:8081/health/live
curl --fail http://127.0.0.1:8081/health/ready
curl --fail http://127.0.0.1:8082/health/live
curl --fail http://127.0.0.1:8082/health/ready
```

Endpoints locais:

| Componente | Endereço |
|---|---|
| Transactions API | `127.0.0.1:8081` |
| Consolidation API | `127.0.0.1:8082` |
| Keycloak | `127.0.0.1:8080` |
| Jaeger UI | `127.0.0.1:16686` |
| PostgreSQL Transactions | `127.0.0.1:5433` |
| PostgreSQL Consolidation | `127.0.0.1:5434` |
| RabbitMQ AMQP | `127.0.0.1:5672` |
| RabbitMQ Management | `127.0.0.1:15672` |

Os dados persistem em `runtime-data/` e são ignorados pelo Git. `docker compose down` remove containers/rede, mas preserva esses diretórios. Removê-los manualmente é um reset destrutivo.

O Compose aguarda Jaeger saudável antes de iniciar os quatro processos de aplicação para tornar a demonstração determinística. Isso é conveniência local; observabilidade **não** é dependência de disponibilidade da arquitetura de produção.

## 2. Obter token e registrar lançamento

```sh
set -a
source .env
set +a

TOKEN=$(curl --fail --silent --show-error \
  http://127.0.0.1:8080/realms/cashflow/protocol/openid-connect/token \
  --data-urlencode 'grant_type=client_credentials' \
  --data-urlencode 'client_id=cashflow-demo' \
  --data-urlencode "client_secret=$DEMO_CLIENT_SECRET" | jq -er '.access_token')
```

Crie um crédito:

```sh
curl --fail-with-body --include http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Idempotency-Key: 607c30f2-1230-4149-8423-074104df242d' \
  --data '{"type":"CREDIT","amount":"100.00","occurredAt":"2026-10-21T12:00:00-03:00","description":"Venda"}'
```

Crie um débito com outra chave:

```sh
curl --fail-with-body --include http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Idempotency-Key: 835f19fb-d03b-4831-88b9-9edc451832df' \
  --data '{"type":"DEBIT","amount":"20.00","occurredAt":"2026-10-21T13:00:00-03:00","description":"Despesa"}'
```

Após a convergência assíncrona:

```sh
curl --fail --silent --show-error \
  http://127.0.0.1:8082/consolidations/2026-10-21 \
  -H "Authorization: Bearer $TOKEN" | jq .
```

Resultado esperado para esses dois fatos: créditos `100.00`, débitos `20.00`, saldo `80.00`, contagem `2`. A resposta mantém `consistency=EVENTUAL` e `completeness=UNKNOWN`.

Repetir exatamente o mesmo POST com a mesma identidade/chave retorna `201` com `Idempotent-Replayed: true` e não cria novo efeito. Reutilizar a chave com comando diferente retorna `409`.

## 3. Comportamento distribuído implementado

Transactions grava fato, idempotência e Outbox na mesma transação PostgreSQL. O Transactions Worker reserva a Outbox e publica `TransactionCreatedV1` no RabbitMQ com mensagem persistente, `mandatory` e publisher confirm. O Consolidation Worker consome com ACK manual após commit e mantém deduplicação por identidade financeira na geração ativa.

A entrega é **at-least-once**. Duplicatas podem existir no transporte; a idempotência impede segundo efeito financeiro. RabbitMQ é o único broker implementado. SQS Standard é arquitetura-alvo AWS e não possui adaptador nesta entrega.

Falhas permanentes podem seguir para DLQ. O T09 executado comprovou somente mensagem permanentemente inválida chegando à DLQ sem alterar a projeção; redrive operacional completo não foi demonstrado.

## 4. Testes automatizados

A forma suportada de executar a regressão completa é:

```sh
sh tools/validate-local.sh
```

Para transparência, o script equivale conceitualmente a:

```sh
set -a
source .env
set +a

export CASHFLOW_TEST_POSTGRES="Host=127.0.0.1;Port=5433;Database=cashflow;Username=cashflow;Password=${POSTGRES_PASSWORD}"
export CASHFLOW_TEST_CONSOLIDATION_POSTGRES="Host=127.0.0.1;Port=5434;Database=consolidation;Username=consolidation;Password=${CONSOLIDATION_POSTGRES_PASSWORD}"
export CASHFLOW_TEST_RABBITMQ="amqp://cashflow:${RABBITMQ_PASSWORD}@127.0.0.1:5672/"

dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
```

Não use `postgres/postgres` ou `guest/guest`: esses não são os usuários/credenciais da topologia entregue. Transactions usa usuário `cashflow`; Consolidation usa `consolidation`; RabbitMQ usa `cashflow`. O script evita divergência entre Compose e testes ao derivar tudo do mesmo `.env`.

A evidência histórica anterior às últimas correções registrou **89 passed, 0 failed, 0 skipped**. Ela não é apresentada como regressão da revisão atual; o resultado atual deve ser obtido novamente pelo comando acima antes do fechamento.

## 5. Ensaios operacionais disponíveis

Com `set -a; source .env; set +a` e o Compose saudável:

```sh
python3 tests/operational/t06_transactions_independent.py
python3 tests/operational/t09_retry_dlq.py
python3 tests/operational/t10_rebuild.py
python3 tests/performance/t15_run.py
```

- **T06:** para Consolidation API, Worker e banco; verifica Transactions live/ready e aceitação de dois lançamentos; restaura e aguarda convergência.
- **T09:** cenário limitado de mensagem inválida → DLQ; não executa redrive completo.
- **T10:** rebuild administrativo local com nova geração e lease/fencing.
- **T15:** warm-up separado de 10 s e janela medida de 120 s a 50 req/s, total esperado de 6.000 consultas.

Resultados efetivamente observados estão em [validação local final](../08-testes/validacao-local-final.md). A [estratégia T01–T20](../05-arquitetura-software/estrategia-testes.md) é normativa e contém cenários mais amplos que não foram todos executados.

## 6. Recovery e observabilidade

`tools/CashFlow.Recovery` lê a fonte Transactions em snapshot somente leitura, constrói uma nova geração e a ativa sob lease PostgreSQL renovável com fencing. Se o processo Recovery morrer, a lease expira, o Worker retoma e o operador deve iniciar um novo rebuild; não existe retomada automática do processo administrativo.

Os quatro processos emitem traces OpenTelemetry, logs estruturados e métricas em `/metrics`. Jaeger recebe traces via OTLP. Não há servidor Prometheus, Grafana ou OTel Collector no Compose entregue. `/metrics` é exposto diretamente no ambiente local; em produção deve permanecer em superfície administrativa/privada.

## 7. Limites do ambiente local

O Compose demonstra comportamento funcional, independência dos contextos, segurança local, mensageria RabbitMQ, recovery e observabilidade. Ele **não demonstra** alta disponibilidade de RabbitMQ em cluster, SQS, Cognito, IAM, KMS, WAF, ECS/Fargate, RDS Multi-AZ, Terraform, deployment AWS, RTO/RPO produtivos ou SLA.

A arquitetura-alvo está em [arquitetura-aws.md](arquitetura-aws.md), e as diferenças de transporte estão nos [ADR-007](../06-adr/adr-007-estrategia-de-mensageria.md) e [ADR-010](../06-adr/adr-010-mensageria-local-rabbitmq-e-aws-sqs.md).