# Procedimentos de recuperação e reconciliação

## Autoridade, segurança e registro

A ferramenta administrativa local `tools/CashFlow.Recovery` é executada fora das APIs públicas. Lê um snapshot somente leitura da base de Lançamentos e escreve uma geração nova apenas na base de Consolidação. Não altera dados autoritativos de Transactions. A operação local foi demonstrada em T10; automação de reconciliação contínua, trilha operacional completa e credenciais temporárias AWS descritas nas etapas abaixo permanecem arquitetura-alvo. Rebuild da projeção não interrompe o registro de lançamentos. Restore da própria fonte é exceção: exige indisponibilidade controlada de escrita para definir um corte seguro.

## Controle persistente

`projection_control` possui uma única linha com `active_generation_id`, `maintenance`, `maintenance_run_id`, `maintenance_started_at` e `maintenance_expires_at`. O Recovery adquire a manutenção por `UPDATE` condicional atômico; o PostgreSQL usa `clock_timestamp()` como tempo autoritativo. Todo consumidor inicia transação, obtém `FOR SHARE` nessa linha e verifica se a lease de manutenção ainda é válida antes de qualquer efeito. A trava é mantida até commit. Workers que encontrem lease válida não aplicam nem confirmam mensagens; pausam recebimentos. Após a expiração, o Worker trata a manutenção como inativa e pode retomar.

A lease local dura 60 s e é renovada a cada 15 s durante o rebuild. Somente o token proprietário pode renová-la, liberá-la ou ativar a geração; uma lease válida impede outro Recovery. Após morte abrupta do processo, ela expira e outro dono pode adquiri-la sem intervenção manual no banco. O comando de rebuild não reinicia automaticamente: o operador deve iniciar outra execução. O timeout de lock de 30 s e o registro operacional completo descritos na especificação-alvo não são evidência de configuração local executada.

## Reconciliação consistente — especificação-alvo

1. Ativar manutenção e aguardar o escoamento das transações de consumo pelo protocolo acima. Lançamentos e publicador continuam.
2. Abrir transação somente leitura `REPEATABLE READ` em Lançamentos. Exportar todos os fatos visíveis, incluindo transactionId, creationEventId, data, tipo e valor, para artefato temporário criptografado, acessível apenas à operação. Registrar contagem e hash do conjunto ordenado; fechar a transação após a exportação.
3. Ler geração ativa, totais e `ProcessedTransaction` com snapshot consistente em Consolidação, que permanece estável por manutenção.
4. Verificar integridade interna: totais diários devem equivaler às somas e contagens do próprio registro de identidades. Comparar cada identidade processada com o fato de origem; dado divergente, duplicidade ou identidade sem origem é corrupção a investigar.
5. Fatos presentes no snapshot de origem e ausentes na projeção são pendências de integração, não automaticamente corrupção. Reportar idade, Outbox, fila e DLQ. A ausência no broker não prova perda; se persistir além da meta e não houver progresso, executar reconstrução.
6. Se tudo estiver correto, registrar a comparação como válida **para o snapshot**, remover manutenção com verificação do runId e retomar consumo. Nunca afirmar completude em tempo real a partir dessa verificação.

A pausa anterior ao snapshot impede que a comparação confunda efeitos processados depois do corte com inconsistência. Novos lançamentos após o snapshot ficam fora da comparação e serão integrados normalmente. A operação diária deve durar menos de 5 min para o conjunto de referência de 1 milhão de fatos; se exceder, alertar e revisar dimensionamento. Projeção continua consultável e marcada como em reconstrução/manutenção.

## Reconstrução integral por geração — procedimento normativo

Os passos abaixo incluem manifesto durável, retenção e reconciliação posterior projetados. A ferramenta local implementa snapshot somente leitura, construção e verificação da geração, lease renovável com fencing e ativação atômica; a [rastreabilidade T10–T12](../08-testes/production-grade-local-traceability.md) delimita a evidência executada.

1. Iniciar manutenção pelo protocolo anterior. Se já ativa pelo mesmo runId após crash, verificar registros e retomar de etapa segura; outro runId exige encerramento auditado da operação anterior.
2. Produzir snapshot consistente da fonte conforme reconciliação. Persistir manifesto e estado `SNAPSHOT_READY`; se o snapshot ficar incompleto, descartá-lo e obter outro mantendo manutenção.
3. Criar `generation_id` nova em estado `BUILDING`. Carregar uma identidade por transactionId e creationEventId e agregar totais por businessDate. Não emitir eventos novos nem alterar Lançamentos.
4. Validar unicidade, hashes, precisão, totais e contagens contra o mesmo snapshot. Estado `VALIDATED` somente após igualdade integral. Falha mantém geração antiga ativa; nenhuma carga parcial fica visível.
5. Em uma transação de Consolidação, obter `FOR UPDATE` do controle, conferir runId e geração anterior, ativar geração validada e gravar manifesto `SWITCHED`. Remover manutenção no mesmo commit.
6. Retomar consumo. Evento cujo lançamento está no snapshot é no-op pela identidade; lançamento posterior ao snapshot é inserido na nova geração e incrementa a projeção. Uma mensagem confirmada pelo broker, mas ainda não processada, permanece segura.
7. Monitorar retomada e metas. Executar nova reconciliação de snapshot para encerrar o incidente. A API consulta geração ativa e dados em uma única consulta/snapshot para não misturar gerações.

