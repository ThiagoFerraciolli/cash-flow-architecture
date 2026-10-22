# ADR-004 — Outbox transacional com reserva temporária

- **Estado:** APROVADO
- **Direcionadores:** RNF-004, ASR-002; isolamento de falhas e concorrência entre publicadores.

## Decisão

Persistir lançamento, identidade do evento, chave de idempotência e Outbox no mesmo commit. A API não chama o broker. O Worker seleciona itens elegíveis com `FOR UPDATE SKIP LOCKED`, grava `lease_token` e `lease_until` e confirma uma transação curta. O envio à rede ocorre fora dessa transação.

Reserva de 60 s, renovada a cada 20 s, lote até 20 e paralelismo 4. Usar relógio do banco. Um publicador só atualiza `published_at`, falha ou renovação se seu token ainda corresponder à reserva atual. Token substituído impede atualização pelo proprietário antigo. Após reinício, reservas vencidas voltam a ser elegíveis.

Sucesso significa confirmação do adaptador: RabbitMQ confirm sem retorno de rota ou sucesso SQS. Falha após envio e antes de persistir sucesso gera republicação com o mesmo eventId; ADR-005 absorve duplicatas. Se a reserva vencer durante um envio, dois envios podem ocorrer; isso é tolerado, não é garantia de exclusão absoluta.

## Falhas persistentes e capacidade

Backoff exponencial com jitter. Após 20 falhas consecutivas por item, marcar `SUSPENDED`, preservar payload e gerar alerta. A operação diagnostica e libera nova rodada de tentativas; não há descarte automático. A distinção evita um item inválido consumir indefinidamente as mesmas tentativas e permite que outros avancem.

Limpar somente itens publicados há mais de 30 dias. O eventId permanece no lançamento, permitindo recuperação mesmo após limpeza. Monitorar disco, idade e suspensões. Outbox não é backup nem armazenamento ilimitado.

## Alternativas

Manter lock de banco enquanto chama o broker prolonga transações e ocupa conexões; foi rejeitado. Publicar diretamente na API cria dual write e dependência de disponibilidade; foi rejeitado. Reserva persistida exige token e recuperação de vencimento, custo aceito.

Testar dois publicadores, expiração da reserva, token antigo, ausência de rota RabbitMQ, falha de confirmação, suspensão e retomada. [Semântica PostgreSQL de SELECT e bloqueios](https://www.postgresql.org/docs/current/sql-select.html).
