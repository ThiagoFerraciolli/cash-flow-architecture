# Execução local — especificação do ambiente

## Estado executável atual

O `compose.yaml` executa nove serviços permanentes: quatro processos de aplicação (`transactions-api`, `transactions-worker`, `consolidation-api`, `consolidation-worker`) e cinco de suporte (`postgres`, `postgres-consolidation`, `rabbitmq`, `identity`/Keycloak e `jaeger`). Os jobs transitórios `migrate` e `migrate-consolidation` encerram após aplicar as migrations de cada contexto. APIs e Workers são processos e containers independentes. Requer Docker Engine com Compose v2. Antes de subir, configure os segredos locais obrigatórios em `.env`, ignorado pelo Git. Na raiz do repositório:

```sh
docker compose up -d --build
docker compose ps
curl --fail http://127.0.0.1:8081/health/live
curl --fail http://127.0.0.1:8081/health/ready
curl --fail http://127.0.0.1:8082/health/live
curl --fail http://127.0.0.1:8082/health/ready
```

A Transactions API está em `127.0.0.1:8081`, a Consolidation API em `127.0.0.1:8082`, o PostgreSQL de Transactions em `127.0.0.1:5433`, o PostgreSQL de Consolidação em `127.0.0.1:5434`, o AMQP RabbitMQ em `127.0.0.1:5672`, a gestão RabbitMQ em `127.0.0.1:15672`, Keycloak em `127.0.0.1:8080` e Jaeger em `127.0.0.1:16686`. Dentro do Compose, Transactions API e Worker conectam-se a `postgres:5432`; Consolidation API e Worker a `postgres-consolidation:5432`; ambos os Workers a `rabbitmq:5672`. A Consolidation API não depende de RabbitMQ ou Transactions para leitura/readiness. O fuso de negócio padrão é `America/Sao_Paulo`. O banco e usuário de Transactions são `cashflow`; os de Consolidação são `consolidation`. As senhas locais dos bancos e do broker vêm de `POSTGRES_PASSWORD`, `CONSOLIDATION_POSTGRES_PASSWORD` e `RABBITMQ_PASSWORD` no ambiente ou `.env`; os valores padrão de desenvolvimento, se usados, não são credenciais de produção.

Os dados de Transactions persistem em `./runtime-data/postgres`; os de Consolidação, em `./runtime-data/consolidation-postgres`; os do broker, em `./runtime-data/rabbitmq`; e os do Keycloak, em `./runtime-data/identity`, todos ignorados pelo Git. O job `migrate` aplica `001_financial_transactions.sql` e `002_idempotency_outbox.sql` em ordem. `migrate-consolidation` aplica `001_consolidation.sql` e `002_recovery_lease.sql` no banco próprio; a migration inicial cria uma geração `ACTIVE` vazia e a segunda acrescenta a lease de manutenção. Cada processo aguarda somente a migration do seu contexto. Os jobs locais não definem a topologia de migrations de produção.

O serviço `identity` importa `identity/cashflow-realm.json`, com realm `cashflow`, audience `cashflow-api` e clientes client credentials `cashflow-demo` e `cashflow-reader`. O primeiro possui permissões de escrita/leitura de Transactions e leitura de Consolidation; o segundo, leitura de Consolidation. O POST exige JWT assinado e validado, com issuer, audience, identidade de cliente permitida e permissão lógica `transactions.write`. O Compose mantém `Authentication__Authority` em `http://identity:8080/realms/cashflow` e `Authentication__Issuer` em `http://localhost:8080/realms/cashflow` por padrão; `CASHFLOW_AUTHORITY`, `CASHFLOW_ISSUER` e `CASHFLOW_ALLOWED_CLIENT_IDS` permitem configurar os valores. O token deve ser emitido pelo Keycloak local; não há bypass de autenticação nem identidade provisória no código de produção. HTTP e AMQP sem TLS são exceções exclusivas do desenvolvimento local.

Após `set -a; source .env`, obtenha um token local sem imprimi-lo e use-o no exemplo:

