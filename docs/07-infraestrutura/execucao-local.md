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

A resposta terá formato semelhante a:

```text
HTTP/1.1 201 Created
Location: /transactions/92588f23-7a57-47ce-960a-48c5d5566d11
X-Correlation-Id: 06a9d46b-a899-49ef-b46c-3d8d8efb8f92

{"id":"92588f23-7a57-47ce-960a-48c5d5566d11", ...}
```

O UUID que aparece depois de `/transactions/` no header `Location` é o mesmo valor do campo `id` no JSON. Esse é o identificador do lançamento que acabou de ser criado.

Guarde esse UUID em uma variável do terminal para reutilizá-lo nas consultas seguintes. **Não execute outro POST**; o comando abaixo apenas atribui um nome ao ID já retornado pela API.

Por exemplo, se a resposta retornou:

```text
Location: /transactions/92588f23-7a57-47ce-960a-48c5d5566d11
```

execute:

```sh
TRANSACTION_ID='92588f23-7a57-47ce-960a-48c5d5566d11'
echo "$TRANSACTION_ID"
```

O terminal deverá imprimir:

```text
92588f23-7a57-47ce-960a-48c5d5566d11
```

> Nas próximas seções, sempre que aparecer `$TRANSACTION_ID`, ele representa exatamente esse lançamento. Se estiver executando o roteiro em outra sessão, use o UUID retornado pela **sua própria** chamada à API, e não o UUID do exemplo acima.

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

Como `$TRANSACTION_ID` é uma variável do **shell** e não existe dentro do `psql`, copie o UUID armazenado nela e use-o na consulta SQL abaixo.

No terminal, antes de entrar no `psql`, você pode conferir novamente o valor com:

```sh
echo "$TRANSACTION_ID"
```

Dentro do `psql`, substitua `<TRANSACTION_ID>` pelo UUID exibido. Exemplo:

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
WHERE id = '92588f23-7a57-47ce-960a-48c5d5566d11';
```

Use o UUID da sua execução no lugar do UUID acima.

Resultado esperado: exatamente uma linha com `CREDIT`, `100.00`, `business_date=2026-10-25` e `actor_client_id=cashflow-demo`.

Observe também `creation_event_id`: ele identifica o evento de criação associado ao fato e será usado para relacionar o lançamento à Outbox.

### 3.3 Inspecionar a identidade idempotente

Veja a estrutura:

```sql
\d idempotency_records
```

A chave primária é composta por `issuer`, `client_id` e `idempotency_key`. Isso evita tratar o UUID isoladamente como identidade global entre clientes diferentes.

A variável `$IDEMPOTENCY_KEY` também pertence ao shell. Consulte o valor com `echo "$IDEMPOTENCY_KEY"` antes de entrar no `psql` e substitua `<IDEMPOTENCY_KEY>` pelo UUID obtido:

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

Depois localize a mensagem pelo lançamento, novamente substituindo `<TRANSACTION_ID>` pelo UUID da sua execução:

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

Use `\d <nome-da-tabela>` nas tabelas de projeção/deduplicação identificadas e consulte registros referentes a `2026-10-25` e/ou ao `TRANSACTION_ID`. O objetivo é comprovar que o resultado retornado pela Consolidation API corresponde à projeção persistida.

Saia com `\q`.

---

## 5. Provar idempotência da escrita

### 5.1 Replay do mesmo comando

Repita **exatamente** o POST original, preservando `$IDEMPOTENCY_KEY`, corpo, identidade e demais dados:

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
- mesmo `id` do primeiro POST.

Confirme no PostgreSQL de Transactions que não surgiu segundo fato para essa identidade e que o registro idempotente continua apontando para o mesmo `transaction_id`.

### 5.2 Mesma chave com comando diferente

Agora preserve a mesma chave, mas altere o comando:

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
    "description":"Teste manual - credito"
  }'
```

Resultado esperado: `HTTP/1.1 409 Conflict`. A mesma identidade idempotente não pode ser reutilizada para representar um comando semanticamente diferente.

