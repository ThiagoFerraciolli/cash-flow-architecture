# Execução local

## Fase 2: Transactions API e PostgreSQL

Requer Docker Engine com Compose v2. Na raiz do repositório:

```sh
docker compose up -d --build
docker compose ps
curl --fail http://127.0.0.1:8081/health/live
curl --fail http://127.0.0.1:8081/health/ready
```

O Compose atual possui somente `postgres` e `transactions-api`. A API está em `http://127.0.0.1:8081`. PostgreSQL é exposto apenas no loopback do host, em `127.0.0.1:5433`, para testes locais. A API usa `postgres:5432` na rede Docker. `Transactions__TimeZone` mantém `America/Sao_Paulo` como default.

O banco e usuário locais são `cashflow`. A senha padrão `cashflow_local_only` é exclusiva de desenvolvimento e pode ser substituída pela variável `POSTGRES_PASSWORD` no ambiente ou em um `.env` local ignorado pelo Git. Não use essa configuração em produção.

O diretório `./runtime-data/postgres` guarda os dados no host e está ignorado pelo Git. Em um banco vazio, o entrypoint oficial do PostgreSQL aplica `001_financial_transactions.sql`; o healthcheck exige que a tabela exista, e a API espera o banco ficar saudável antes de iniciar. Essa é somente a inicialização local. Em produção, migrations devem rodar em job separado, conforme ADR-003.

Para criar um lançamento nesta fase:

```sh
curl --fail-with-body --include http://127.0.0.1:8081/transactions \
  -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: 607c30f2-1230-4149-8423-074104df242d' \
  --data '{"type":"CREDIT","amount":"100.00","occurredAt":"2026-10-21T12:00:00-03:00","description":"Venda"}'

curl --fail-with-body http://127.0.0.1:8081/transactions/SEU_ID
```

Use uma nova chave UUID por POST. O header preserva a forma do contrato, mas replay idempotente e persistência atômica com Outbox pertencem à Fase 3. A autenticação também não está implementada nesta fase.

Para rodar o teste de integração com o PostgreSQL local:

```sh
CASHFLOW_TEST_POSTGRES='Host=127.0.0.1;Port=5433;Database=cashflow;Username=cashflow;Password=cashflow_local_only' \
  dotnet test tests/CashFlow.Transactions.IntegrationTests --no-build
```

Adapte a senha do comando se definir `POSTGRES_PASSWORD`. O teste cria e remove um schema isolado no banco local.

`docker compose down` remove os containers e a rede, mas preserva os dados em `./runtime-data/postgres`; `docker compose up -d` recria os containers usando os mesmos dados. Para apagar os dados intencionalmente, execute `docker compose down` e depois remova `./runtime-data/postgres`. Essa remoção é destrutiva.

## Evolução prevista

As próximas fases acrescentarão RabbitMQ, Transactions Worker, Consolidation API e Consolidation Worker como processos e containers separados. O ambiente final previsto tem seis containers: PostgreSQL, RabbitMQ e os quatro processos de aplicação. Nenhum Worker executa no container da API. As bases dos dois contextos permanecem isoladas logicamente no PostgreSQL. O Compose desta fase não inicia serviços ainda não implementados.