```sh
TOKEN=$(curl --fail --silent --show-error \
  http://127.0.0.1:8080/realms/cashflow/protocol/openid-connect/token \
  --data-urlencode 'grant_type=client_credentials' \
  --data-urlencode 'client_id=cashflow-demo' \
  --data-urlencode "client_secret=$DEMO_CLIENT_SECRET" | jq -er '.access_token')
```

```sh
curl --fail-with-body --include http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Idempotency-Key: 607c30f2-1230-4149-8423-074104df242d' \
  --data '{"type":"CREDIT","amount":"100.00","occurredAt":"2026-10-21T12:00:00-03:00","description":"Venda"}'

curl --fail-with-body http://127.0.0.1:8081/transactions/SEU_ID
```

A Consolidation API valida JWT com `iss`, `aud=cashflow-api`, `client_id` permitido e permissão lógica `consolidations.read`, usando as mesmas variáveis de emissor e clientes permitidos. Com um `TOKEN` válido, consulte `GET /consolidations/2026-10-21` ou `GET /consolidations?from=2026-10-01&to=2026-10-31&pageSize=50`. A listagem inclui cada dia do intervalo, inclusive zeros sem linha persistida, e retorna `nextCursor` opaco. Se a geração ativa mudar entre páginas, a API retorna 409 `projection-generation-changed`; recomece a paginação. Durante uma **lease de manutenção válida**, a geração ativa continua legível com `projectionState=REBUILDING`; lease expirada não mantém esse estado. As respostas indicam `consistency=EVENTUAL` e `completeness=UNKNOWN`; zero não confirma ausência de lançamentos. A readiness depende exclusivamente do PostgreSQL de Consolidação e do esquema necessário. Sem token válido, a API não executa consultas funcionais.

Repetir o mesmo POST com a mesma chave e identidade retorna `201` com `Idempotent-Replayed: true` e o mesmo ID. Reutilizar a chave com outro comando retorna `409`. Cada primeira criação confirmada grava, em uma transação PostgreSQL, o lançamento, o registro de idempotência e uma mensagem Outbox em estado `PENDING`. Transactions Worker reserva essa mensagem no PostgreSQL, publica o `TransactionCreatedV1` persistente com `mandatory` e publisher confirm no exchange direto durável `cashflow.events`, routing key `transaction.created.v1`, e só então marca `PUBLISHED`. Consolidation Worker consome a fila quorum `consolidation.transactions.v1` com ACK manual, prefetch 16 e até 8 processamentos simultâneos. Ele valida o contrato, trava `projection_control FOR SHARE`, grava `processed_transactions` e atualiza `daily_consolidations` por UPSERT relativo na mesma transação do seu banco; o ACK ocorre após o commit. Duplicatas do mesmo fato financeiro são no-op, inclusive com outro `eventId`. Conflitos de identidade e contratos inválidos vão para a DLQ somente após confirmação da publicação nela. A entrega é **at-least-once**: publicação repetida e redelivery podem ocorrer, mas a identidade `(generation_id, transaction_id)` impede segundo efeito financeiro na geração. Lançamentos criados antes da Fase 3 permanecem no banco sem uma identidade histórica fabricada.

O Worker consulta até 20 itens por segundo, publica até 4 em paralelo e usa lease de 60 s com renovação a cada 20 s. Falhas sistêmicas do broker abrem circuito com backoff e deixam itens recuperáveis sem consumir as 20 tentativas de falha isolada. Para demonstrar independência da API, pare somente `rabbitmq`, crie um lançamento com JWT válido e observe POST `201` e Outbox `PENDING`; reinicie o broker e aguarde `PUBLISHED`. `/health/ready` da API depende do PostgreSQL, não do broker. O Worker expõe liveness e readiness privadas no container; sua readiness reflete banco e publicador.

