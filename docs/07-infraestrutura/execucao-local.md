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

### Passo 1 — Identificar e copiar o ID da transação

No JSON retornado pela API, localize o campo `id`:

```text
{"id":"92588f23-7a57-47ce-960a-48c5d5566d11", ...}
```

O mesmo UUID aparece no header `Location`:

```text
Location: /transactions/92588f23-7a57-47ce-960a-48c5d5566d11
```

Copie somente o UUID, sem `/transactions/`. Esse valor identifica o lançamento que acabou de ser criado e será utilizado nas próximas consultas ao banco.

### Passo 2 — Salvar o ID em uma variável

**Continue no terminal normal. Ainda não entre no PostgreSQL.**

Atribua o UUID copiado à variável `TRANSACTION_ID`. O comando abaixo não cria outra transação; apenas guarda uma referência ao ID já retornado pela API:

```sh
TRANSACTION_ID='92588f23-7a57-47ce-960a-48c5d5566d11'
```

Substitua o UUID do exemplo pelo UUID retornado pela sua própria chamada.

### Passo 3 — Confirmar os dois UUIDs antes de entrar no PostgreSQL

Ainda no terminal normal, execute:

```sh
echo "$TRANSACTION_ID"
echo "$IDEMPOTENCY_KEY"
```

Você deve ver dois UUIDs. Por exemplo:

```text
92588f23-7a57-47ce-960a-48c5d5566d11
d97b5dde-fc2c-413c-aed7-77666b02d195
```

Eles têm funções diferentes:

- `TRANSACTION_ID` identifica o **lançamento financeiro criado**;
- `IDEMPOTENCY_KEY` identifica a **requisição idempotente** que criou esse lançamento.

Anote ou mantenha visíveis esses dois valores. Eles serão usados nas consultas das seções 3.2, 3.3 e 3.4.

Somente depois dessa conferência avance para a próxima etapa e entre no PostgreSQL.

### 3.1 Entrar no PostgreSQL de Transactions

```sh
docker compose exec postgres psql -U cashflow -d cashflow
```

O prompt deve mudar para:

```text
cashflow=#
```

**A partir deste ponto, permaneça dentro do `psql` até a seção 3.4.** Execute somente os comandos SQL e os metacomandos `psql` mostrados abaixo. Não execute `echo`, `docker compose`, `curl` ou outros comandos do shell enquanto o prompt estiver `cashflow=#`.

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

Ainda dentro do `psql`, veja primeiro o schema:

```sql
\d financial_transactions
```

Na consulta abaixo, substitua `<TRANSACTION_ID>` pelo UUID que você confirmou **antes de entrar no `psql`**. Não digite literalmente `$TRANSACTION_ID`, pois essa é uma variável do shell e não é expandida pelo `psql`.

Exemplo: se `TRANSACTION_ID` era `92588f23-7a57-47ce-960a-48c5d5566d11`, execute:

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

Resultado esperado: exatamente uma linha com `CREDIT`, `100.00`, `business_date=2026-10-25` e `actor_client_id=cashflow-demo`.

Observe também `creation_event_id`: ele identifica o evento de criação associado ao fato e será usado para relacionar o lançamento à Outbox.

### 3.3 Inspecionar a identidade idempotente

Continue no mesmo prompt `cashflow=#` e veja a estrutura:

```sql
\d idempotency_records
```

A chave primária é composta por `issuer`, `client_id` e `idempotency_key`. Isso evita tratar o UUID isoladamente como identidade global entre clientes diferentes.

