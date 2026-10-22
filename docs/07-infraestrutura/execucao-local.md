# Execução local — especificação do ambiente

**Estado: DOCUMENTAÇÃO APROVADA; AMBIENTE A IMPLEMENTAR.** Os arquivos e comandos abaixo são o contrato da próxima etapa. Não existem Compose, imagens da aplicação, migrations ou testes executáveis nesta branch. Este guia define como a entrega executável deverá funcionar.

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

Após o bootstrap futuro, carregar em variáveis somente os valores locais de cliente/segredo; não versionar tokens nem executar com shell em modo de rastreamento. `.env.example` documentará `DEMO_CLIENT_ID=cashflow-demo`, `DEMO_CLIENT_SECRET`, `READER_CLIENT_SECRET`, `LOAD_CLIENT_SECRET`, senhas PostgreSQL/RabbitMQ e bootstrap Keycloak. A preparação gera os segredos localmente; o bootstrap idempotente do serviço identity aguarda o Keycloak, cria realm/clientes/scopes/audience e mapper explícito `client_id` no access token (derivado do cliente autenticado, não enviado livremente pelo usuário), e só fica saudável após concluir. Usa esses valores e falha se algum obrigatório estiver vazio. Não aceitar credenciais padrão fixas como fallback. Gerar um valor diferente por segredo com `openssl rand -hex 32`; copiar para .env, restringir sua leitura ao usuário e exportar DEMO_CLIENT_SECRET apenas no terminal usado na demonstração.

Os comandos abaixo pressupõem `DEMO_CLIENT_SECRET` definido no terminal pelo operador; não são executáveis nesta branch sem a aplicação futura. O token dura 15 min; renovar durante ensaios longos.

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

Criar crédito 100,00 e débito 20,00 no mesmo dia; consultar saldo projetado 80,00 e contagem 2 após convergência. Repetir POST com a mesma chave: nenhum novo lançamento. Parar toda Consolidação, registrar novos fatos e comprovar persistência; reiniciar e verificar somas exatas. Parar RabbitMQ, registrar e verificar Outbox pendente; restaurar broker e confirmar publicação. Exercitar DLQ, redrive e reconstrução via ferramenta de recuperação.

## Diagnóstico e limites

Se API não ficar pronta, verificar migration, banco e configuração de identidade. Falha do broker afeta o Worker, não readiness de Lançamentos. Se houver Outbox suspensa, seguir procedimento de recuperação. Portas ocupadas podem ser remapeadas por configuração, atualizando issuer externo do Keycloak e valor esperado pelas APIs, sem alterar a URL interna confiável de JWKS. O teste de bootstrap valida token obtido no host e aceito pelas APIs nos containers.

`docker compose stop` preserva volumes. Excluir volumes destrói dados locais; limpeza destrutiva não faz parte da sequência normal. O aceite local exige execução limpa por outro desenvolvedor, sem credenciais AWS e sem ajustes manuais no código.

Referência de configuração de emissor: [hostname e backchannel do Keycloak](https://www.keycloak.org/server/hostname).
