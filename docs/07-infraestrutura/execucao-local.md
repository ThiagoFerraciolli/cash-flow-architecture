# Execução local

Este documento descreve **somente o ambiente executável entregue** e fornece um roteiro manual, reproduzível e progressivo para validar a solução localmente. Arquitetura de produção AWS, SQS, Cognito, Terraform e demais evoluções ficam nos documentos de infraestrutura/ADRs e não são apresentadas aqui como componentes locais.

O roteiro manual segue o fluxo real da arquitetura: autenticação → gravação em Transactions → idempotência → Outbox → RabbitMQ → Consolidation → resiliência → observabilidade. Em cada etapa, observe o resultado antes de avançar.

## Pré-requisitos

- Docker Engine com Docker Compose v2.
- `curl`, `jq` e `uuidgen` para os exemplos manuais.
- SDK .NET 10 para a validação automatizada completa.
- Internet na primeira construção/pull das imagens e restore de dependências.

O Compose usa nove serviços permanentes: quatro processos de aplicação (`transactions-api`, `transactions-worker`, `consolidation-api`, `consolidation-worker`) e cinco suportes (`postgres`, `postgres-consolidation`, `rabbitmq`, `identity`/Keycloak e `jaeger`). `migrate` e `migrate-consolidation` são jobs transitórios.

> Os exemplos assumem execução na raiz do repositório. Variáveis definidas no terminal, como `TOKEN`, `READER_TOKEN`, `IDEMPOTENCY_KEY` e `TRANSACTION_ID`, existem somente naquela sessão do shell.

## Caminho automatizado

Em um clone novo, a regressão automatizada completa pode ser executada com:

```sh
sh tools/validate-local.sh
```

Esse é o **golden path automatizado**. Não é necessário criar `.env` manualmente: o script carrega `.env.example` como baseline e cria `.env` automaticamente quando ele não existe. Se `.env` já existir, ele funciona como override local. O `.env.example` contém somente credenciais demonstrativas para localhost; não reutilize esses valores em ambiente compartilhado ou produção.

Para compreender e inspecionar o comportamento da arquitetura, use o roteiro manual a seguir.

---

## 1. Subir somente a solução

Se o objetivo for explorar a aplicação sem executar a regressão:

```sh
cp .env.example .env
docker compose up -d --build --wait
docker compose ps
```

> Se `.env` já existir e contiver configuração local válida, preserve-o. Os valores efetivos de `.env` prevalecem sobre o baseline de `.env.example`.

Verifique as APIs:

```sh
curl --fail http://127.0.0.1:8081/health/live
curl --fail http://127.0.0.1:8081/health/ready
curl --fail http://127.0.0.1:8082/health/live
curl --fail http://127.0.0.1:8082/health/ready
```

Os quatro comandos devem terminar com sucesso.

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

Os dados persistem em `runtime-data/` e são ignorados pelo Git. `docker compose down` remove containers/rede, mas preserva esses diretórios. Portanto, valores já existentes nas consultas não significam necessariamente que o bootstrap falhou. Remover `runtime-data/` é um reset destrutivo e não faz parte do fluxo normal deste roteiro.

O Compose aguarda Jaeger saudável antes de iniciar os quatro processos de aplicação para tornar a demonstração determinística. Isso é conveniência local; observabilidade **não** é dependência de disponibilidade da arquitetura de produção.

---

## 2. Validar autenticação e autorização

### 2.1 Carregar as variáveis locais

```sh
set -a
source .env
set +a
```

### 2.2 Obter token do `cashflow-demo`

```sh
TOKEN=$(curl --fail --silent --show-error \
  http://127.0.0.1:8080/realms/cashflow/protocol/openid-connect/token \
  --data-urlencode 'grant_type=client_credentials' \
  --data-urlencode 'client_id=cashflow-demo' \
  --data-urlencode "client_secret=$DEMO_CLIENT_SECRET" | jq -er '.access_token')
```