Consolidation Worker declara `cashflow.retry` e as filas quorum `consolidation.retry.5s`, `.15s`, `.30s` e `.60s`. Falhas isoladas usam `retryAttempt` para até cinco retentativas após o processamento original, com TTL técnico de 5, 15, 30 e 60 segundos e retorno à rota principal por DLX. Contrato inválido, conflito financeiro e falha isolada esgotada seguem por `cashflow.errors`/`transaction.created.v1.failed` para a fila quorum `consolidation.transactions.v1.dlq`. Publicações para retry/DLQ são persistentes, mandatory e confirmadas antes do ACK da original. A política RabbitMQ da fila principal define delivery-limit 100 e DLX seguro; a interface de gestão local é usada pelo Worker para aplicar essa política à fila já persistida pela Fase 4. Indisponibilidade compartilhada do banco da Consolidação pausa o consumidor e deixa mensagens recuperáveis na fila principal, sem aumentar `retryAttempt` nem impedir POST na Transactions API. O Worker retoma após readiness do seu banco. `/health/live` e `/health/ready` são privados ao container do Worker.

Para executar os testes de integração contra PostgreSQL e RabbitMQ reais:

```sh
CASHFLOW_TEST_POSTGRES='Host=127.0.0.1;Port=5433;Database=cashflow;Username=cashflow;Password=cashflow_local_only' \
  CASHFLOW_TEST_RABBITMQ='amqp://cashflow:cashflow_local_only@127.0.0.1:5672/' \
  dotnet test tests/CashFlow.Transactions.IntegrationTests --no-build

CASHFLOW_TEST_CONSOLIDATION_POSTGRES='Host=127.0.0.1;Port=5434;Database=consolidation;Username=consolidation;Password=consolidation_local_only' \
  CASHFLOW_TEST_RABBITMQ='amqp://cashflow:cashflow_local_only@127.0.0.1:5672/' \
  dotnet test tests/CashFlow.Consolidation.IntegrationTests --no-build
```

Ajuste as senhas no comando se configurar as variáveis de senha. Os testes criam e removem schemas PostgreSQL e vhosts RabbitMQ isolados.

### E2E completo — fluxo autenticado

Com o Docker Compose local saudável e o `.env` local configurado com o segredo de `cashflow-demo`, execute na raiz do repositório:

```sh
set -a; source .env
dotnet test tests/CashFlow.EndToEndTests --no-build \
  --filter FullyQualifiedName~Keycloak_to_transactions_outbox_rabbitmq_and_consolidation_converges
```

O teste valida o caminho real Keycloak → Transactions API → Outbox → RabbitMQ → Consolidation Worker → Consolidation API, com lançamentos autenticados e espera limitada pela convergência da projeção.

### T06 — independência de Transactions durante outage de Consolidation

Com o mesmo Compose saudável, execute na raiz do repositório:

```sh
set -a; source .env
python3 tests/operational/t06_transactions_independent.py
```

O script para `consolidation-api`, `consolidation-worker` e `postgres-consolidation`, verifica que Transactions continua live/ready e aceita lançamentos durante a indisponibilidade, restaura os serviços e aguarda a convergência da projeção.

### T15 — ensaio local bounded de Consolidação

Com o Compose saudável e `DEMO_CLIENT_SECRET` e `READER_CLIENT_SECRET` carregados do `.env` local, execute `python3 tests/performance/t15_run.py`. O runner obtém JWTs do Keycloak, prepara via HTTP uma data de negócio isolada com CREDIT 100.00 e DEBIT 20.00 e espera a projeção convergir antes da medição. Em seguida, executa `tests/performance/t15_consolidation_50rps.js` no container `grafana/k6:1.5.0`, conectado à rede do Compose. O token de leitura é passado ao container por variável de ambiente, sem ser impresso.

