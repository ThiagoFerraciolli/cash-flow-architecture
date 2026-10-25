# ADR-010 — Semântica de transporte: RabbitMQ local e SQS AWS

- **Direcionadores:** execução local independente de cloud, RNF-001/006/007/014 e facilidade operacional na AWS.
- **Decisão tecnológica:** [ADR-007](adr-007-estrategia-de-mensageria.md).

## Escopo desta decisão

O ADR-007 define **por que** RabbitMQ foi adotado localmente e SQS Standard foi escolhido para a arquitetura-alvo AWS. Este ADR define **como** as propriedades de entrega, confirmação, retry e DLQ devem funcionar em cada transporte.

A entrega atual contém implementação real de **RabbitMQ**. O comportamento de **SQS** abaixo é especificação de arquitetura-alvo: não existe adaptador SQS no código entregue e nenhuma homologação AWS foi executada.

## Mapeamento

| Propriedade | RabbitMQ local — implementado | SQS AWS — alvo |
|---|---|---|
| Protocolo | AMQP 0-9-1 | API HTTPS/SigV4 |
| Destino lógico | Exchange `cashflow.events`, routing key `transaction.created.v1`, fila `consolidation.transactions.v1` | Fila Standard `cashflow-consolidation-v1` |
| Publicação | Persistente, `mandatory`, publisher confirm e ausência de retorno por falta de rota | Sucesso de `SendMessage`; tratar falhas individuais em batch |
| Consumo | Ack manual após commit | `DeleteMessage` após commit |
| Falha/conexão | Entrega não confirmada pode retornar | Mensagem reaparece após visibility timeout |
| Ordenação | Não depender de ordem | Não depender de ordem |
| Erro definitivo | Publicar na fila de erro e somente então ack da original | Sem delete; redrive policy após limite |
| Retenção | Volume persistente e controle local de capacidade | Principal 7 dias, DLQ 14 dias na referência AWS |
| Escala | Consumidores concorrentes; nó único local não oferece HA | Serviço gerenciado; escala dos Workers independente |

## RabbitMQ — comportamento da implementação local

A topologia local usa exchange durável `cashflow.events`, routing key `transaction.created.v1` e fila principal `consolidation.transactions.v1`. O publicador da Outbox usa publicação persistente, `mandatory` e confirmação do broker; falha de publicação mantém o item da Outbox pendente para nova tentativa. O consumidor confirma a mensagem somente após o commit da Consolidação.

Falhas transitórias de processamento usam filas de espera com TTL e retorno à fila principal. Erros permanentes seguem para DLQ. A implementação deve evitar requeue em laço apertado e preservar payload e identidades em retentativas. Duplicatas possíveis por redelivery são absorvidas pela idempotência financeira descrita no ADR-005.

Falha compartilhada do banco de Consolidação não deve transformar indisponibilidade sistêmica em uma sequência agressiva de retries por mensagem: o Worker sinaliza indisponibilidade/readiness e retoma quando a dependência volta. O cenário operacional executado T09 comprovou somente a parte limitada de mensagem permanentemente inválida chegando à DLQ sem alterar a projeção; redrive operacional completo não foi executado.

## SQS — especificação da arquitetura-alvo

Na AWS, a decisão é SQS Standard. O consumidor deverá renovar visibility timeout durante processamento longo, executar `DeleteMessage` somente após commit e usar redrive policy para DLQ. Falha compartilhada do banco deve interromper novos recebimentos e preservar mensagens já recebidas por extensão de visibilidade. Falha permanente não deve ser silenciosamente descartada.

Essas regras são requisitos para o futuro adaptador SQS, não descrição do código atual. RabbitMQ local não valida IAM, KMS, quotas, visibility timeout, redrive policy nem comportamento real do serviço SQS. A homologação AWS deverá executar invariantes equivalentes e testes específicos do transporte gerenciado.

## Restore da fonte

Os nomes de filas são destinos lógicos. Em restore de Lançamentos na arquitetura-alvo, uma nova geração física de destinos pode ser criada para isolar mensagens anteriores ao ponto restaurado. Rebuild normal da Consolidação não exige troca de filas. O procedimento normativo está em [recuperação e reconciliação](../07-infraestrutura/recuperacao-e-reconciliacao.md).

## Consequências

- Portabilidade aplica-se às regras de negócio e ao contrato de integração, não à equivalência operacional entre brokers.
- A evidência local demonstra RabbitMQ; não demonstra SQS.
- Introduzir o adaptador SQS exigirá implementação, testes e homologação antes de produção.
- Não há migração automática de mensagens RabbitMQ para SQS.

Referências de tecnologia usadas no desenho: [RabbitMQ publisher confirms](https://www.rabbitmq.com/docs/confirms), [RabbitMQ dead-letter exchanges](https://www.rabbitmq.com/docs/dlx), [RabbitMQ quorum queues](https://www.rabbitmq.com/docs/quorum-queues) e [Amazon SQS quotas/retention](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/quotas-messages.html).