# Execução local — especificação do ambiente

## Estado executável atual — Fase 6

O `compose.yaml` desta branch executa sete workloads permanentes: `postgres` (banco de Transactions), `postgres-consolidation`, `rabbitmq`, `transactions-api`, `transactions-worker`, `consolidation-api` e `consolidation-worker`. Os jobs `migrate` e `migrate-consolidation` encerram após aplicar as migrations de cada contexto. APIs e Workers são processos e containers independentes. Requer Docker Engine com Compose v2. Na raiz do repositório:

```sh
docker compose up -d --build
docker compose ps
curl --fail http://127.0.0.1:8081/health/live
curl --fail http://127.0.0.1:8081/health/ready
curl --fail http://127.0.0.1:8082/health/live
curl --fail http://127.0.0.1:8082/health/ready
```

A Transactions API está em `127.0.0.1:8081`, a Consolidation API em `127.0.0.1:8082`, o PostgreSQL de Transactions em `127.0.0.1:5433`, o PostgreSQL de Consolidação em `127.0.0.1:5434`, o AMQP RabbitMQ em `127.0.0.1:5672` e a interface de gestão RabbitMQ em `127.0.0.1:15672`. Dentro do Compose, Transactions API e Worker conectam-se a `postgres:5432`; Consolidation API e Worker conectam-se a `postgres-consolidation:5432`; ambos os Workers conectam-se a `rabbitmq:5672`. A Consolidation API não depende de RabbitMQ ou Transactions para leitura/readiness. O fuso de negócio padrão é `America/Sao_Paulo`. O banco e usuário de Transactions são `cashflow`; os de Consolidação são `consolidation`. As senhas locais padrão `cashflow_local_only` e `consolidation_local_only` servem apenas ao desenvolvimento e podem ser substituídas por `POSTGRES_PASSWORD` e `CONSOLIDATION_POSTGRES_PASSWORD` no ambiente ou em `.env` local ignorado pelo Git. A credencial RabbitMQ local usa `RABBITMQ_PASSWORD` da mesma forma.

Os dados de Transactions persistem em `./runtime-data/postgres`; os de Consolidação, em `./runtime-data/consolidation-postgres`; e os do broker, em `./runtime-data/rabbitmq`, todos ignorados pelo Git. O job `migrate` aplica `001_financial_transactions.sql` e `002_idempotency_outbox.sql` em ordem; `migrate-consolidation` aplica `001_consolidation.sql` no banco próprio e cria uma geração `ACTIVE` vazia com `projection_control.maintenance=false`. Cada processo aguarda somente a migration do seu contexto. Os jobs locais não definem a topologia de migrations de produção.

O POST exige JWT assinado e validado, com `iss`, `aud=cashflow-api`, `client_id` permitido e permissão lógica `transactions.write`. Configure `CASHFLOW_AUTHORITY` com a URL de discovery/JWKS do emissor confiável, `CASHFLOW_ISSUER` com o issuer esperado e `CASHFLOW_ALLOWED_CLIENT_IDS` com os clientes permitidos antes de subir o Compose. Os defaults apontam para a identidade local prevista na arquitetura-alvo, ainda não fornecida neste Compose. Para demonstração local, use um emissor JWT de teste controlado externamente ao Compose e forneça um token válido em `TOKEN`; não há bypass de autenticação nem identidade provisória no código de produção.

Exemplo executável nesta fase, após configurar o emissor e obter `TOKEN`:

```sh
curl --fail-with-body --include http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Idempotency-Key: 607c30f2-1230-4149-8423-074104df242d' \
  --data '{"type":"CREDIT","amount":"100.00","occurredAt":"2026-10-21T12:00:00-03:00","description":"Venda"}'

curl --fail-with-body http://127.0.0.1:8081/transactions/SEU_ID
```

A Consolidation API valida JWT com `iss`, `aud=cashflow-api`, `client_id` permitido e permissão lógica `consolidations.read`, usando as mesmas variáveis de emissor e clientes permitidos. Com um `TOKEN` válido, consulte `GET /consolidations/2026-10-21` ou `GET /consolidations?from=2026-10-01&to=2026-10-31&pageSize=50`. A listagem inclui cada dia do intervalo, inclusive zeros sem linha persistida, e retorna `nextCursor` opaco. Se a geração ativa mudar entre páginas, a API retorna 409 `projection-generation-changed`; recomece a paginação. Durante `maintenance=true`, a geração ativa continua legível com `projectionState=REBUILDING`. As respostas indicam `consistency=EVENTUAL` e `completeness=UNKNOWN`; zero não confirma ausência de lançamentos. A readiness depende exclusivamente do PostgreSQL de Consolidação e do esquema necessário. Sem token válido, a API não executa consultas funcionais.

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

`docker compose down` remove containers e rede, mas preserva os três diretórios em `./runtime-data`; `docker compose up -d` recria os serviços com os mesmos dados, projeção, identidades processadas e mensagens persistentes. Para reset explicitamente destrutivo, execute `docker compose down` e remova manualmente esses diretórios antes de subir o ambiente novamente.

## Arquitetura-alvo — especificação futura

Há dois serviços/bounded contexts, Transactions e Consolidation, com quatro deployables independentes: Transactions API, Transactions Worker/Outbox Publisher, Consolidation API e Consolidation Worker. API e Worker não são combinados no mesmo container. PostgreSQL e RabbitMQ completam a topologia mínima de seis workloads principais; o Compose local usa dois containers PostgreSQL separados, totalizando sete workloads permanentes nesta fase. A especificação detalhada abaixo prevê também infraestrutura e ferramentas auxiliares, inclusive ownership independente dos dados de cada contexto; esta seção não altera a decisão de deployment físico futuro.

Os comandos e exemplos das seções seguintes dependem de capacidades ainda não implementadas nesta branch. Rebuild/reconciliation completos, SQS operacional, Cognito, Keycloak completo, Terraform, deployment AWS, ensaios de performance/RTO/RPO e observabilidade completa não fazem parte do Compose executável da Fase 6. A arquitetura AWS permanece documentada como alvo, sem homologação nesta fase.

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