O warm-up é uma execução separada de 10 s a 10 requisições/s. A janela medida usa `constant-arrival-rate` por 2 min a 50 requisições/s (6.000 requisições esperadas), com um GET autenticado por iteração e timeout HTTP de 5 s. O script valida os valores e o estado da resposta, contando no máximo uma falha por request mesmo se houver timeout, erro de transporte, status inesperado ou body incorreto. Aprovação local: taxa sustentada ≥ 50 requisições/s, falhas/requisições enviadas ≤ 5% e `dropped_iterations=0`. P95 ≤ 500 ms e P99 ≤ 1 s são metas internas reportadas separadamente. Este ensaio bounded e reproduzível de 2 min é a validação local final; não substitui o ensaio principal de 10 min registrado em `parametros-operacionais.md`.

### Recovery e observabilidade locais

Com `set -a; source .env` no terminal e o Compose saudável, `python3 tests/operational/t09_retry_dlq.py` exercita o cenário limitado de mensagem permanentemente inválida na DLQ; `python3 tests/operational/t10_rebuild.py` exercita o rebuild administrativo. Consulte o script e o [procedimento de recuperação](recuperacao-e-reconciliacao.md) antes de executar T10, pois ele altera a geração ativa de Consolidação. A ferramenta `tools/CashFlow.Recovery` lê a fonte de Transactions em snapshot somente leitura, constrói e verifica uma nova geração de Consolidação e troca a geração ativa sob uma lease PostgreSQL de 60 s, renovada a cada 15 s. Apenas o token dono pode renovar, liberar ou ativar; lease expirada permite retomada do Worker e nova aquisição. Após crash, o operador precisa iniciar outra execução de Recovery. O processo não é reiniciado automaticamente.

Os quatro processos de aplicação emitem traces OpenTelemetry, logs estruturados e métricas de texto Prometheus em `/metrics`. Spans de publicação/consumo e propagação W3C ligam o trace através da Outbox e do RabbitMQ. Jaeger recebe os traces via OTLP e expõe sua UI em `127.0.0.1:16686`; não há servidor Prometheus, Grafana ou OTel Collector no Compose. Um GET posterior da Consolidation API é outra requisição e não integra necessariamente o trace transacional. As [evidências locais](../08-testes/validacao-local-final.md) registram T09, T10, T13, T14, T15 e T18 sem extrapolar para AWS.

`docker compose down` remove containers e rede, mas preserva os quatro diretórios de dados mencionados acima; `docker compose up -d` recria os serviços com os mesmos dados, projeção, identidades processadas e mensagens persistentes. Para reset explicitamente destrutivo, execute `docker compose down` e remova manualmente esses diretórios antes de subir o ambiente novamente.

## Arquitetura-alvo — especificação futura

Há dois serviços/bounded contexts, Transactions e Consolidation, com quatro deployables independentes: Transactions API, Transactions Worker/Outbox Publisher, Consolidation API e Consolidation Worker. API e Worker não são combinados no mesmo container. PostgreSQL e RabbitMQ completam a topologia mínima de seis tipos de workloads principais; o Compose local usa dois PostgreSQL separados, além de Keycloak e Jaeger como suporte. O ownership dos dados permanece independente por contexto. Esta seção preserva a arquitetura-alvo sem alterar a decisão de deployment físico futuro.

As seções seguintes preservam a **especificação futura** e seus exemplos ilustrativos; seus nomes de serviços e comandos não substituem os comandos executáveis acima. O rebuild administrativo por geração, Keycloak local, tracing/métricas locais e ensaios T06/T09/T10/T13/T14/T15/T18 limitados já existem. Reconciliação contínua, redrive completo, servidor Prometheus/Grafana/Collector, SQS e Cognito operacionais, Terraform, deployment AWS e ensaios de RTO/RPO permanecem fora do Compose executável. A arquitetura AWS não foi provisionada nem homologada.

## Pré-requisitos e composição

Docker Engine com Compose v2, Git, curl, jq e OpenSSL para os exemplos, 4 CPUs, 8 GB de RAM disponíveis e 15 GB de disco como referência inicial; SDK .NET 10 apenas para execução fora de containers. Primeira preparação requer internet para imagens/pacotes; execução posterior não depende de AWS. Linux containers, imagens multi-arquitetura quando disponíveis e versões fixadas por digest na implementação; não usar `latest`.