A geração anterior fica somente leitura por 7 dias e é excluída apenas quando inativa e sem execução dependente. Nunca apagar identidades da geração ativa. Antes da troca, abortar permite voltar ao consumo da geração antiga. Depois da troca e retomada, reversão exige outra reconstrução; reativar geração antiga perderia efeitos novos já confirmados.

Se ocorrer crash antes da troca, a geração antiga permanece ativa; a lease de manutenção expira, permitindo retomada do Worker e nova aquisição pelo Recovery. Se ocorrer depois do commit, geração e liberação da manutenção refletem a troca completa. A retomada do comando é iniciada pelo operador. O restante deste procedimento, incluindo manifesto operacional e retenção de artefatos, representa a especificação-alvo; não foi integralmente demonstrado no ensaio T10. Planejar espaço para duas gerações e o snapshot.

## DLQ, Outbox suspensa e expiração

Inspecionar metadados sem expor payload em logs; corrigir causa e validar contrato. Liberar Outbox suspensa preservando eventId. Redrive preserva identidades e conteúdo financeiro, registra operador/runId e usa taxa limitada. Mensagem com fato inválido não deve ser manualmente alterada para mascarar divergência: recuperar o evento a partir da fonte e preservar evidência do incidente.

SQS possui retenção finita: após expiração, publicação marcada não garante consumo. Alarmes e reconciliação diária antecipam a situação; reconstrução cobre dados ausentes sem exigir evento retido. RabbitMQ local também pode perder dados por remoção do volume; a fonte financeira é a recuperação, desde que seu volume esteja preservado.

## Restore da fonte de Lançamentos

RDS Multi-AZ trata disponibilidade; PITR e snapshots tratam recuperação. Restore da fonte não reutiliza automaticamente a projeção nem a fila existentes, pois elas podem conter fatos posteriores ao ponto recuperado. O runbook obrigatório é:

1. Abrir incidente e registrar `runId`, motivo, último ponto restaurável e RPO/RTO estimados. No ALB, bloquear `POST /transactions` com 503 e `Retry-After`; interromper API de escrita e publicador, aguardar transações e leases em curso e revogar conexão de escrita da credencial antiga. Clientes devem repetir o mesmo comando com a mesma chave somente após a reabertura.
2. Pausar consumidores antes de qualquer troca. Isolar fila principal, retries e DLQ da geração antiga: remover acesso dos Workers e proibir redrive. Preservar métricas e mensagens como evidência até o encerramento. Mensagem antiga nunca é aplicada automaticamente à fonte restaurada.
3. Restaurar Lançamentos em instância isolada para o ponto escolhido. Validar migrations, constraints, chaves de idempotência, fatos, Outbox e contagens; registrar o instante efetivamente recuperado. Não copiar linhas seletivas da instância antiga sem procedimento corretivo específico e auditado.
4. Criar geração física nova do transporte conforme ADR-010 e configurar publicador e consumidor com o destino novo. Manter ambos parados. Restaurar ou recriar Consolidação e executar reconstrução integral a partir de um único snapshot da fonte restaurada, incluindo identidades e totais.
5. Validar hashes, somas, contagens e permissões. Trocar endpoint/segredo de Lançamentos; iniciar consumidor no destino novo, iniciar publicador e confirmar que a Outbox pendente publica com as mesmas identidades. Duplicatas de fatos incluídos no snapshot são no-op na geração ativa.
6. Reabrir a API de escrita somente após readiness, smoke test autenticado e reconciliação válida. Monitorar Outbox, fila nova, duplicatas e divergência. Encerrar o incidente com RPO/RTO observados e decisão auditada sobre retenção e posterior remoção da geração antiga.

Fatos confirmados depois do ponto restaurado podem ter sido perdidos dentro do RPO. Identificá-los por trilhas externas e pela instância antiga, quando íntegra, mas não reinseri-los automaticamente: cada recuperação exige validação de negócio e preservação da chave de idempotência. A fila antiga não é fonte de verdade e não corrige essa perda. No ambiente local, o ensaio usa novo conjunto de filas ou virtual host RabbitMQ e o mesmo bloqueio de escrita.

Restore exclusivo de Consolidação mantém Lançamentos e publicação ativos: restaurar em instância isolada, ativar manutenção, reconstruir uma geração nova a partir da fonte, validar e trocar conforme o procedimento de rebuild. Não promover um snapshot antigo da projeção como estado atual.

Ensaio mensal de restore e trimestral de falha de zona. Falha regional não tem DR automático nesta referência; recuperação inter-região é evolução condicionada a novo objetivo e orçamento.

## Gatilhos de evolução por crescimento

Abrir revisão arquitetural quando qualquer condição ocorrer: reconciliação diária acima de 5 minutos em três execuções consecutivas; restore ou rebuild acima de 75% do RTO; projeção, identidades ou Outbox acima de 70% do armazenamento; espaço livre insuficiente para snapshot, duas gerações e 20% de margem; mais de 10 milhões de fatos; ou projeção de violação do RTO nos 90 dias seguintes. A revisão deve escolher, com medição, entre particionamento, snapshot incremental verificável, arquivamento que preserve identidades, aumento de classe/armazenamento ou rebuild paralelo. Até aprovação de nova ADR, o procedimento integral continua normativo e a capacidade deve ser ampliada antes de ultrapassar o limite.
