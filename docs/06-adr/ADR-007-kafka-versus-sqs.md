# ADR-007 — Kafka versus SQS para integração assíncrona

- **Status:** ACEITO PARA A ARQUITETURA DO DESAFIO
- **Drivers:** RNF-001, RNF-005, RNF-007, ASR-001, ASR-003, ASR-005
- **Relacionados:** ADR-002, ADR-003

## Contexto

A integração precisa permitir que Lançamentos continue aceitando operações quando a Consolidação estiver indisponível e deve recuperar trabalho pendente posteriormente.

O case informa 50 req/s no serviço de consolidado. Ele não exige replay histórico arbitrário, múltiplos grupos de consumidores, event sourcing ou plataforma de streaming. Portanto, a decisão deve privilegiar a menor solução que satisfaça os requisitos e ASRs, sem confundir capacidade opcional com necessidade.

## Alternativas

### Amazon MSK / Kafka

Vantagens:
- log retido e offsets;
- replay natural;
- múltiplos consumidores independentes;
- bom encaixe para event streaming.

Custos:
- maior baseline financeiro;
- maior superfície operacional;
- particionamento, retenção e operação do cluster passam a ser preocupações arquiteturais.

### Amazon SQS Standard

Vantagens:
- serviço gerenciado e cobrança por uso;
- desacoplamento temporal e backlog durável;
- redelivery, visibility timeout e DLQ/redrive nativos;
- escala muito superior à carga conhecida;
- menor esforço operacional para o problema atual.

Limites:
- não é event log;
- não oferece replay histórico arbitrário após remoção da mensagem;
- não garante ordenação global.

Esses limites não violam requisitos conhecidos: a consolidação é uma projeção derivável do banco de Lançamentos, e somas de créditos/débitos não dependem de ordem.

## Decisão

Usar **Amazon SQS Standard** na arquitetura AWS de referência.

A recuperação normal ocorre pelo backlog da fila. A recuperação de mensagens problemáticas ocorre por DLQ + redrive. Para divergência ou reconstrução excepcional, a fonte de verdade é o banco de Lançamentos e a projeção é refeita/reconciliada de forma controlada.

Kafka/MSK deixa de ser a escolha padrão porque suas capacidades adicionais não são requeridas pelo desafio e pioram custo/simplicidade. Essa mudança não altera os bounded contexts nem a semântica at-least-once.

## Por que Standard e não FIFO

O domínio conhecido não exige ordenação. O consumidor é idempotente e o consolidado usa incrementos atômicos, portanto duplicidade/redelivery são tratados explicitamente. Introduzir FIFO e `MessageGroupId` exigiria inventar uma chave de ordenação de negócio que o case não fornece.

## Consequências

- arquitetura e Cost View devem mostrar SQS + DLQ, não MSK;
- observabilidade usa métricas de fila/idade/DLQ em vez de consumer lag Kafka;
- contratos e domínio permanecem independentes do broker;
- a documentação de testes deve incluir indisponibilidade da fila, redelivery, DLQ/redrive e reconciliação;
- Kafka permanece opção de evolução se surgirem requisitos reais de replay histórico, event streaming ou vários consumidores independentes.

## Critérios de reavaliação

Reabrir este ADR se surgir pelo menos um requisito que altere materialmente a decisão, como:
- replay histórico arbitrário como operação frequente;
- múltiplos grupos de consumidores independentes sobre o mesmo histórico;
- retenção longa do event log como requisito;
- processamento sequencial por chave de negócio explicitamente definida;
- adoção corporativa obrigatória de uma plataforma Kafka já operada e cujo custo incremental seja baixo.