| Serviço Compose previsto | Papel | Exposição no host |
|---|---|---|
| transactions-api | Registro/consulta | 127.0.0.1:8081 |
| transactions-worker | Publicação Outbox | Nenhuma |
| consolidation-api | Consulta da projeção | 127.0.0.1:8082 |
| consolidation-worker | Consumo | Nenhuma |
| transactions-db | PostgreSQL próprio | Nenhuma |
| consolidation-db | PostgreSQL próprio | Nenhuma |
| rabbitmq | RabbitMQ 4.x com management, nó único e volume | Gestão 127.0.0.1:15672; AMQP interno |
| identity | Keycloak de desenvolvimento | 127.0.0.1:8080 |
| migrate | Job de migrations, perfil tools | Nenhuma |
| tests / load / recovery | Ferramentas, perfil tools | Nenhuma |
| otel-collector / prometheus / grafana | Perfil observability | Grafana 127.0.0.1:3000 |

Duas instâncias PostgreSQL locais permitem parar também o banco de Consolidação sem derrubar Lançamentos. Volumes separados preservam dados entre reinícios. RabbitMQ quorum em nó único valida persistência/protocolo, não alta disponibilidade. Keycloak em modo de desenvolvimento é apenas suporte local.

## Configuração e identidade

A implementação entregará `.env.example` sem segredos e `compose.yaml`. Aplicações usam .NET 10, PostgreSQL 17, RabbitMQ 4.x e Keycloak em versão estável suportada fixada na implementação; o manifesto de versões registra tags, digests e compatibilidade comprovada. Cópia local `.env` ignorada pelo Git; gerar senhas e client secrets na preparação. `Messaging__Provider=RabbitMq`, timezone/moeda e demais defaults vêm da tabela de parâmetros. Uma rede Docker interna conecta aplicações, bancos e AMQP; somente portas de avaliação são vinculadas ao loopback.

Criar realm `cashflow`, audience `cashflow-api` e clientes técnicos: `cashflow-demo` com as três permissões; `cashflow-reader` com leitura; `cashflow-load` com leitura do consolidado. Fluxo client credentials para demonstração, sem senha de usuário. Todas as chamadas funcionais exigem token. A configuração normaliza `issuer + client_id` para o escopo de idempotência. Emissor local fixo `http://localhost:8080/realms/cashflow`, anunciado pelo Keycloak; as APIs usam URL interna confiável `http://identity:8080/realms/cashflow/protocol/openid-connect/certs` para buscar JWKS, validando o issuer externo esperado. Não tentar alcançar localhost do host a partir do container. Essa URL de chaves é configuração local fixa, nunca informada pelo token ou usuário. HTTP e AMQP sem TLS são exceção exclusiva do ambiente local isolado; acesso remoto exige TLS e configuração de produção.

## Sequência e comandos previstos

**Executar somente após a implementação dos arquivos indicados.** O job migrate usa connection strings específicas de migration, aguarda os bancos e encerra com código diferente de zero em falha. APIs não executam migrations na inicialização.

```sh
cp .env.example .env
# Gerar cada segredo com openssl rand -hex 32 e preencher .env sem versionar.
docker compose up -d transactions-db consolidation-db rabbitmq identity
docker compose --profile tools run --rm migrate
docker compose up -d transactions-api transactions-worker consolidation-api consolidation-worker
docker compose ps
docker compose --profile tools run --rm tests
docker compose --profile tools run --rm load
docker compose stop consolidation-api consolidation-worker consolidation-db
# Registrar lançamentos: devem continuar retornando 201.
docker compose start consolidation-db consolidation-api consolidation-worker
docker compose logs --tail=100 transactions-worker consolidation-worker
docker compose stop
```

## Exemplo de uso previsto

