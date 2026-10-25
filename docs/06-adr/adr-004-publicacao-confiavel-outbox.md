# ADR-004 — Outbox transacional com reserva temporária

- **Direcionadores:** RNF-004, ASR-002; isolamento de falhas e concorrência entre publicadores.

## Decisão

Persistir lançamento, identidade do evento, chave de idempotência e Outbox no mesmo commit. A API não chama o broker. O Worker seleciona itens elegíveis com `FOR UPDATE SKIP LOCKED`, grava `lease_token` e `lease_until` e confirma uma transação curta. O envio à rede ocorre fora dessa transação.

Reserva de 60 s, renovada a cada 20 s, lote até 20 e paralelismo 4. Usar relógio do banco. Um publicador só atualiza `published_at`, falha ou renovação se seu token ainda corresponder à reserva atual. Token substituído impede atualização pelo proprietário antigo. Após reinício, reservas vencidas voltam a ser elegíveis.

No corte implementado, sucesso significa confirmação do adaptador RabbitMQ: publicação persistente/`mandatory`, publisher confirm e ausência de retorno por falta de rota. Falha após envio e antes de persistir sucesso gera possibilidade de republicação com o mesmo eventId; ADR-005 absorve duplicatas. Se a reserva vencer durante um envio, dois envios podem ocorrer; isso é tolerado, não é garantia de exclusão absoluta. SQS é arquitetura-alvo e não participa desta implementação; sua semântica exigida está no ADR-010.

## Classificação de falhas e capacidade

No Worker RabbitMQ implementado, falhas classificadas como sistêmicas — por exemplo conexão/socket, broker indisponível, interrupção/fechamento do canal, publish exception, ausência de rota e timeout/cancelamento do envio — abrem o circuit breaker do publicador e pausam novas tentativas até a próxima sondagem. Reservas são marcadas com falha sistêmica e `next_attempt_at` com backoff exponencial e jitter, sem consumir o contador de tentativas isoladas do item.

Evento Outbox inválido detectado antes do envio é falha permanente e marca `SUSPENDED`. Falha de publicação não classificada como sistêmica usa backoff isolado; o store aplica a política de tentativas/suspensão. Não há descarte automático do payload.

O circuit breaker implementado usa sondagem do adaptador após o backoff e é fechado quando uma publicação real é confirmada e marcada com sucesso. Readiness do Worker cai nas falhas sistêmicas classificadas. A entrega não implementa uma interface administrativa de liberação de item suspenso nem identidade de operador para essa liberação; isso permanece capacidade operacional de evolução.

A política de retenção/limpeza de itens publicados e a operação administrativa de itens suspensos são requisitos operacionais de produção, não jobs demonstrados nesta entrega local. Outbox não é backup nem armazenamento ilimitado; idade, volume e suspensões devem ser monitorados em produção.

## Alternativas

Manter lock de banco enquanto chama o broker prolonga transações e ocupa conexões; foi rejeitado. Publicar diretamente na API cria dual write e dependência de disponibilidade; foi rejeitado. Reserva persistida exige token e recuperação de vencimento, custo aceito.

Testes automatizados cobrem aspectos da reserva/publicação e a regressão local valida o fluxo integrado. Cenários operacionais adicionais — dois publicadores reais concorrentes, administração de suspensão e políticas produtivas de retenção — não devem ser inferidos como demonstrados apenas por esta decisão. [Semântica PostgreSQL de SELECT e bloqueios](https://www.postgresql.org/docs/current/sql-select.html).