---

## 6. Validar independência de Transactions em relação a Consolidation

Antes deste teste, gere uma nova chave para não misturar o ensaio com a transação anterior:

```sh
RESILIENCE_KEY=$(uuidgen | tr '[:upper:]' '[:lower:]')
```

Pare o lado de Consolidation:

```sh
docker compose stop consolidation-api consolidation-worker postgres-consolidation
```

Confirme que Transactions continua saudável:

```sh
curl --fail http://127.0.0.1:8081/health/live
curl --fail http://127.0.0.1:8081/health/ready
```

Registre um novo lançamento em Transactions:

```sh
curl --include \
  http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: $RESILIENCE_KEY" \
  --data '{
    "type":"CREDIT",
    "amount":"10.00",
    "occurredAt":"2026-10-25T14:00:00-03:00",
    "description":"Transactions independente de Consolidation"
  }'
```

Resultado esperado: `201 Created`, mesmo com o contexto de Consolidation indisponível.

Restaure os serviços:

```sh
docker compose start postgres-consolidation consolidation-worker consolidation-api
```

Aguarde readiness:

```sh
until curl --fail --silent http://127.0.0.1:8082/health/ready >/dev/null; do sleep 2; done
```

Consulte novamente a projeção e confirme convergência posterior.

Esse ensaio comprova independência do write path de Transactions em relação à disponibilidade síncrona de Consolidation.

---

## 7. Validar indisponibilidade temporária do RabbitMQ

Este teste verifica o motivo arquitetural da Outbox: o commit financeiro não depende de o broker estar disponível naquele instante.

Pare RabbitMQ:

```sh
docker compose stop rabbitmq
```

Gere uma nova chave:

```sh
BROKER_DOWN_KEY=$(uuidgen | tr '[:upper:]' '[:lower:]')
```

Registre um lançamento:

```sh
curl --include \
  http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: $BROKER_DOWN_KEY" \
  --data '{
    "type":"CREDIT",
    "amount":"7.00",
    "occurredAt":"2026-10-25T15:00:00-03:00",
    "description":"Broker temporariamente indisponivel"
  }'
```

Resultado esperado: `201 Created`.

Entre no banco de Transactions e localize o lançamento, seu registro de idempotência e a Outbox. O fato deve estar comprometido localmente mesmo sem publicação imediata.

Restaure RabbitMQ:

```sh
docker compose start rabbitmq
```

Aguarde o broker ficar saudável:

```sh
until docker compose exec rabbitmq rabbitmq-diagnostics -q ping; do sleep 2; done
```

Acompanhe filas e logs dos workers:

```sh
docker compose logs --tail=100 transactions-worker consolidation-worker
```

Depois consulte Consolidation e confirme convergência posterior. Esse comportamento demonstra desacoplamento temporal e entrega assíncrona; ele não transforma RabbitMQ em componente opcional, pois o evento ainda precisa ser publicado para a projeção convergir.

---

## 8. Retry e DLQ

O ensaio operacional disponível para falha permanente é:

```sh
python3 tests/operational/t09_retry_dlq.py
```

Esse cenário comprova somente mensagem permanentemente inválida chegando à DLQ sem alterar a projeção. Ele **não** comprova um redrive operacional completo.

Para inspeção manual do broker antes/depois:

```sh
docker compose exec rabbitmq rabbitmqctl list_queues name messages_ready messages_unacknowledged consumers
```

Registre como evidência o nome da DLQ e a alteração em `messages_ready` observada durante o ensaio.

---

## 9. Recovery / rebuild

O ensaio reproduzível disponível é:

```sh
python3 tests/operational/t10_rebuild.py
```

Ele exercita o rebuild administrativo local com nova geração e lease/fencing. `tools/CashFlow.Recovery` lê a fonte Transactions em snapshot somente leitura, constrói uma nova geração e a ativa sob lease PostgreSQL renovável com fencing.