A atribuição acima não imprime o token. Confirme que ele foi obtido sem expor o JWT completo:

```sh
echo "$TOKEN" | cut -c1-80
```

O resultado deve começar com `eyJ`, prefixo típico de um JWT codificado.

### 2.3 Inspecionar o payload do JWT

No macOS:

```sh
echo "$TOKEN" | cut -d '.' -f2 | tr '_-' '/+' | \
  awk '{l=length($0)%4; if(l==2) $0=$0"=="; else if(l==3) $0=$0"="; print}' | \
  base64 -D | jq .
```

Confirme principalmente:

- `iss` apontando para o realm `cashflow`;
- `aud` igual a `cashflow-api`;
- `azp`/`client_id` igual a `cashflow-demo`;
- `typ` igual a `Bearer`;
- `scope` contendo `cashflow/transactions.write`, `cashflow/transactions.read` e `cashflow/consolidations.read`.

> Esse comando apenas **decodifica** o payload para inspeção. Ele não valida criptograficamente a assinatura do JWT. A validação da assinatura ocorre na API ao autenticar a requisição.

### 2.4 Provar acesso autorizado

```sh
curl --include \
  http://127.0.0.1:8082/consolidations/2026-10-21 \
  -H "Authorization: Bearer $TOKEN"
```

Resultado esperado: `HTTP/1.1 200 OK`. O conteúdo financeiro pode variar se `runtime-data/` já contiver dados.

### 2.5 Provar que autenticação é obrigatória

Repita a consulta sem `Authorization`:

```sh
curl --include \
  http://127.0.0.1:8082/consolidations/2026-10-21
```

Resultado esperado: `HTTP/1.1 401 Unauthorized`. A resposta pode incluir `traceId`, `correlationId` e o header `X-Correlation-Id` para rastreabilidade.

### 2.6 Provar autorização por escopo

Obtenha um token do cliente somente-leitura:

```sh
READER_TOKEN=$(curl --fail --silent --show-error \
  http://127.0.0.1:8080/realms/cashflow/protocol/openid-connect/token \
  --data-urlencode 'grant_type=client_credentials' \
  --data-urlencode 'client_id=cashflow-reader' \
  --data-urlencode "client_secret=$READER_CLIENT_SECRET" | jq -er '.access_token')

echo "$READER_TOKEN" | cut -c1-80
```

Tente escrever em Transactions com esse token:

```sh
curl --include \
  http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $READER_TOKEN" \
  -H "Idempotency-Key: $(uuidgen | tr '[:upper:]' '[:lower:]')" \
  --data '{
    "type":"CREDIT",
    "amount":"100.00",
    "occurredAt":"2026-10-25T12:00:00-03:00",
    "description":"Teste de autorizacao"
  }'
```

Resultado esperado: `HTTP/1.1 403 Forbidden`.

A diferença validada é:

```text
sem credencial válida                    -> 401 Unauthorized
credencial válida sem escopo de escrita  -> 403 Forbidden
cashflow-demo com escopo adequado         -> operação autorizada
```

---

## 3. Registrar um lançamento e seguir sua persistência

Para evitar colisão com execuções anteriores, gere uma chave nova:

```sh
IDEMPOTENCY_KEY=$(uuidgen | tr '[:upper:]' '[:lower:]')
echo "$IDEMPOTENCY_KEY"
```

Crie um crédito:

```sh
curl --include \
  http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: $IDEMPOTENCY_KEY" \
  --data '{
    "type":"CREDIT",
    "amount":"100.00",
    "occurredAt":"2026-10-25T12:00:00-03:00",
    "description":"Teste manual - credito"
  }'
```

Resultado esperado: `HTTP/1.1 201 Created`.

Observe no response:

- `Location: /transactions/<uuid>`;
- `X-Correlation-Id`;
- `id` do lançamento;
- `businessDate` igual a `2026-10-25`;
- `occurredAt` normalizado para UTC (`2026-10-25T15:00:00Z`).

