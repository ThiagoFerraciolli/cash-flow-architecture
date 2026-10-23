# ADR-010 — Semântica de transporte: RabbitMQ local e SQS na AWS

- **Direcionadores:** execução local independente de cloud, RNF-001/006/007/014 e facilidade operacional na AWS.

## Escopo da decisão

O ADR-007 define a escolha tecnológica: RabbitMQ é o broker implementado no ambiente local e SQS Standard é a arquitetura-alvo AWS. Este ADR define como as garantias necessárias são mapeadas entre os transportes sem afirmar equivalência de implementação ou de evidência.

Uma fila AWS real localmente exigiria conta, rede e credenciais. Um emulador SQS permitiria testes de desenvolvimento, mas não provaria o comportamento do serviço gerenciado. Por isso, esta entrega implementa e demonstra somente o adaptador RabbitMQ. O adaptador SQS deverá ser implementado e homologado no ambiente AWS antes de produção.

Ambos devem transportar o contrato do ADR-008 e confirmar consumo somente após commit. A portabilidade se aplica às invariantes de negócio; operação, configuração e testes são específicos por transporte.

## Mapeamento

| Propriedade | RabbitMQ local — implementado | SQS AWS — arquitetura-alvo |
|---|---|---|
| Protocolo | AMQP 0-9-1 | API HTTPS/SigV4 |
| Destino lógico | Exchange direto `cashflow.events`, routing key `transaction.created.v1`, fila quorum `consolidation.transactions.v1` | Fila Standard `cashflow-consolidation-v1` |
| Publicação | Persistente, `mandatory`, publisher confirm e ausência de retorno por falta de rota | Sucesso de `SendMessage`; tratar falhas individuais se houver batch |
| Consumo | Ack manual após commit | `DeleteMessage` após commit |
| Falha/conexão | Entrega não confirmada pode retornar | Mensagem reaparece após visibilidade |
| Ordenação | Não depender de ordem | Não depender de ordem |
| Erro definitivo | Publicação confirmada na fila de erro e só então ack da original | Sem delete; redrive policy após limite |
| Retenção | Volumes persistentes e controle de capacidade | Principal 7 dias, DLQ 14 dias como parâmetro-alvo a validar |
| Escala | Consumidores concorrentes; nó único local não oferece HA | Serviço gerenciado; escala dos Workers independente |

## Retentativa RabbitMQ — implementada localmente

O adaptador republica falhas transitórias para filas quorum de espera com TTL de 5, 15, 30 ou 60 segundos e cabeçalho `retryAttempt`. Usa publicação persistente, mandatory e confirmação antes do ack da original. Cada fila de espera possui DLX para a fila principal. A perda do confirm deixa a original sem ack e pode gerar duplicata, absorvida pela deduplicação financeira.

Após cinco tentativas, publica na fila quorum `consolidation.transactions.v1.dlq` pelo mesmo protocolo de confirmação. Erro definitivo vai diretamente para ela. Se o destino de retry/DLQ estiver indisponível, a original permanece sem ack e o consumidor reconecta com backoff; não há descarte silencioso. A fila principal usa delivery-limit 100 e DLX seguro para DLQ, separando reconexões das cinco tentativas de aplicação.

Falha compartilhada da base de Consolidação não percorre as filas de espera por mensagem. O consumidor pausa recebimento e volta após a readiness do banco. As cinco tentativas são reservadas a falha isolada. Contrato inválido segue diretamente para DLQ.

## Retentativa SQS — comportamento requerido para implementação futura

O futuro adaptador SQS deverá renovar visibility timeout enquanto processa. Diante de falha compartilhada da base, deverá estender a visibilidade das mensagens já recebidas e interromper long polling. Falha definitiva não deverá executar `DeleteMessage`; a redrive policy será responsável pela progressão à DLQ. O contador de recebimentos pertence ao SQS e deverá ser tratado como semântica própria do serviço.

Essas regras são especificação da arquitetura-alvo, não evidência executada nesta entrega.

## Restore da fonte

Em restore de Lançamentos, a arquitetura-alvo prevê destinos físicos novos com sufixo imutável de recuperação, por exemplo `cashflow-consolidation-v1-r<runId>`, evitando mistura entre gerações. No ambiente local, um ensaio equivalente pode usar novo conjunto de filas ou novo virtual host. O rebuild normal de Consolidação não troca filas.

## Limites e validação

RabbitMQ não valida IAM, KMS, quotas, visibility timeout, retenção ou redrive SQS. Nenhuma infraestrutura AWS ou adaptador SQS foi executado nesta entrega. A homologação AWS deverá executar as mesmas invariantes de negócio e testes específicos do transporte gerenciado. Não há migração automática de mensagens RabbitMQ para SQS.

## Topologia RabbitMQ implementada

Exchange `cashflow.events` direto/durável, binding da principal em `transaction.created.v1`. Exchange direto/durável `cashflow.retry`, com routing keys `retry.5s`, `retry.15s`, `retry.30s`, `retry.60s`; filas quorum correspondentes `consolidation.retry.5s`, `.15s`, `.30s`, `.60s` com TTL fixo por fila e DLX para a rota principal. Exchange `cashflow.errors` direto/durável, binding para `consolidation.transactions.v1.dlq` em `transaction.created.v1.failed`.

Filas/exchanges/bindings são definidos antes do publicador; falha de rota mantém a Outbox recuperável. O ambiente local é deliberadamente um nó único e não representa HA de produção.

Referências técnicas de RabbitMQ e AWS usadas na decisão devem ser verificadas novamente durante a homologação produtiva, pois parâmetros e capacidades de serviços gerenciados podem evoluir.
