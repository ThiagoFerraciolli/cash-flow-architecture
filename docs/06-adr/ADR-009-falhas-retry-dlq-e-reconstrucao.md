# ADR-009 — Tratamento de falhas, retry, DLQ, reconciliação e reconstrução

- **Status:** ACEITO PARA A ARQUITETURA DO DESAFIO — sujeito à validação humana da branch
- **Drivers:** RNF-005, RNF-007, RNF-010, ASR-003, ASR-005, ASR-009
- **Relacionados:** ADR-002, ADR-004, ADR-005, ADR-007, ADR-008

## Contexto

A entrega é at-least-once. Falhas transitórias precisam permitir nova tentativa, mensagens que não podem ser processadas precisam ser isoladas e divergências silenciosas precisam ser detectáveis. A fila não deve ser tratada como histórico financeiro nem como única estratégia de reconstrução.

## Alternativas

1. **Retry infinito para qualquer erro** — simples, mas poison messages podem permanecer indefinidamente e degradar o fluxo.
2. **Descartar após N tentativas** — evita bloqueio, porém cria perda silenciosa e viola a necessidade de recuperação.
3. **Retry controlado + DLQ/redrive + reconciliação + rebuild pela fonte de verdade** — adiciona disciplina operacional, mas cobre os modos de falha relevantes sem exigir plataforma de streaming.

## Decisão

### Publisher da Outbox

Falha de envio mantém `published_at` nulo. `attempts`, `next_attempt_at` e `last_error` registram o estado operacional. Nova tentativa usa backoff com jitter. Valores exatos de intervalo, limite e retenção são configuração do ambiente, pois SLA/SLO não foram fornecidos.

Falha depois de `SendMessage` e antes de marcar `published_at` pode republicar o mesmo `eventId`; isso é esperado e seguro pelo consumidor idempotente.

### Consolidation Worker

- falha transitória: não executar `DeleteMessage`; a mensagem volta após `VisibilityTimeout`;
- mensagem inválida ou falha persistente: a redrive policy da SQS a encaminha para a DLQ após `maxReceiveCount` configurado;
- DLQ não vazia gera sinal operacional e exige triagem;
- depois de corrigida a causa, o redrive retorna mensagens à fila principal;
- idempotência por `eventId` torna redelivery/redrive seguros.

Não são fixados números arbitrários de tentativas, visibility timeout ou retenção como requisitos produtivos. Eles devem ser medidos e configurados por ambiente.

## Reconciliação

O MVP deve possuir procedimento/teste que compare, por `BusinessDate`, contagem, total de créditos e total de débitos entre `financial_transactions` e `daily_consolidations`. Divergência gera investigação e, quando necessário, rebuild controlado.

O procedimento pode acessar os dois bancos como ferramenta operacional/E2E; isso não autoriza acesso cruzado entre os serviços no fluxo de negócio normal.

## Reconstrução

`financial_transactions` é a fonte de verdade deste escopo. A reconstrução não depende da retenção histórica da SQS.

Para um intervalo afetado, a operação deve:
1. controlar concorrência com o consumo normal para evitar dupla aplicação durante o rebuild;
2. obter os lançamentos da fonte de verdade;
3. reconstruir a projeção de forma determinística, por processo operacional dedicado ou reemissão controlada;
4. preservar/renovar identificadores de processamento conforme a estratégia escolhida, sem burlar a deduplicação acidentalmente;
5. validar o resultado por reconciliação antes de encerrar a operação.

O mecanismo físico exato do comando de rebuild fica para a implementação/runbook. O ADR fixa a propriedade arquitetural: reconstrução parte dos lançamentos e termina com reconciliação.

## Consequências

- falhas persistentes deixam de ser perda silenciosa;
- SQS + DLQ cobre recuperação operacional sem exigir Kafka;
- observabilidade precisa incluir idade/profundidade da fila, Outbox pendente e DLQ;
- testes devem exercitar falha de broker, redelivery, DLQ/redrive, concorrência e reconciliação;
- rebuild exige operação controlada e não deve rodar concorrentemente de forma ingênua com o consumidor normal.