Copie o `id` retornado para uma variável. Exemplo:

```sh
TRANSACTION_ID='<id-retornado-pela-api>'
echo "$TRANSACTION_ID"
```

### 3.1 Entrar no PostgreSQL de Transactions

```sh
docker compose exec postgres psql -U cashflow -d cashflow
```

O prompt deve mudar para:

```text
cashflow=#
```

Liste as tabelas:

```sql
\dt
```

Devem existir, entre outras, as tabelas:

```text
financial_transactions
idempotency_records
outbox_messages
local_schema_migrations
```

> O `psql` pode abrir resultados longos no pager `less`. Quando aparecer `(END)`, pressione `q` para retornar ao prompt `cashflow=#`.

### 3.2 Inspecionar o fato financeiro

Opcionalmente veja primeiro o schema:

```sql
\d financial_transactions
```

Consulte o lançamento usando o UUID retornado pela API, substituindo `<TRANSACTION_ID>`:

```sql
SELECT
    id,
    creation_event_id,
    type,
    amount,
    description,
    occurred_at,
    business_date,
    created_at,
    actor_issuer,
    actor_client_id
FROM financial_transactions
WHERE id = '<TRANSACTION_ID>';
```

Resultado esperado: exatamente uma linha com `CREDIT`, `100.00`, `business_date=2026-10-25` e `actor_client_id=cashflow-demo`.

Observe também `creation_event_id`: ele identifica o evento de criação associado ao fato e será usado para relacionar o lançamento à Outbox.

### 3.3 Inspecionar a identidade idempotente

Veja a estrutura:

```sql
\d idempotency_records
```

A chave primária é composta por `issuer`, `client_id` e `idempotency_key`. Isso evita tratar o UUID isoladamente como identidade global entre clientes diferentes.

Consulte a chave gerada no shell, substituindo `<IDEMPOTENCY_KEY>`:

```sql
SELECT
    issuer,
    client_id,
    idempotency_key,
    request_hash,
    canonical_version,
    transaction_id,
    response_status,
    response_body,
    created_at
FROM idempotency_records
WHERE idempotency_key = '<IDEMPOTENCY_KEY>';
```

Confirme:

- `client_id = cashflow-demo`;
- `transaction_id` igual ao lançamento retornado pela API;
- `response_status = 201`;
- `request_hash` preenchido;
- `response_body` contendo a resposta persistida para replay.

### 3.4 Inspecionar a Outbox

Veja primeiro o schema real:

```sql
\d outbox_messages
```

Depois localize a mensagem pelo lançamento:

```sql
SELECT *
FROM outbox_messages
WHERE aggregate_id = '<TRANSACTION_ID>';
```

Confirme que existe uma mensagem ligada ao mesmo `aggregate_id`. Compare também o identificador do evento com o `creation_event_id` de `financial_transactions`.

A existência do fato financeiro, do registro idempotente e da Outbox relacionados demonstra as três persistências que compõem o write path de Transactions. O desenho implementado grava essas estruturas na mesma transação PostgreSQL.

Saia do `psql`:

```sql
\q
```

---

## 4. Acompanhar Outbox, RabbitMQ e Consolidation

O fluxo esperado após o commit local é:

```text
Transactions API
      |
      v
PostgreSQL Transactions
  fato + idempotência + Outbox
      |
      v
Transactions Worker
      |
      v
RabbitMQ
      |
      v
Consolidation Worker
      |
      v
PostgreSQL Consolidation
      |
      v
Consolidation API
```

### 4.1 Inspecionar RabbitMQ

Confirme o estado do broker:

```sh
docker compose exec rabbitmq rabbitmqctl list_queues name messages_ready messages_unacknowledged consumers
```

Também é possível usar a interface de gerenciamento em `127.0.0.1:15672`, com as credenciais efetivas configuradas no `.env`.

