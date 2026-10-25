# ADR-002 — Integração assíncrona orientada a eventos

- **Direcionadores:** RNF-001/004/005/007, ASR-001/002/003/005.

## Alternativas e decisão

Chamada HTTP síncrona para consolidar antes de confirmar o lançamento acoplaria disponibilidade. Consulta direta da base de Lançamentos pelo serviço de Consolidação acoplaria modelo e credenciais. Adotar evento assíncrono com Outbox transacional e armazenamento próprio de projeção.

O registro depende apenas de seu commit local. RabbitMQ é o transporte implementado no ambiente local; SQS Standard é a escolha da arquitetura-alvo AWS, conforme ADR-007 e ADR-010. Kafka/MSK foi avaliado apenas como alternativa e não integra a arquitetura adotada. A integração entrega pelo menos uma vez, exige identidades estáveis e não promete ordenação ou exactly-once.

## Consequências

Consulta é eventualmente consistente e informa isso no contrato. Fila/DLQ são transporte temporário; retenção e expiração não substituem a fonte financeira. Recuperação extraordinária usa snapshot e nova geração. Publicação e processamento possuem métricas próprias e escalam separadamente.

O consumidor confirma apenas após transação local bem-sucedida; deduplicação por lançamento protege redelivery e reemissão. O contrato versionado está no ADR-008; as semânticas do RabbitMQ implementado e do SQS projetado estão no ADR-010. T06–T12 e T19 pertencem ao mapa normativo de validação; a cobertura efetivamente executada e seus limites estão em `docs/08-testes/production-grade-local-traceability.md` e `docs/08-testes/validacao-local-final.md`.
