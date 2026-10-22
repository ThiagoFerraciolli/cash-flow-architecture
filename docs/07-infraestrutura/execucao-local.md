# Execução local — especificação do ambiente

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
| identity | Keycloak de desenvolvimento, quando habilitado | 127.0.0.1:8080 |
| migrate | Job de migrations, perfil tools | Nenhuma |
| tests / load / recovery | Ferramentas, perfil tools | Nenhuma |
| otel-collector / prometheus / grafana | Perfil observability | Grafana 127.0.0.1:3000 |

Duas instâncias PostgreSQL locais permitem parar também o banco de Consolidação sem derrubar Lançamentos. Volumes separados preservam dados entre reinícios. RabbitMQ quorum em nó único valida persistência/protocolo, não alta disponibilidade. Keycloak em modo de desenvolvimento é suporte para os cenários locais de autenticação e autorização previstos no escopo incremental.

## Configuração e identidade

A implementação entregará `.env.example` sem segredos e `compose.yaml`. Aplicações usam .NET 10, PostgreSQL 17 e RabbitMQ 4.x; o mecanismo local de identidade segue o ADR-011 e o [escopo de implementação](../01-requisitos/escopo-implementacao.md). Versões de componentes executáveis devem ser fixadas na implementação. `Messaging__Provider=RabbitMq`, timezone/moeda e demais defaults vêm da tabela de parâmetros. Uma rede Docker interna conecta aplicações, bancos e AMQP; somente portas de avaliação são vinculadas ao loopback.

Quando o perfil de identidade com Keycloak for habilitado, criar realm `cashflow`, audience `cashflow-api` e clientes técnicos conforme ADR-011. Fluxo client credentials é usado para demonstração, sem senha de usuário. Todas as chamadas funcionais protegidas exigem token. A configuração normaliza `issuer + client_id` para o escopo de idempotência. HTTP e AMQP sem TLS são exceção exclusiva do ambiente local isolado; acesso remoto exige TLS e configuração de produção.

## Sequência e comandos previstos

Os comandos abaixo se tornam executáveis à medida que os respectivos artefatos forem implementados. O job migrate usa connection strings específicas de migration, aguarda os bancos e encerra com código diferente de zero em falha. APIs não executam migrations na inicialização.

```sh
cp .env.example .env
# Gerar cada segredo com openssl rand -hex 32 e preencher .env sem versionar.
docker compose up -d transactions-db consolidation-db rabbitmq
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

O perfil de identidade acrescenta o serviço `identity` e a obtenção de token descrita no ADR-011 quando essa capacidade estiver implementada.

## Demonstração mínima

Criar crédito 100,00 e débito 20,00 no mesmo dia; consultar saldo projetado 80,00 e contagem 2 após convergência. Repetir POST com a mesma chave: nenhum novo lançamento. Parar toda Consolidação, registrar novos fatos e comprovar persistência; reiniciar e verificar somas exatas. Parar RabbitMQ, registrar e verificar Outbox pendente; restaurar broker e confirmar publicação. DLQ/redrive, autenticação, observabilidade e reconciliação entram na demonstração quando suas fases de implementação forem concluídas.

## Diagnóstico e limites

Falha do broker afeta o Worker, não readiness de Lançamentos. Se houver Outbox suspensa, seguir procedimento de recuperação. `docker compose stop` preserva volumes. Excluir volumes destrói dados locais; limpeza destrutiva não faz parte da sequência normal. O aceite local exige execução limpa por outro desenvolvedor, sem credenciais AWS e sem ajustes manuais no código.