Em estado saudável e após convergência, não é obrigatório encontrar a mensagem ainda pronta na fila: o worker pode publicá-la e o consumidor processá-la rapidamente. O objetivo desta inspeção é verificar filas, consumidores e eventual backlog, não usar a presença instantânea da mensagem como requisito de sucesso.

### 4.2 Consultar a projeção consolidada

```sh
curl --fail --silent --show-error \
  http://127.0.0.1:8082/consolidations/2026-10-25 \
  -H "Authorization: Bearer $TOKEN" | jq .
```

Se a data estava vazia antes do ensaio, o crédito criado deve produzir `totalCredits=100.00`, `totalDebits=0.00`, `balance=100.00` e `transactionCount=1` após convergência. Se `runtime-data/` já possuía lançamentos nessa data, valide o **incremento** provocado pelo novo fato, e não valores absolutos.

A resposta mantém semântica de consistência eventual; portanto, uma consulta feita imediatamente após o POST pode ocorrer antes da projeção convergir.

### 4.3 Inspecionar o banco de Consolidation

```sh
docker compose exec postgres-consolidation psql -U consolidation -d consolidation
```

Liste e inspecione a estrutura existente:

```sql
\dt
```

Use `\d <nome-da-tabela>` nas tabelas de projeção/deduplicação identificadas e consulte registros referentes a `2026-10-25` e/ou ao `TRANSACTION_ID`. O objetivo é comprovar que o resultado retornado pela Consolidation API corresponde à projeção persistida, e não a uma leitura síncrona do banco de Transactions.

Saia com:

```sql
\q
```

---

## 5. Validar idempotência

Use o **mesmo** `IDEMPOTENCY_KEY`, o mesmo cliente e exatamente o mesmo comando da seção 3:

```sh
curl --include \
  http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: $IDEMPOTENCY_KEY" \
  --data '{
    "type":"CREDIT",
    "amount":"100.00",
    "occurredAt":"2026-10-25T12:00:00-03:00",
    "description":"Teste manual - credito"
  }'
```

Resultado esperado:

- `201 Created`;
- header `Idempotent-Replayed: true`;
- mesmo `id`/resposta persistida;
- nenhum segundo efeito financeiro.

Confirme no banco de Transactions que continua existindo somente um fato para o `TRANSACTION_ID` e um registro idempotente para a identidade testada.

### 5.1 Mesma chave com comando diferente

Mantenha a mesma chave, mas altere o comando:

```sh
curl --include \
  http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: $IDEMPOTENCY_KEY" \
  --data '{
    "type":"CREDIT",
    "amount":"101.00",
    "occurredAt":"2026-10-25T12:00:00-03:00",
    "description":"Teste manual - comando diferente"
  }'
```

Resultado esperado: `409 Conflict`. O `request_hash` permite distinguir replay legítimo de reutilização conflitante da mesma identidade idempotente.

---

## 6. Validar independência de Transactions

O objetivo é demonstrar que indisponibilidade temporária do contexto de Consolidation não impede o write path de Transactions.

Antes do ensaio, gere uma nova chave:

```sh
OUTAGE_KEY=$(uuidgen | tr '[:upper:]' '[:lower:]')
```

Pare o contexto de Consolidation:

```sh
docker compose stop consolidation-api consolidation-worker postgres-consolidation
```

Confirme que Transactions continua saudável:

```sh
curl --fail http://127.0.0.1:8081/health/live
curl --fail http://127.0.0.1:8081/health/ready
```

Registre um novo fato:

```sh
curl --include \
  http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: $OUTAGE_KEY" \
  --data '{
    "type":"DEBIT",
    "amount":"20.00",
    "occurredAt":"2026-10-25T13:00:00-03:00",
    "description":"Teste manual - consolidation indisponivel"
  }'
```

Resultado esperado: `201 Created` mesmo com Consolidation indisponível.

Restaure o contexto:

