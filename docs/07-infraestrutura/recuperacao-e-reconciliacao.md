# Procedimentos de recuperação e reconciliação

**Estado: DOCUMENTAÇÃO APROVADA.** Especificação operacional a implementar e ensaiar; nenhum procedimento foi executado nesta branch.

## Autoridade, segurança e registro

A ferramenta administrativa futura será executada fora das APIs públicas por operador autenticado. Usa leitura da base de Lançamentos e escrita restrita na base de Consolidação; credenciais próprias, temporárias na AWS, sem conceder acesso cruzado aos serviços de negócio. Cada execução registra runId, operador, motivo, horários, snapshot, gerações, contagens, somas, resultado e erros sanitizados. Reconciliação e rebuild da projeção não interrompem o registro de lançamentos. Restore da própria fonte é exceção: exige indisponibilidade controlada de escrita para definir um corte seguro.

## Controle persistente

`projection_control` possui uma única linha: `active_generation_id`, `maintenance`, `maintenance_run_id`, `maintenance_started_at`. Todo consumidor inicia transação, obtém `FOR SHARE` nessa linha e verifica `maintenance` antes de qualquer efeito. A trava é mantida até commit. O operador usa `FOR UPDATE` para ativar manutenção; aguarda transações em andamento e impede novas alterações. Workers que encontrem manutenção não aplicam nem confirmam mensagens; pausam recebimentos. Entregas já em voo são liberadas para reentrega pelo adaptador sem contabilizar erro financeiro.

O estado de manutenção sobrevive a reinícios e não expira automaticamente. Timeout de lock é 30 s; não obtê-lo aborta a tentativa e exige investigação. Um runId é proprietário da operação; outra execução não pode sobrescrever uma manutenção ativa.

## Reconciliação consistente

1. Ativar manutenção e aguardar o escoamento das transações de consumo pelo protocolo acima. Lançamentos e publicador continuam.
2. Abrir transação somente leitura `REPEATABLE READ` em Lançamentos. Exportar todos os fatos visíveis, incluindo transactionId, creationEventId, data, tipo e valor, para artefato temporário criptografado, acessível apenas à operação. Registrar contagem e hash do conjunto ordenado; fechar a transação após a exportação.
3. Ler geração ativa, totais e `ProcessedTransaction` com snapshot consistente em Consolidação, que permanece estável por manutenção.
4. Verificar integridade interna: totais diários devem equivaler às somas e contagens do próprio registro de identidades. Comparar cada identidade processada com o fato de origem; dado divergente, duplicidade ou identidade sem origem é corrupção a investigar.
5. Fatos presentes no snapshot de origem e ausentes na projeção são pendências de integração, não automaticamente corrupção. Reportar idade, Outbox, fila e DLQ. A ausência no broker não prova perda; se persistir além da meta e não houver progresso, executar reconstrução.
6. Se tudo estiver correto, registrar a comparação como válida **para o snapshot**, remover manutenção com verificação do runId e retomar consumo. Nunca afirmar completude em tempo real a partir dessa verificação.

A pausa anterior ao snapshot impede que a comparação confunda efeitos processados depois do corte com inconsistência. Novos lançamentos após o snapshot ficam fora da comparação e serão integrados normalmente. A operação diária deve durar menos de 5 min para o conjunto de referência de 1 milhão de fatos; se exceder, alertar e revisar dimensionamento. Projeção continua consultável e marcada como em reconstrução/manutenção.

## Reconstrução integral por geração

1. Iniciar manutenção pelo protocolo anterior. Se já ativa pelo mesmo runId após crash, verificar registros e retomar de etapa segura; outro runId exige encerramento auditado da operação anterior.
2. Produzir snapshot consistente da fonte conforme reconciliação. Persistir manifesto e estado `SNAPSHOT_READY`; se o snapshot ficar incompleto, descartá-lo e obter outro mantendo manutenção.
3. Criar `generation_id` nova em estado `BUILDING`. Carregar uma identidade por transactionId e creationEventId e agregar totais por businessDate. Não emitir eventos novos nem alterar Lançamentos.
4. Validar unicidade, hashes, precisão, totais e contagens contra o mesmo snapshot. Estado `VALIDATED` somente após igualdade integral. Falha mantém geração antiga ativa; nenhuma carga parcial fica visível.
5. Em uma transação de Consolidação, obter `FOR UPDATE` do controle, conferir runId e geração anterior, ativar geração validada e gravar manifesto `SWITCHED`. Remover manutenção no mesmo commit.
6. Retomar consumo. Evento cujo lançamento está no snapshot é no-op pela identidade; lançamento posterior ao snapshot é inserido na nova geração e incrementa a projeção. Uma mensagem confirmada pelo broker, mas ainda não processada, permanece segura.
7. Monitorar retomada e metas. Executar nova reconciliação de snapshot para encerrar o incidente. A API consulta geração ativa e dados em uma única consulta/snapshot para não misturar gerações.

A geração anterior fica somente leitura por 7 dias e é excluída apenas quando inativa e sem execução dependente. Nunca apagar identidades da geração ativa. Antes da troca, abortar permite voltar ao consumo da geração antiga. Depois da troca e retomada, reversão exige outra reconstrução; reativar geração antiga perderia efeitos novos já confirmados.

Se ocorrer crash antes da troca, a manutenção permanece e a API mostra projeção antiga. Se ocorrer depois do commit, geração e manutenção refletem a troca completa. O operador identifica o estado pelo manifesto; não repete uma troca às cegas. Planejar espaço para duas gerações e o snapshot; remover artefato temporário até 24 h após encerramento.

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
