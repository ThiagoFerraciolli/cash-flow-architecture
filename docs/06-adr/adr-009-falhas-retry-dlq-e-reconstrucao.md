# ADR-009 — Recuperação verificável a partir da fonte financeira

- **Direcionadores:** RNF-004/005/006/007/010; ASR-002/003/004/005/009.

## Decisão

Combinar retentativas limitadas, isolamento de falhas, reconciliação consistente e reconstrução integral em nova geração. O algoritmo normativo está em [recuperação e reconciliação](../07-infraestrutura/recuperacao-e-reconciliacao.md); tempos e limites em [parâmetros](../07-infraestrutura/parametros-operacionais.md).

A Outbox usa reserva e suspensão controlada (ADR-004). Publicador e consumidor classificam falhas em três grupos: sistêmica de dependência, permanente atribuível ao item e isolada ainda indeterminada. Adaptadores implementam confirmação, retry e DLQ conforme ADR-010. Nenhum caminho de erro financeiro pode confirmar e descartar silenciosamente uma mensagem.

Falha sistêmica do banco, broker, rede, credencial ou quota abre circuit breaker e pausa novos recebimentos. No SQS, o adaptador estende a visibilidade das mensagens já recebidas dentro do limite do serviço e interrompe o polling; no RabbitMQ, cancela o consumidor e devolve a entrega uma vez, reabrindo-o somente após readiness da dependência. Esses eventos não consomem tentativas de aplicação. Uma mensagem inválida segue diretamente para DLQ; uma falha isolada e indeterminada usa retentativas limitadas. Se uma indisponibilidade longa ainda levar uma mensagem à DLQ por semântica do transporte, o redrive auditado permanece obrigatório e não há perda silenciosa.

A fila é transporte temporário. Expiração SQS, perda de volume local ou mensagem ausente são recuperáveis a partir dos lançamentos persistidos. Reconciliação não depende da existência da Outbox publicada. Deduplicação financeira ativa não expira.

## Reconstrução aprovada

Pausar processamento por controle persistente; manter Lançamentos aceitando escrita; obter snapshot consistente de todos os fatos; construir totais e identidades na geração nova; validar; trocar geração atomicamente; retomar consumo. Mensagens anteriores ao snapshot tornam-se duplicatas; posteriores aplicam normalmente. A pausa começa antes do snapshot para não perder efeitos já confirmados pelo consumidor durante a construção.

## Alternativas e consequências

Replay exclusivo da fila foi rejeitado por retenção finita. Limpar projeção e deduplicação em produção foi rejeitado por janela de perda/duplicidade. Rebuild concorrente sem captura incremental foi rejeitado porque poderia perder eventos já confirmados. A pausa controlada é mais simples e verificável, ao custo de defasagem temporária e espaço para duas gerações.

A geração anterior serve para auditoria; não pode ser reativada depois da retomada sem nova reconstrução. Testes incluem falha do processo em cada etapa, evento em trânsito, expiração de fila, restauração de banco e comparação consistente.

Restore da fonte financeira é uma operação distinta de rebuild da projeção: bloqueia novas escritas, isola a geração antiga do transporte, restaura Lançamentos, cria uma geração física nova de filas e reconstrói Consolidação antes de reabrir o tráfego. Mensagens da geração antiga nunca são aplicadas automaticamente à fonte restaurada. O procedimento normativo define a ordem e a evidência exigida.