```sh
docker compose start postgres-consolidation consolidation-worker consolidation-api
```

Aguarde os serviços ficarem saudáveis:

```sh
docker compose ps
```

Consulte novamente a consolidação e confirme convergência posterior. Esse ensaio demonstra desacoplamento temporal entre a aceitação do lançamento e sua projeção.

---

## 7. Validar indisponibilidade temporária do RabbitMQ

Este ensaio demonstra a função da Outbox quando o transporte está temporariamente indisponível.

Gere uma chave exclusiva:

```sh
BROKER_OUTAGE_KEY=$(uuidgen | tr '[:upper:]' '[:lower:]')
```

Pare o broker:

```sh
docker compose stop rabbitmq
```

Registre um novo lançamento:

```sh
curl --include \
  http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: $BROKER_OUTAGE_KEY" \
  --data '{
    "type":"CREDIT",
    "amount":"10.00",
    "occurredAt":"2026-10-25T14:00:00-03:00",
    "description":"Teste manual - broker indisponivel"
  }'
```

A aceitação do fato deve depender do PostgreSQL de Transactions, não da disponibilidade instantânea do broker. Inspecione `financial_transactions` e `outbox_messages` para confirmar a persistência local.

Restaure RabbitMQ:

```sh
docker compose start rabbitmq
```

Acompanhe os containers:

```sh
docker compose ps
docker compose logs --tail=100 transactions-worker consolidation-worker
```

Depois consulte Consolidation e confirme que a projeção converge quando o transporte volta a ficar disponível.

---

## 8. Retry e DLQ

O ensaio operacional disponível para falha permanente é:

```sh
python3 tests/operational/t09_retry_dlq.py
```

O cenário implementado comprova mensagem permanentemente inválida chegando à DLQ sem alterar a projeção. Ele **não** demonstra um redrive operacional completo.

Para inspeção manual do broker durante o ensaio:

```sh
docker compose exec rabbitmq rabbitmqctl list_queues name messages_ready messages_unacknowledged consumers
```

Use também os logs do consumidor:

```sh
docker compose logs --tail=200 consolidation-worker
```

---

## 9. Recovery e rebuild

`tools/CashFlow.Recovery` lê a fonte Transactions em snapshot somente leitura, constrói uma nova geração e a ativa sob lease PostgreSQL renovável com fencing. Se o processo Recovery morrer, a lease expira, o Worker retoma e o operador deve iniciar um novo rebuild; não existe retomada automática do processo administrativo.

O ensaio operacional disponível é:

```sh
python3 tests/operational/t10_rebuild.py
```

Durante a validação, compare a geração ativa antes e depois do rebuild e confirme que a nova projeção é ativada sem transformar o banco de Transactions em dependência síncrona da Consolidation API.

---

## 10. Observabilidade

Os quatro processos emitem traces OpenTelemetry, logs estruturados e métricas em `/metrics`. Jaeger recebe traces via OTLP.

### 10.1 Logs

```sh
docker compose logs --tail=100 transactions-api
docker compose logs --tail=100 transactions-worker
docker compose logs --tail=100 consolidation-worker
docker compose logs --tail=100 consolidation-api
```

Use o `X-Correlation-Id` retornado pelas APIs para relacionar requisições e eventos nos logs quando aplicável.

### 10.2 Métricas

```sh
curl --fail http://127.0.0.1:8081/metrics
curl --fail http://127.0.0.1:8082/metrics
```

O Compose não inclui servidor Prometheus ou Grafana. `/metrics` é exposto diretamente no ambiente local; em produção deve permanecer em superfície administrativa/privada.

### 10.3 Traces

Abra a Jaeger UI em `127.0.0.1:16686` e procure traces dos processos da solução após executar um POST e uma consulta de consolidação.

A observabilidade local ajuda a seguir o caminho entre HTTP, persistência, publicação e consumo, mas não altera a semântica funcional da arquitetura.

