# ADR-009 — Recuperação verificável a partir da fonte financeira

- **Estado:** APROVADO
- **Direcionadores:** RNF-004/005/006/007/010; ASR-002/003/004/005/009.

## Decisão

Combinar retentativas limitadas, isolamento de falhas, reconciliação consistente e reconstrução integral em nova geração. O algoritmo normativo está em [recuperação e reconciliação](../07-infraestrutura/recuperacao-e-reconciliacao.md); tempos e limites em [parâmetros](../07-infraestrutura/parametros-operacionais.md).

A Outbox usa reserva e suspensão controlada (ADR-004). O consumidor diferencia falha transitória de contrato inválido. Adaptadores implementam confirmação, retry e DLQ conforme ADR-010. Nenhum caminho de erro financeiro pode confirmar e descartar silenciosamente uma mensagem.

A fila é transporte temporário. Expiração SQS, perda de volume local ou mensagem ausente são recuperáveis a partir dos lançamentos persistidos. Reconciliação não depende da existência da Outbox publicada. Deduplicação financeira ativa não expira.

## Reconstrução aprovada

Pausar processamento por controle persistente; manter Lançamentos aceitando escrita; obter snapshot consistente de todos os fatos; construir totais e identidades na geração nova; validar; trocar geração atomicamente; retomar consumo. Mensagens anteriores ao snapshot tornam-se duplicatas; posteriores aplicam normalmente. A pausa começa antes do snapshot para não perder efeitos já confirmados pelo consumidor durante a construção.

## Alternativas e consequências

Replay exclusivo da fila foi rejeitado por retenção finita. Limpar projeção e deduplicação em produção foi rejeitado por janela de perda/duplicidade. Rebuild concorrente sem captura incremental foi rejeitado porque poderia perder eventos já confirmados. A pausa controlada é mais simples e verificável, ao custo de defasagem temporária e espaço para duas gerações.

A geração anterior serve para auditoria; não pode ser reativada depois da retomada sem nova reconstrução. Testes incluem falha do processo em cada etapa, evento em trânsito, expiração de fila, restauração de banco e comparação consistente.
