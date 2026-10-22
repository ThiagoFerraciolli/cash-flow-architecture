# ADR-010 — RabbitMQ local e SQS na AWS

- **Direcionadores:** execução local independente de cloud, RNF-001/006/007/014 e facilidade operacional na AWS.

## Alternativas e decisão

Uma fila AWS real localmente exigiria conta, rede e credenciais. Um emulador SQS permitiria testes de desenvolvimento, mas não provaria o comportamento do serviço gerenciado. RabbitMQ é executável em containers e foi escolhido para o ambiente local. SQS Standard permanece na AWS para reduzir operação de infraestrutura. RabbitMQ também poderia operar na cloud, mas essa não é a topologia adotada.

Implementar dois adaptadores reais, selecionados por configuração. Ambos transportam o contrato do ADR-008 e confirmam consumo somente após commit. Portabilidade se aplica às regras de negócio; operação e testes são específicos por transporte.

## Mapeamento

| Propriedade | RabbitMQ local | SQS na AWS |
|---|---|---|
| Protocolo | AMQP 0-9-1 | API HTTPS/SigV4 |
| Destino lógico | Exchange direto `cashflow.events`, routing key `transaction.created.v1`, fila quorum `consolidation.transactions.v1` | Fila Standard `cashflow-consolidation-v1` |
| Publicação | Persistente, `mandatory`, publisher confirm e ausência de retorno por falta de rota | Sucesso de `SendMessage`; tratar falhas individuais se houver batch |
| Consumo | Ack manual após commit | `DeleteMessage` após commit |
| Falha/conexão | Entrega não confirmada pode retornar | Mensagem reaparece após visibilidade |
| Ordenação | Não depender de ordem | Não depender de ordem |
| Erro definitivo | Publicação confirmada na fila de erro e só então ack da original | Sem delete; redrive policy após limite |
| Retenção | Volumes persistentes e controle de capacidade | Principal 7 dias, DLQ 14 dias |
| Escala | Consumidores concorrentes; nó único local não oferece HA | Serviço gerenciado; escala dos Workers independente |

## Retentativa RabbitMQ

O adaptador republica falhas transitórias para filas quorum de espera com TTL de 5, 15, 30 ou 60 segundos e cabeçalho `retryAttempt`. Usa publicação persistente, mandatory e confirmação antes do ack da original. Cada fila de espera possui DLX para a fila principal. Habilitar dead-lettering pelo menos uma vez nas quorum queues, com `overflow=reject-publish` e política compatível com a versão fixada. A perda do confirm deixa a original sem ack e pode gerar duplicata, absorvida pela deduplicação financeira.

Após cinco tentativas, publicar na fila quorum `consolidation.transactions.v1.dlq` pelo mesmo protocolo de confirmação. Erro definitivo vai diretamente para ela. Se o destino de retry/DLQ estiver indisponível, manter original sem ack, pausar novos recebimentos e reconectar com backoff; não executar requeue em laço apertado. A fila principal usa delivery-limit 100 e DLX seguro para DLQ, separando reconexões das cinco tentativas de aplicação; nunca descartar silenciosamente em reconexões repetidas. Redrive preserva payload e identidades; reinicia somente contador técnico, com auditoria.

Falha compartilhada da base de Consolidação não percorre as filas de espera por mensagem. O consumidor cancela o recebimento, devolve a entrega em curso uma única vez e só volta após a readiness do banco. As cinco tentativas são reservadas a falha isolada cuja causa ainda não foi classificada. Contrato inválido segue diretamente para DLQ.

## Retentativa SQS

O consumidor renova visibility timeout enquanto processa. Diante de falha compartilhada da base, estende a visibilidade das mensagens já recebidas e interrompe long polling; readiness restabelecida libera a retomada. Falha definitiva não executa `DeleteMessage` e pode ajustar a visibilidade para acelerar o redrive. Falha isolada permanece sem delete até o limite da redrive policy. O contador de recebimentos pertence ao SQS e pode avançar em corridas ou indisponibilidades longas; chegada à DLQ exige triagem ou redrive, nunca exclusão automática.

## Gerações físicas para restore da fonte

Os nomes acima são destinos lógicos. Em restore de Lançamentos, criar destinos físicos novos com sufixo imutável de recuperação, por exemplo `cashflow-consolidation-v1-r<runId>`, e atualizar publicador e consumidor por configuração auditada. Fila principal, retries e DLQ antigos ficam isolados e somente leitura operacional até o encerramento do incidente; não se faz redrive entre gerações. No ambiente local, o ensaio equivalente usa novo conjunto de filas ou novo virtual host. Rebuild normal de Consolidação não troca filas.

## Limites e validação

RabbitMQ não valida IAM, KMS, quotas, visibilidade ou expiração SQS. A homologação AWS deve executar a mesma suíte de invariantes mais testes próprios do SQS. Dados locais são sintéticos; não há migração automática de mensagens RabbitMQ para SQS. O plano de implantação define a troca de ambiente.

Referências oficiais consultadas em 22/10/2026: [confirmações RabbitMQ](https://www.rabbitmq.com/docs/confirms), [dead-letter exchanges](https://www.rabbitmq.com/docs/dlx), [quorum queues](https://www.rabbitmq.com/docs/quorum-queues), [retenção SQS](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/quotas-messages.html).

## Topologia RabbitMQ de referência

Exchange `cashflow.events` direto/durável, binding da principal em `transaction.created.v1`. Exchange direto/durável `cashflow.retry`, com routing keys `retry.5s`, `retry.15s`, `retry.30s`, `retry.60s`; filas quorum correspondentes `consolidation.retry.5s`, `.15s`, `.30s`, `.60s` com TTL fixo por fila e DLX `cashflow.events`/routing key `transaction.created.v1`. Exchange `cashflow.errors` direto/durável, binding para `consolidation.transactions.v1.dlq` em `transaction.created.v1.failed`. A fila principal referencia esse DLX para limite nativo; retry final da aplicação publica diretamente nesse exchange com confirmação.

Definir filas/exchanges/bindings antes de iniciar publicador; falha de rota mantém Outbox pendente. Nas filas quorum que transferem por DLX, usar política `dead-letter-strategy=at-least-once`, `overflow=reject-publish` e feature flags compatíveis. No local, limite inicial por fila de 1 GiB e alarme de disco em 70%; ao atingir limite, rejeitar novas publicações para preservar origem, sem drop-head. Rejeição do broker deve ser tratada como envio não confirmado. Não configurar TTL de negócio na principal ou DLQ.