Após o ensaio, consulte novamente a Consolidation API e compare os totais com a fonte de Transactions. Se o processo Recovery morrer, a lease expira, o Worker retoma e o operador deve iniciar um novo rebuild; não existe retomada automática do processo administrativo.

---

## 10. Observabilidade

### 10.1 Logs

```sh
docker compose logs --tail=100 transactions-api transactions-worker consolidation-api consolidation-worker
```

Use os `X-Correlation-Id` observados nas chamadas HTTP para procurar correlação nos logs quando aplicável.

### 10.2 Métricas

Os quatro processos expõem `/metrics`. Exemplos:

```sh
curl --fail http://127.0.0.1:8081/metrics | head
curl --fail http://127.0.0.1:8082/metrics | head
```

Não há servidor Prometheus ou Grafana no Compose entregue. A exposição direta de `/metrics` é uma conveniência local; em produção deve permanecer em superfície administrativa/privada.

### 10.3 Traces

Abra a UI do Jaeger em `127.0.0.1:16686` e procure traces dos processos após executar POSTs e consultas. Os quatro processos emitem traces OpenTelemetry via OTLP para o Jaeger local.

O Compose aguarda Jaeger saudável no bootstrap para tornar a demonstração determinística; isso não significa que observabilidade seja dependência de disponibilidade da arquitetura-alvo de produção.

---

## 11. Performance

O ensaio disponível é:

```sh
python3 tests/performance/t15_run.py
```

O cenário usa warm-up separado de 10 s e janela medida de 120 s a 50 req/s, total esperado de 6.000 consultas.

Registre throughput, erros e latências reportados pelo script. O resultado representa o ambiente local da execução, não um SLA de produção.

---

## 12. Regressão automatizada final

Depois dos ensaios manuais e operacionais, restaure todos os serviços e execute o gate completo:

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

O golden path registrado em 23/10/2026 executou **90 passed, 0 failed, 0 skipped**, com build **0 warnings / 0 errors**. Uma nova avaliação deve considerar a execução corrente como evidência principal; essa contagem histórica documenta apenas o último gate registrado.

---

## 13. Ensaios operacionais disponíveis

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

---

## 14. O que cada etapa demonstra

| Etapa | Evidência principal |
|---|---|
| Health | processos locais disponíveis e ready |
| JWT válido | autenticação aceita pela API |
| Sem token | `401 Unauthorized` |
| Reader escrevendo | `403 Forbidden` |
| POST Transactions | `201 Created` e fato persistido |
| Idempotência | mesma identidade/comando → replay; comando divergente → `409` |
| Outbox | evento persistido junto ao fato financeiro |
| RabbitMQ | transporte assíncrono entre contextos |
| Consolidation | projeção eventual do fato financeiro |
| Consolidation indisponível | Transactions continua aceitando writes |
| Broker indisponível | commit local preservado e publicação posterior |
| DLQ | isolamento de falha permanente no cenário implementado |
| Recovery | reconstrução de projeção com geração e fencing |
| Observabilidade | logs, métricas e traces locais |
| Performance | comportamento do ambiente sob carga controlada |
| Regressão | build e testes automatizados do conjunto |

---

## 15. Limites do ambiente local

O Compose demonstra comportamento funcional, independência dos contextos, segurança local, mensageria RabbitMQ, recovery e observabilidade. Ele **não demonstra** alta disponibilidade de RabbitMQ em cluster, SQS, Cognito, IAM, KMS, WAF, ECS/Fargate, RDS Multi-AZ, Terraform, deployment AWS, RTO/RPO produtivos ou SLA.

RabbitMQ é o único broker implementado nesta entrega. SQS Standard permanece arquitetura-alvo AWS e não possui adaptador local implementado.

A arquitetura-alvo está em [arquitetura-aws.md](arquitetura-aws.md), e as diferenças de transporte estão nos [ADR-007](../06-adr/adr-007-estrategia-de-mensageria.md) e [ADR-010](../06-adr/adr-010-mensageria-local-rabbitmq-e-aws-sqs.md).
