# ADR-004 — Outbox transacional com reserva temporária

- **Direcionadores:** RNF-004, ASR-002; isolamento de falhas e concorrência entre publicadores.

## Decisão

Persistir lançamento, identidade do evento, chave de idempotência e Outbox no mesmo commit. A API não chama o broker. O Worker seleciona itens elegíveis com `FOR UPDATE SKIP LOCKED`, grava `lease_token` e `lease_until` e confirma uma transação curta. O envio à rede ocorre fora dessa transação.

Reserva de 60 s, renovada a cada 20 s, lote até 20 e paralelismo 4. Usar relógio do banco. Um publicador só atualiza `published_at`, falha ou renovação se seu token ainda corresponder à reserva atual. Token substituído impede atualização pelo proprietário antigo. Após reinício, reservas vencidas voltam a ser elegíveis.

Sucesso significa confirmação do adaptador: RabbitMQ confirm sem retorno de rota ou sucesso SQS. Falha após envio e antes de persistir sucesso gera republicação com o mesmo eventId; ADR-005 absorve duplicatas. Se a reserva vencer durante um envio, dois envios podem ocorrer; isso é tolerado, não é garantia de exclusão absoluta.

## Classificação de falhas e capacidade

Falha sistêmica do broker ou da rede — DNS, conexão, TLS, autenticação, throttling, indisponibilidade ou timeouts simultâneos em vários itens — abre o circuit breaker do publicador e pausa o lote. Reservas são liberadas ou deixadas expirar com `next_attempt_at` e backoff exponencial com jitter. Essa condição gera métrica e alerta próprios, mas não incrementa o contador que suspende cada item: uma indisponibilidade compartilhada não transforma mensagens válidas em mensagens defeituosas.

Falha permanente atribuível ao item — contrato inválido detectado antes do envio, tamanho acima do limite ou rejeição não retentável ligada ao payload — marca `SUSPENDED` imediatamente. Falha isolada cuja causa continue indeterminada usa backoff e, após 20 tentativas consecutivas, também marca `SUSPENDED`. Em ambos os casos, preservar payload e causa sanitizada, alertar e exigir diagnóstico antes de uma nova rodada. Não há descarte automático.

O circuit breaker usa sondagem controlada e só fecha depois de uma publicação de teste ou item real confirmada pelo adaptador. Falha de autorização ou configuração não é recuperada em laço apertado: permanece aberta até correção operacional. A classificação, a transição de estado e a identidade do operador que liberar item suspenso são auditáveis.

Limpar somente itens publicados há mais de 30 dias. O eventId permanece no lançamento, permitindo recuperação mesmo após limpeza. Monitorar disco, idade e suspensões. Outbox não é backup nem armazenamento ilimitado.

## Alternativas

Manter lock de banco enquanto chama o broker prolonga transações e ocupa conexões; foi rejeitado. Publicar diretamente na API cria dual write e dependência de disponibilidade; foi rejeitado. Reserva persistida exige token e recuperação de vencimento, custo aceito.

Testar dois publicadores, expiração da reserva, token antigo, ausência de rota RabbitMQ, falha de confirmação, suspensão e retomada. [Semântica PostgreSQL de SELECT e bloqueios](https://www.postgresql.org/docs/current/sql-select.html).