---

## 11. Performance

O ensaio disponível é:

```sh
python3 tests/performance/t15_run.py
```

O T15 usa warm-up separado de 10 s e janela medida de 120 s a 50 req/s, total esperado de 6.000 consultas. Registre o resultado observado na execução em vez de inferir performance apenas pela configuração do teste.

---

## 12. Regressão automatizada completa

Depois da exploração manual, execute o gate automatizado:

```sh
sh tools/validate-local.sh
```

Para transparência, o script carrega `.env.example`, aplica opcionalmente `.env`, deriva `CASHFLOW_TEST_*`, executa `docker compose up -d --build --wait`, verifica readiness e então executa:

```sh
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
```

Não use `postgres/postgres` ou `guest/guest`: esses não são os usuários/credenciais da topologia entregue. Transactions usa usuário `cashflow`; Consolidation usa `consolidation`; RabbitMQ usa `cashflow`. O script evita divergência entre Compose e testes ao derivar tudo da mesma configuração.

O golden path final registrado em 23/10/2026 foi **90 passed, 0 failed, 0 skipped**, com build **0 warnings / 0 errors**. Esse número é evidência daquela execução; uma nova avaliação deve considerar como resultado corrente aquilo que o comando reportar no ambiente atual.

Resultados anteriormente observados estão em [validação local final](../08-testes/validacao-local-final.md). A [estratégia T01–T20](../05-arquitetura-software/estrategia-testes.md) é normativa e contém cenários mais amplos que não foram todos executados.

---

## 13. O que cada etapa comprova

| Etapa | Evidência principal | Propriedade observada |
|---|---|---|
| Health checks | `/health/live` e `/health/ready` | processos disponíveis/prontos |
| JWT válido | `200` em endpoint protegido | autenticação aceita |
| Sem JWT | `401` | autenticação obrigatória |
| Reader escrevendo | `403` | autorização por escopo |
| POST Transactions | `201` + UUID | comando aceito |
| `financial_transactions` | fato persistido | durabilidade do write path |
| `idempotency_records` | chave + hash + resposta | identidade/replay idempotente |
| `outbox_messages` | evento associado ao fato | publicação desacoplada do commit HTTP |
| RabbitMQ | filas/consumidores | transporte assíncrono local |
| Consolidation | projeção persistida + API | consistência eventual |
| Replay | `201` + `Idempotent-Replayed: true` | sem segundo efeito financeiro |
| Payload diferente | `409` | proteção contra reutilização conflitante |
| Consolidation parada | Transactions continua aceitando | independência entre contextos |
| Broker parado | fato/Outbox persistem | tolerância à indisponibilidade temporária do transporte |
| T09 | mensagem inválida em DLQ | tratamento de falha permanente no escopo demonstrado |
| T10 | nova geração | rebuild/recovery |
| Logs/métricas/traces | correlation/telemetria | observabilidade |
| T15 | resultado medido | comportamento sob carga do cenário definido |
| `validate-local.sh` | build + testes | regressão automatizada |

---

## 14. Limites do ambiente local

O Compose demonstra comportamento funcional, independência dos contextos, segurança local, mensageria RabbitMQ, recovery e observabilidade. Ele **não demonstra** alta disponibilidade de RabbitMQ em cluster, SQS, Cognito, IAM, KMS, WAF, ECS/Fargate, RDS Multi-AZ, Terraform, deployment AWS, RTO/RPO produtivos ou SLA.

RabbitMQ é o único broker implementado nesta entrega. SQS Standard pertence à arquitetura-alvo AWS e não possui adaptador executável no ambiente local.

A arquitetura-alvo está em [arquitetura-aws.md](arquitetura-aws.md), e as diferenças de transporte estão nos [ADR-007](../06-adr/adr-007-estrategia-de-mensageria.md) e [ADR-010](../06-adr/adr-010-mensageria-local-rabbitmq-e-aws-sqs.md).
