# ADR-009 — Recuperação verificável a partir da fonte financeira

- **Direcionadores:** RNF-004/005/006/007/010; ASR-002/003/004/005/009.

## Decisão

Combinar retentativas limitadas, isolamento de falhas e reconstrução integral em nova geração. Reconciliação contínua e restore/failover completos permanecem capacidades operacionais de evolução. O procedimento e os limites estão em [recuperação e reconciliação](../07-infraestrutura/recuperacao-e-reconciliacao.md) e [parâmetros](../07-infraestrutura/parametros-operacionais.md).

A Outbox usa reserva e suspensão controlada (ADR-004). No corte local implementado, Transactions Worker classifica falhas de publicação e aplica circuit breaker para falhas sistêmicas do RabbitMQ; Consolidation Worker usa ACK após commit, retry técnico e DLQ para falhas tratáveis/permanentes. Nenhum caminho intencional de erro financeiro confirma e descarta silenciosamente uma mensagem.

No RabbitMQ local, falha permanente de contrato pode seguir para DLQ e falhas isoladas usam retentativas limitadas. A evidência operacional T09 demonstrou somente o caminho de mensagem permanentemente inválida até a DLQ; redrive operacional completo não foi executado. Com indisponibilidade compartilhada do PostgreSQL de Consolidação, o consumidor pausa e deixa a mensagem recuperável no broker, retomando após readiness da dependência.

SQS Standard pertence apenas à arquitetura-alvo AWS. Visibilidade, polling, redrive e comportamento sob indisponibilidade descritos no ADR-010 são requisitos de um futuro adaptador SQS e precisam ser homologados em AWS antes de serem tratados como comportamento implementado.

A fila é transporte temporário. Perda ou expiração de transporte não deve ser a única fonte de recuperação: os lançamentos persistidos em Transactions são a fonte financeira. Deduplicação financeira ativa não expira no corte atual.

## Reconstrução implementada localmente

A ferramenta administrativa Recovery pausa o processamento por controle persistente com lease/fencing, mantém Transactions aceitando escrita, lê a fonte em snapshot PostgreSQL somente leitura, constrói e verifica uma geração nova e troca a geração ativa atomicamente. Se o processo morrer, a lease expira e o Worker volta a processar; o operador deve iniciar uma nova execução de Recovery. Não há restart automático do rebuild.

T10 demonstrou localmente a troca de geração e a reconstrução de uma data isolada com CREDIT 100, DEBIT 20, saldo 80 e contagem 2. Testes automatizados cobrem expiração, tomada de posse e fencing da lease. Isso não equivale a demonstrar todos os cenários normativos T10–T12, reconciliação contínua, redrive ou restore AWS.

## Alternativas e consequências

Replay exclusivo da fila foi rejeitado por retenção finita. Limpar projeção e deduplicação em produção foi rejeitado por janela de perda/duplicidade. Rebuild concorrente sem controle de manutenção foi rejeitado porque poderia perder efeitos já confirmados. A pausa controlada é mais simples e verificável, ao custo de defasagem temporária e espaço para duas gerações.

A geração anterior serve para auditoria e não deve ser reativada depois da retomada sem nova reconstrução. Restore da fonte financeira é uma operação distinta do rebuild da projeção e permanece procedimento arquitetural a ser ensaiado antes de uso produtivo.
