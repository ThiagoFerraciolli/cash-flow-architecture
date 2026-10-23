# ADR-007 — Estratégia de mensageria e escolha tecnológica

- **Direcionadores:** RNF-001/005/007; independência entre Lançamentos e Consolidação; simplicidade operacional; custo; execução local reproduzível.

## Contexto

A solução precisa desacoplar a confirmação de um lançamento do processamento da Consolidação. O desafio não prescreve uma tecnologia de broker; a decisão deve preservar entrega assíncrona, tolerância a redelivery e integridade financeira sem introduzir capacidades que o caso não exige.

## Alternativas avaliadas

| Alternativa | Benefício | Decisão para este escopo |
|---|---|---|
| RabbitMQ | Executável em containers, publisher confirms, ack manual, DLQ e filas quorum | **Adotado e implementado no ambiente local** |
| SQS Standard | Serviço gerenciado, redelivery, DLQ e baixa responsabilidade operacional | **Adotado como arquitetura-alvo AWS; não implementado nem homologado nesta entrega** |
| SQS FIFO | Ordenação por grupo e deduplicação de envio no serviço | Rejeitado: ordenação não é requisito e não substitui idempotência financeira |
| Kafka/MSK | Log retido, replay e múltiplos consumidores independentes | Rejeitado: capacidades, operação e custo não se justificam pelos requisitos atuais |
| RabbitMQ na AWS | Mantém o mesmo broker do ambiente local | Rejeitado para a arquitetura-alvo: exigiria operar/contratar o broker quando SQS atende ao caso com menor responsabilidade operacional |

## Decisão

Usar **RabbitMQ como broker da implementação local**. O fluxo local efetivamente implementado é Outbox PostgreSQL → Transactions Worker → RabbitMQ → Consolidation Worker, com entrega at-least-once, publisher confirms, ack após commit, retry/DLQ e idempotência financeira.

Para a **arquitetura-alvo AWS**, adotar **SQS Standard**. SQS é uma decisão de produção projetada; esta entrega não contém adaptador SQS, configuração `Messaging__Provider=Sqs`, provisionamento AWS ou homologação do transporte gerenciado.

As regras de negócio e o contrato de evento permanecem independentes do transporte. As diferenças operacionais entre RabbitMQ local e SQS na AWS são especificadas no ADR-010 e devem ser validadas no ambiente correspondente antes de produção.

Não depender de ordenação global. A reconstrução da Consolidação usa os fatos persistidos em Transactions como fonte, não o histórico do broker. Repetições de publicação ou entrega são absorvidas pela idempotência do consumidor.

## Consequências

- O ambiente local é reproduzível sem conta AWS.
- A evidência local comprova o comportamento com RabbitMQ; não comprova semântica, quotas, IAM, KMS, visibility timeout ou redrive do SQS.
- A adoção de SQS exige implementação do adaptador e homologação AWS antes de uso produtivo.
- Não existe requisito atual que justifique Kafka/MSK ou SQS FIFO.

## Reavaliação

Reabrir esta decisão se surgirem replay histórico frequente, streaming, múltiplos consumidores independentes, ordenação de negócio, plataforma corporativa obrigatória ou evidência de que SQS Standard não atende aos requisitos operacionais. As 50 consultas/s do Consolidado, isoladamente, não justificam Kafka e não determinam a tecnologia de mensageria.
