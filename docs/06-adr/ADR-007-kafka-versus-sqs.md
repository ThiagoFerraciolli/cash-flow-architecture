# ADR-007 — Kafka versus SQS para integração assíncrona

- **Status:** PROPOSTO

## Contexto

ADR-002 selecionou Kafka como tecnologia de referência por retenção, replay, backlog e recuperação. A arquitetura-alvo AWS torna necessário confrontar essa decisão com custo e simplicidade operacional.

O case informa 50 req/s no consolidado, o que **não é volume suficiente, isoladamente, para justificar Kafka**.

## Alternativas

### Amazon MSK / Kafka
- event log retido;
- replay/reprocessamento natural;
- múltiplos consumidores independentes;
- maior custo e complexidade.

### Amazon SQS
- entrega assíncrona gerenciada;
- retry/DLQ simples;
- cobrança por uso e baixo baseline;
- não possui o mesmo modelo de log retido/replay arbitrário de Kafka.

## Decisão para o desafio

**Manter Kafka na arquitetura de referência**, porque replay e recuperação são capacidades explicitamente valorizadas no desenho e tornam a demonstração de recuperação após indisponibilidade mais clara.

Entretanto, registrar SQS como alternativa economicamente preferível se, após validação com negócio/operação, os requisitos reais forem apenas desacoplamento, retry e entrega confiável sem necessidade de event log, múltiplos consumidores ou replay histórico.

## Consequências

- Kafka não será defendido por throughput;
- a Cost View deve evidenciar o custo/complexidade da decisão;
- implementação deve evitar dependências de domínio desnecessárias do broker;
- trocar o adaptador de integração por SQS não deve alterar o domínio financeiro;
- esta decisão pode ser revisitada sem mudar as fronteiras de Transactions e Consolidation.

## Critério de reavaliação

Preferir SQS se replay histórico/múltiplos consumidores não forem requisitos e o TCO de MSK não for justificável. Manter Kafka se retenção/replay/event streaming forem requisitos operacionais confirmados.