Agora substitua `<IDEMPOTENCY_KEY>` pelo valor que você confirmou **antes de entrar no `psql`**. Por exemplo:

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
WHERE idempotency_key = 'd97b5dde-fc2c-413c-aed7-77666b02d195';
```

O UUID acima é apenas exemplo; use a chave gerada na sua execução.

Confirme:

- `client_id = cashflow-demo`;
- `transaction_id` igual ao lançamento retornado pela API;
- `response_status = 201`;
- `request_hash` preenchido;
- `response_body` contendo a resposta persistida para replay.

### 3.4 Inspecionar a Outbox

Ainda no mesmo `psql`, veja primeiro o schema real:

```sql
\d outbox_messages
```

Depois localize a mensagem usando o mesmo UUID de `TRANSACTION_ID` utilizado na seção 3.2. Exemplo:

```sql
SELECT *
FROM outbox_messages
WHERE aggregate_id = '92588f23-7a57-47ce-960a-48c5d5566d11';
```

Use o UUID da sua execução no lugar do UUID do exemplo.

Confirme que existe uma mensagem ligada ao mesmo `aggregate_id`. Compare também o identificador do evento com o `creation_event_id` de `financial_transactions`.

A existência do fato financeiro, do registro idempotente e da Outbox relacionados demonstra as três persistências que compõem o write path de Transactions. O desenho implementado grava essas estruturas na mesma transação PostgreSQL.

Somente agora saia do `psql`:

```sql
\q
```

Depois de `\q`, o prompt volta ao terminal normal. A partir desse ponto, comandos como `docker compose`, `curl` e `echo` voltam a ser executados no shell.

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

Use `\d <nome-da-tabela>` nas tabelas de projeção/deduplicação identificadas e consulte registros referentes a `2026-10-25` e/ou ao `TRANSACTION_ID`. O objetivo é comprovar que o resultado retornado pela Consolidation API corresponde à projeção persistida e que o evento consumido é tratado de forma idempotente.

Saia com:

```sql
\q
```

---

## 5. Validar idempotência

Use exatamente a mesma `IDEMPOTENCY_KEY` e exatamente o mesmo payload da seção 3:

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

- `HTTP/1.1 201 Created`;
- header `Idempotent-Replayed: true`;
- mesmo `id` retornado anteriormente;
- nenhum segundo efeito financeiro.

Consulte novamente a consolidação e/ou `financial_transactions` para confirmar que o replay não duplicou o lançamento.

Agora mantenha a mesma chave e altere o comando, por exemplo o valor:

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

Resultado esperado: `HTTP/1.1 409 Conflict`. A mesma identidade idempotente não pode representar dois comandos semanticamente diferentes.

---

## 6. Validar independência de Transactions

Pare o contexto de Consolidation:

```sh
docker compose stop consolidation-api consolidation-worker postgres-consolidation
```

Transactions deve continuar saudável:

```sh
curl --fail http://127.0.0.1:8081/health/live
curl --fail http://127.0.0.1:8081/health/ready
```

Crie uma nova chave:

```sh
OUTAGE_KEY=$(uuidgen | tr '[:upper:]' '[:lower:]')
```

Registre um débito enquanto Consolidation está indisponível:

```sh
curl --include \
  http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: $OUTAGE_KEY" \
  --data '{
    "type":"DEBIT",
    "amount":"25.00",
    "occurredAt":"2026-10-25T13:00:00-03:00",
    "description":"Teste com Consolidation indisponivel"
  }'
```

Resultado esperado: `201 Created`. O write path de Transactions não depende de uma chamada síncrona a Consolidation.

Restaure o contexto:

```sh
docker compose start postgres-consolidation consolidation-worker consolidation-api
```

Aguarde os serviços ficarem saudáveis e consulte novamente `2026-10-25`. O débito deve convergir para a projeção após o processamento assíncrono.

---

## 7. Validar indisponibilidade temporária do RabbitMQ

Pare o broker:

```sh
docker compose stop rabbitmq
```

Crie uma chave nova:

```sh
BROKER_OUTAGE_KEY=$(uuidgen | tr '[:upper:]' '[:lower:]')
```

Registre outro lançamento:

```sh
curl --include \
  http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: $BROKER_OUTAGE_KEY" \
  --data '{
    "type":"CREDIT",
    "amount":"40.00",
    "occurredAt":"2026-10-25T14:00:00-03:00",
    "description":"Teste com broker indisponivel"
  }'
