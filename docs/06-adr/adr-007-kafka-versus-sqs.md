# ADR-007 — SQS Standard como broker AWS

- **Direcionadores:** RNF-001/005/007; simplicidade operacional e custo.

## Alternativas

| Alternativa | Benefício | Custo/limite para este escopo |
|---|---|---|
| SQS Standard | Fila gerenciada, desacoplamento, redelivery e DLQ | Retenção finita, sem replay após remoção e sem ordenação garantida |
| SQS FIFO | Ordenação por grupo e deduplicação de envio no serviço | Ordenação não é necessária; não substitui idempotência financeira |
| Kafka/MSK | Log retido, replay e consumidores independentes | Operação/partições e custo de referência superiores, capacidades não exigidas |
| RabbitMQ na AWS | Mesmo broker do local e roteamento flexível | Exige operar cluster ou contratar serviço; SQS reduz essa responsabilidade |

## Decisão

Adotar SQS Standard na AWS. Somar fatos imutáveis não exige ordem global. Deduplicação financeira e UPSERT garantem correção mesmo com repetição. A rejeição de FIFO decorre da falta de necessidade de ordenação, não de impossibilidade técnica de escolher uma chave de grupo.

A reconstrução usa fatos persistidos, não histórico do broker. Retenção principal 7 dias/DLQ 14 dias com alarmes e reconciliação; expiração exige recuperação pela fonte. O ambiente local usa RabbitMQ por decisão própria no ADR-010.

## Reavaliação

Reabrir se surgirem replay histórico frequente, múltiplos consumidores independentes, streaming, ordenação de negócio ou plataforma corporativa existente com vantagem comprovada. As 50 consultas/s, isoladamente, não justificam Kafka. Testar capacidade da solução completa; não inferir capacidade do banco/Worker a partir da escala do broker.
