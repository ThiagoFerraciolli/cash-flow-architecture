# Parâmetros operacionais e metas de referência

## Negócio e contratos

| Parâmetro | Decisão |
|---|---|
| Escopo | Um comerciante, um caixa lógico, moeda BRL; sem multi-tenant |
| Dia financeiro | `America/Sao_Paulo`, derivado de `occurredAt` obrigatório com offset; sem fechamento |
| Datas aceitas | Retroativas aceitas; instante futuro além de 5 minutos do relógio UTC do servidor rejeitado |
| Valor | Decimal textual, duas casas, de 0,01 a 999.999.999,99; sem arredondamento implícito |
| Agregação | Créditos menos débitos do próprio dia; não inclui saldo transportado de dias anteriores |
| Imutabilidade | Sem alteração, exclusão ou estorno no escopo |
| Descrição | Opcional, até 500 caracteres; não publicar no evento nem nos logs |
| HTTP | Corpo máximo 16 KiB; página padrão 50 e máxima 200; intervalo máximo 366 dias |
| Identidade | Cliente técnico autenticado; chave de idempotência no escopo de `issuer + client_id` |
| Idempotência | Sem expiração no escopo; registro mínimo retido junto ao fato financeiro |
| Dados financeiros e deduplicação ativa | Sem exclusão automática; futura política legal deve preservar capacidade de recuperação |

## Processamento

| Parâmetro | Valor inicial |
|---|---|
| Consulta de Outbox | A cada 1 s; lote máximo 20 |
| Reserva de publicação | 60 s por lote; renovação a cada 20 s; envio concorrente máximo 4 |
| Timeout de publicação | 10 s por tentativa |
| Retentativa Outbox | Sistêmica: circuit breaker, base 1 s e teto 60 s, sem suspender item; payload inválido: suspensão imediata; falha isolada indeterminada: suspensão após 20 tentativas |
| Limpeza Outbox publicada | Após 30 dias; nunca remover pendente ou suspensa |
| Consumidor | Até 8 operações concorrentes por processo; transação de banco limitada a 10 s |
| SQS | Long polling 20 s; lote até 10; visibilidade 60 s; extensão a cada 20 s se necessária |
| SQS redrive | `maxReceiveCount = 8`; retenção principal 7 dias e DLQ 14 dias; indisponibilidade compartilhada pausa polling e estende mensagens em curso |
| RabbitMQ | Mensagens persistentes, filas quorum duráveis, prefetch 16; confirmação manual |
| RabbitMQ retry | Até 5 tentativas de processamento; espera de 5 s, 15 s, 30 s e 60 s entre tentativas |
| RabbitMQ limite nativo | delivery-limit 100 na principal, DLX seguro para DLQ; distinto das 5 tentativas da aplicação |
| RabbitMQ capacidade local | 1 GiB por fila; overflow rejeita publicação, nunca remove mensagem antiga |
| RabbitMQ retenção | Sem TTL de negócio na fila principal/DLQ; monitorar disco e profundidade; retry com TTL técnico |
| Reprocessamento operacional | Iniciar a 10 mensagens/s; pausar se falha > 1% em 1 min ou idade da fila crescer por 5 min |
| Reconciliação | Diária às 03:00 no fuso de negócio e após incidente, restore ou reconstrução |
| Reconstrução de Consolidação | Toda a projeção; consumo pausado por controle persistente; escrita de Lançamentos continua |
| Restore de Lançamentos | Escrita e publicação pausadas; fila antiga isolada; destino físico novo; Consolidação reconstruída antes da reabertura |

Timeout de visibilidade e backoff RabbitMQ são mecanismos diferentes. O domínio não depende de igualdade de tempos ou de contadores entre brokers. Tamanho de mensagem da aplicação limitado a 16 KiB em ambos.

## Metas e alarmes

| Sinal | Meta ou ação inicial |
|---|---|
| Requisito explícito | 50 requisições/s no consolidado e falha/perda ≤ 5% |
| Ensaio principal | GET por data; aquecimento 2 min + medição 10 min; timeout HTTP 5 s |
| Latência técnica adotada | P95 ≤ 500 ms e P99 ≤ 1 s no ensaio de referência |
| Convergência normal | 99% dos lançamentos refletidos em até 60 s no ensaio de referência |
| Recuperação de backlog | 10.000 lançamentos acumulados em até 10 min após retomada, com escrita concorrente de 10/s |
| Disponibilidade AWS | Objetivo interno de 99,9% mensal por API; não é SLA contratual nem evidência obtida |
| Recuperação de falha de zona/processo | RTO alvo ≤ 15 min, a demonstrar em homologação |
| Restore de Lançamentos | RTO alvo ≤ 4 h e RPO alvo ≤ 5 min; medir ponto restaurável real; não afirmar perda zero em desastre |
| Restore/rebuild de Consolidação | RTO alvo ≤ 4 h para 1 milhão de lançamentos; projeção recuperável até os fatos disponíveis |
| Outbox | Aviso se item pendente > 60 s; crítico > 5 min ou qualquer item suspenso |
| Fila principal | Aviso idade > 60 s; crítico > 5 min; escalonar incidente SQS antes de 24 h de idade |
| DLQ | Qualquer mensagem gera alerta; triagem em até 4 h; escalonar idade > 24 h |
| Recursos | Aviso disco/banco > 70%, crítico > 85%; conexões limitadas a 5 por processo |
| HTTP | Erros > 1% por 5 min ou ausência de instâncias saudáveis geram alerta |
| Logs | Aplicação 30 dias; auditoria operacional 90 dias; sem payload financeiro sensível |
| Backups AWS | Automáticos/PITR 35 dias; snapshot antes de migração destrutiva; restore mensal em homologação |
| Revisão de crescimento | Abrir com 10 milhões de fatos, reconciliação > 5 min por 3 dias, restore/rebuild > 75% do RTO, armazenamento > 70% ou espaço sem duas gerações + snapshot + 20% de margem |

Os ensaios usam dados sintéticos. A independência é garantida pelo desenho no caminho de escrita, mas não implica capacidade infinita: crescimento de Outbox, armazenamento e conexões devem ser monitorados. Não bloquear a API apenas porque Consolidação ou broker estão indisponíveis; rejeitar com 503 quando a própria persistência não puder confirmar com segurança.
