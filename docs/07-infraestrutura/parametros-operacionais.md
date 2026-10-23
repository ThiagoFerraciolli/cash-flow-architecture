# Parâmetros operacionais e metas de referência

Este documento reúne três categorias diferentes. Elas não devem ser confundidas:

1. **REQUISITO DO DESAFIO:** fornecido nos PDFs.
2. **IMPLEMENTAÇÃO LOCAL:** parâmetro existente no código/Compose entregue.
3. **META / ARQUITETURA-ALVO:** escolha para orientar produção/homologação; não é requisito do cliente nem evidência executada.

## Negócio e contratos — decisões adotadas

| Parâmetro | Decisão |
|---|---|
| Escopo | Um comerciante, um caixa lógico, moeda BRL; sem multi-tenant |
| Dia financeiro | `America/Sao_Paulo`, derivado de `occurredAt` obrigatório com offset |
| Datas aceitas | Retroativas aceitas; instante futuro além de 5 minutos do relógio UTC rejeitado |
| Valor | Decimal textual, duas casas, de 0,01 a 999.999.999,99 |
| Agregação | Créditos menos débitos do próprio dia; sem saldo transportado |
| Imutabilidade | Sem alteração, exclusão ou estorno no escopo |
| Descrição | Opcional, até 500 caracteres; não publicada no evento |
| HTTP | Corpo máximo 16 KiB; página padrão 50/máxima 200; intervalo máximo 366 dias |
| Idempotência | Escopo `issuer + client_id + key`; sem expiração no estudo de caso |

Esses valores refinam ambiguidades do desafio; não devem ser atribuídos aos PDFs como requisitos explícitos.

## Processamento local implementado

| Parâmetro | Valor |
|---|---|
| Consulta de Outbox | A cada 1 s; lote máximo 20 |
| Reserva de publicação | 60 s; renovação a cada 20 s; envio concorrente máximo 4 |
| Timeout de publicação | 10 s por tentativa |
| Outbox | Falha sistêmica abre circuit breaker; payload inválido suspende; falha isolada possui limite de tentativas |
| Consumidor | Até 8 operações concorrentes por processo; prefetch RabbitMQ 16 |
| RabbitMQ | Mensagens persistentes, filas quorum duráveis e ACK manual após commit |
| RabbitMQ retry | Filas técnicas com 5 s, 15 s, 30 s e 60 s; limite da aplicação separado do delivery-limit nativo |
| Recovery | Lease 60 s; renovação 15 s; owner token/fencing; nova geração |

Esses parâmetros descrevem a implementação local. A existência da configuração não significa que cada limite tenha sido submetido a teste operacional específico.

## Requisito explícito e evidência local

| Item | Origem | Evidência |
|---|---|---|
| Consolidado recebe 50 req/s | DESAFIO | T15 local: 6.000/6.000 GETs em 120 s a 50 req/s |
| Perda/falha máxima 5% | DESAFIO | T15 local: 0 falhas e 0 dropped iterations na janela executada |
| Transactions permanece disponível se Consolidação cair | DESAFIO | T06 local: API/Worker/banco da Consolidação parados; Transactions live/ready 200 e dois POSTs 201 |

O T15 comprova o comportamento **somente na janela e ambiente executados**. Não constitui SLA, benchmark AWS nem demonstração de 10 minutos/30.000 requisições.

## Metas internas de engenharia — não exigidas pelo desafio

| Sinal | Meta de referência |
|---|---|
| Latência | P95 ≤ 500 ms e P99 ≤ 1 s |
| Convergência normal | 99% dos lançamentos refletidos em até 60 s |
| Recuperação de backlog | 10.000 lançamentos em até 10 min após retomada, com escrita concorrente |
| Disponibilidade AWS | 99,9% mensal por API como objetivo interno |
| Falha de zona/processo | RTO alvo ≤ 15 min |
| Restore de Transactions | RTO alvo ≤ 4 h e RPO alvo ≤ 5 min |
| Restore/rebuild de Consolidation | RTO alvo ≤ 4 h para massa de referência |

Essas metas orientam uma futura homologação. Exceto pelas latências observadas no T15 local, elas **não foram demonstradas** nesta entrega.

## Arquitetura-alvo AWS / SQS

Parâmetros como long polling SQS, visibility timeout, redrive, retenção, Multi-AZ, backups/PITR, alarmes CloudWatch e políticas IAM/KMS pertencem à arquitetura-alvo AWS. O adaptador SQS e a infraestrutura AWS não existem no código entregue e não foram homologados.

Valores de referência propostos para essa futura implementação incluem long polling de 20 s, lote até 10, visibility timeout inicial de 60 s e DLQ com retenção maior que a fila principal. Esses números devem ser recalibrados em homologação; não são evidência local.

## Operação e capacidade

A independência entre Transactions e Consolidation é uma propriedade arquitetural, mas não implica capacidade infinita. Outbox, filas, armazenamento, conexões e idade do backlog devem ser monitorados. A API de Transactions não deve ser bloqueada apenas porque Consolidation ou o broker estão indisponíveis; deve rejeitar quando sua própria persistência não puder confirmar o fato com segurança.

A [estratégia de testes](../05-arquitetura-software/estrategia-testes.md) mantém cenários normativos mais amplos. A evidência realmente executada e seus limites estão em [docs/08-testes](../08-testes/validacao-local-final.md).