```

Resultado esperado: `201 Created`. A indisponibilidade do broker não deve impedir o commit do lançamento, pois a mensagem permanece na Outbox.

Enquanto RabbitMQ estiver parado, você pode entrar novamente no PostgreSQL de Transactions e confirmar que o fato e a Outbox foram persistidos. Depois saia do `psql` e restaure o broker:

```sh
docker compose start rabbitmq
```

Acompanhe o publisher:

```sh
docker compose logs --tail=200 transactions-worker
```

Depois da recuperação do broker, o worker deve conseguir publicar a pendência e a projeção deve convergir.

---

## 8. Retry e DLQ

O cenário operacional automatizado de mensagem inválida está em:

```sh
python3 tests/operational/t09_retry_dlq.py
```

O teste injeta mensagem inválida e comprova o encaminhamento para a DLQ. Depois, inspecione filas e logs:

```sh
docker compose exec rabbitmq rabbitmqctl list_queues name messages_ready messages_unacknowledged consumers
docker compose logs --tail=200 consolidation-worker
```

A evidência aqui é **mensagem inválida → DLQ**. Não interprete esse cenário como comprovação de um fluxo completo de redrive/reprocessamento da DLQ se esse fluxo não estiver implementado no executável local.

---

## 9. Recovery e rebuild

O utilitário `tools/CashFlow.Recovery` existe para cenários operacionais controlados de recuperação/rebuild da projeção. A validação automatizada correspondente pode ser executada com:

```sh
python3 tests/operational/t10_rebuild.py
```

Esse cenário deve ser tratado como operação explícita, não como comportamento normal de leitura.

---

## 10. Observabilidade

### 10.1 Logs

```sh
docker compose logs --tail=200 transactions-api
docker compose logs --tail=200 transactions-worker
docker compose logs --tail=200 consolidation-api
docker compose logs --tail=200 consolidation-worker
```

Use `X-Correlation-Id` das respostas HTTP para relacionar requisições aos logs quando aplicável.

### 10.2 Métricas

```sh
curl --fail http://127.0.0.1:8081/metrics
curl --fail http://127.0.0.1:8082/metrics
```

### 10.3 Traces

Abra `127.0.0.1:16686` e use a interface do Jaeger para inspecionar traces dos processos instrumentados.

---

## 11. Performance

Com a solução saudável, execute:

```sh
python3 tests/performance/t15_run.py
```

O cenário faz 10 segundos de warm-up e depois 120 segundos a 50 requisições por segundo, totalizando 6.000 consultas medidas. Use o relatório gerado pelo teste como evidência do resultado da execução atual.

---

## 12. Regressão automatizada completa

Depois da inspeção manual, rode novamente o golden path:

```sh
sh tools/validate-local.sh
```

O script executa restore/build/test e a suíte operacional. Uma execução histórica em 23/10/2026 registrou 90 testes .NET aprovados, sem falhas, skips, warnings ou errors; para uma nova avaliação, considere sempre o resultado produzido pela execução atual como fonte de verdade.

---

## 13. O que cada etapa comprova

| Evidência | Propriedade arquitetural |
|---|---|
| 200 com token válido | autenticação/autorização funcional |
| 401 sem token | autenticação obrigatória |
| 403 com `cashflow-reader` em escrita | autorização por escopo |
| fato + idempotência + Outbox | write path transacional |
| actor issuer/client persistidos | identidade de máquina auditável |
| mesma chave + mesmo comando | replay idempotente |
| mesma chave + comando diferente | conflito idempotente |
| Transactions grava com Consolidation parado | desacoplamento entre contextos |
| Transactions grava com RabbitMQ parado | Outbox desacopla commit do broker |
| convergência após recuperação | consistência eventual |
| mensagem inválida → DLQ | isolamento de poison message |
| rebuild controlado | capacidade operacional de recuperação |
| correlation ID, logs, métricas e traces | observabilidade |
| teste de carga | evidência quantitativa de leitura local |

---

## 14. Limites do ambiente local

O ambiente local comprova o comportamento funcional e operacional implementado, mas não simula integralmente a infraestrutura de produção. Em particular, ele não reproduz RabbitMQ em cluster HA, SQS gerenciado, Cognito, IAM, KMS, WAF, ECS/Fargate, RDS Multi-AZ, Terraform, deploy AWS nem objetivos produtivos de RTO/RPO/SLA.

RabbitMQ é o broker implementado no executável local; SQS pertence à arquitetura alvo de produção. Para essa evolução, consulte a visão AWS e os ADRs de mensageria/infraestrutura correspondentes.