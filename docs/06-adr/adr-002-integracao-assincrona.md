# ADR-002 — Integração assíncrona orientada a eventos

- **Estado:** APROVADO
- **Direcionadores:** RNF-001/004/005/007, ASR-001/002/003/005.

## Alternativas e decisão

Chamada HTTP síncrona para consolidar antes de confirmar o lançamento acoplaria disponibilidade. Consulta direta da base de Lançamentos pelo serviço de Consolidação acoplaria modelo e credenciais. Adotar evento assíncrono com Outbox transacional e armazenamento próprio de projeção.

O registro depende apenas de seu commit local. Broker local RabbitMQ e broker AWS SQS Standard, conforme ADR-010; comparação com Kafka centralizada no ADR-007. A integração entrega pelo menos uma vez, exige identidades estáveis e não promete ordenação ou exactly-once.

## Consequências

Consulta é eventualmente consistente e informa isso no contrato. Fila/DLQ são transporte temporário; retenção e expiração não substituem a fonte financeira. Recuperação extraordinária usa snapshot e nova geração. Publicação e processamento possuem métricas próprias e escalam separadamente.

O consumidor confirma apenas após transação local bem-sucedida; deduplicação por lançamento protege redelivery e reemissão. O contrato versionado está no ADR-008; semânticas de cada adaptador no ADR-010. T06–T12 e T19 validam disponibilidade, falhas e contratos na implementação.