Após a implementação das capacidades correspondentes, carregar em variáveis somente os valores locais de cliente/segredo; não versionar tokens nem executar com shell em modo de rastreamento. `.env.example` documentará `DEMO_CLIENT_ID=cashflow-demo`, `DEMO_CLIENT_SECRET`, `READER_CLIENT_SECRET`, `LOAD_CLIENT_SECRET`, senhas PostgreSQL/RabbitMQ e bootstrap Keycloak. A preparação gera os segredos localmente; o bootstrap idempotente do serviço identity aguarda o Keycloak, cria realm/clientes/scopes/audience e mapper explícito `client_id` no access token (derivado do cliente autenticado, não enviado livremente pelo usuário), e só fica saudável após concluir. Usa esses valores e falha se algum obrigatório estiver vazio. Não aceitar credenciais padrão fixas como fallback. Gerar um valor diferente por segredo com `openssl rand -hex 32`; copiar para .env, restringir sua leitura ao usuário e exportar DEMO_CLIENT_SECRET apenas no terminal usado na demonstração.

Os comandos abaixo pressupõem `DEMO_CLIENT_SECRET` definido no terminal pelo operador e tornam-se executáveis quando a aplicação e a capacidade de identidade estiverem implementadas. O token dura 15 min; renovar durante ensaios longos.

```sh
TOKEN=$(curl --fail --silent --show-error \
  http://localhost:8080/realms/cashflow/protocol/openid-connect/token \
  --data-urlencode 'grant_type=client_credentials' \
  --data-urlencode 'client_id=cashflow-demo' \
  --data-urlencode "client_secret=$DEMO_CLIENT_SECRET" | jq -er '.access_token')

curl --fail-with-body --include http://localhost:8081/transactions \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: 607c30f2-1230-4149-8423-074104df242d' \
  --data '{"type":"CREDIT","amount":"100.00","occurredAt":"2026-10-21T12:00:00-03:00","description":"Venda de demonstracao"}'

curl --fail-with-body --include http://localhost:8081/transactions \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: 835f19fb-d03b-4831-88b9-9edc451832df' \
  --data '{"type":"DEBIT","amount":"20.00","occurredAt":"2026-10-21T13:00:00-03:00","description":"Despesa de demonstracao"}'

curl --fail --silent --show-error http://localhost:8082/consolidations/2026-10-21 \
  -H "Authorization: Bearer $TOKEN" | jq .
```

Saída esperada: POSTs com 201; consulta com créditos 100.00, débitos 20.00, saldo 80.00 e contagem 2 após convergência, mantendo completeness UNKNOWN. Repetir os mesmos POSTs preserva totais e retorna Idempotent-Replayed. Os exemplos usam data passada fixa aceita pelo escopo; novas demonstrações devem usar outras chaves ou manter os mesmos comandos idênticos.

## Demonstração mínima

Criar crédito 100,00 e débito 20,00 no mesmo dia; consultar saldo projetado 80,00 e contagem 2 após convergência. Repetir POST com a mesma chave: nenhum novo lançamento. Parar toda Consolidação, registrar novos fatos e comprovar persistência; reiniciar e verificar somas exatas. Parar RabbitMQ, registrar e verificar Outbox pendente; restaurar broker e confirmar publicação. Exercitar DLQ, redrive e reconstrução conforme as capacidades classificadas para implementação incremental; mecanismos classificados como evolução não bloqueiam o aceite local inicial.

## Diagnóstico e limites

Se API não ficar pronta, verificar migration, banco e configuração de identidade. Falha do broker afeta o Worker, não readiness de Lançamentos. Se houver Outbox suspensa, seguir procedimento de recuperação. Portas ocupadas podem ser remapeadas por configuração, atualizando issuer externo do Keycloak e valor esperado pelas APIs, sem alterar a URL interna confiável de JWKS. O teste de bootstrap valida token obtido no host e aceito pelas APIs nos containers.

`docker compose stop` preserva volumes. Excluir volumes destrói dados locais; limpeza destrutiva não faz parte da sequência normal. O aceite local exige execução limpa por outro desenvolvedor, sem credenciais AWS e sem ajustes manuais no código.

Referência de configuração de emissor: [hostname e backchannel do Keycloak](https://www.keycloak.org/server/hostname).
