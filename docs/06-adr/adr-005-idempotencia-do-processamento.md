# ADR-005 — Idempotência vinculada à identidade financeira

- **Direcionadores:** RNF-006, ASR-004; recuperação após expiração de mensagens e reconstrução.

## Entrada HTTP

`Idempotency-Key` obrigatório, UUID, com unicidade `(issuer, client_id, key)`. A identidade vem do token validado. O registro e o lançamento são persistidos na mesma transação da Outbox. Mesma chave e mesmo hash reproduzem o status 201 e o corpo lógico original; hash diferente retorna 409. Ausência/formato inválido retorna 400.

Canonicalizar o comando validado: tipo em representação normativa, valor decimal com duas casas, `occurredAt` normalizado para UTC, descrição com distinção definida no contrato HTTP e ordem fixa dos campos. SHA-256 desse comando, sem timestamps gerados pelo servidor. A canonicalização recebe versão persistida; alterações futuras preservam comparação de registros antigos.

Não há expiração nem reutilização automática de chaves neste escopo. Reter chave, hash, versão, identidade, transactionId e resposta mínima enquanto existir o fato financeiro. O custo é armazenamento crescente, aceito para impedir que retry tardio crie outro lançamento. Limpeza futura exige nova decisão e contrato explícito.

Concorrência: `INSERT ... ON CONFLICT DO NOTHING` da chave dentro da transação; se já existe, ler a operação vencedora e comparar. Em READ COMMITTED, a leitura ocorre em novo comando após resolução do conflito. Se houver timeout/deadlock, desfazer a transação e responder erro retentável; jamais persistir lançamento sem chave. A resposta inclui `Idempotent-Replayed: true` no replay.

## Consumo

`ProcessedTransaction` substitui o controle limitado a `ProcessedEvent`. Chave primária `(generation_id, transaction_id)` e unicidade adicional `(generation_id, event_id)`. Armazena o fato mínimo: data, tipo, valor, eventId, hash financeiro e instante de processamento.

Na mesma transação: obter trava compartilhada da linha de controle, verificar manutenção, ler geração ativa, inserir identidade financeira e aplicar UPSERT relativo dos totais. Duplicata com mesmo fato é no-op; mesma identidade com conteúdo divergente é erro definitivo, alerta e DLQ. Confirmar a mensagem somente após commit, inclusive no caminho de duplicata.

Não remover deduplicação da geração ativa por prazo. Eventos de versões distintas do mesmo lançamento não podem gerar um segundo efeito. Novos eventIds não legitimam repetir um transactionId. A reconstrução cria uma geração com o mesmo conjunto de identidades financeiras da fonte, conforme o procedimento de recuperação.

## Consequências e aceite

Aceita-se armazenamento adicional para preservar integridade. Não há promessa de exactly-once do transporte. Há efeito financeiro único por lançamento e geração, sujeito à execução correta das transações. Testar duplicatas tardias, concorrência, identidade conflitante, troca de versão, reconstrução e crash após commit.
