# Estratégia de Testes

- **Status:** EM REVISÃO FINAL — aguardando validação humana

## Objetivo

Demonstrar requisitos funcionais e riscos arquiteturais, não apenas cobertura. Esta documentação define testes; **nenhum resultado é declarado validado antes de execução real**.

## 1. Unitários

Transactions: crédito/débito válidos, `amount <= 0`, tipo inválido, derivação determinística de `BusinessDate` e canonicalização/hash de idempotência.

Consolidation: incrementos de crédito/débito/contagem e `Balance = Credits - Debits`.

## 2. Application

- lançamento + idempotência + Outbox na mesma UoW;
- mesma chave/mesmo payload reproduz resposta sem novo lançamento;
- mesma chave/payload diferente → conflito;
- queries/paginação;
- evento novo aplica projeção; duplicata não reaplica.

## 3. Integração com PostgreSQL real

Usar Testcontainers/containers, não provider in-memory para semântica transacional.

Casos: migrations, constraints, rollback das duas unidades transacionais, `SKIP LOCKED`, UPSERT concorrente, precisão decimal, índices principais e limpeza que nunca remove Outbox pendente.

## 4. API/contrato

- POST sem `Idempotency-Key` → 400;
- POST válido → 201;
- replay válido preserva resposta lógica e sinaliza `Idempotent-Replayed`;
- chave reutilizada com payload diferente → 409;
- GET existente/inexistente;
- consolidado de data válida sem movimento → 200 com zeros;
- intervalo/paginação;
- erros em `application/problem+json`;
- health/readiness.

Contract tests validam `TransactionCreatedV1`, inclusive `schemaVersion`, decimal textual e `BusinessDate`.

## 5. Independência — cenário arquitetural obrigatório

1. subir ambiente;
2. parar Consolidation API e Worker;
3. executar novos `POST /transactions`;
4. verificar sucesso e persistência local;
5. confirmar que eventos ficam na Outbox/fila conforme estado do Publisher;
6. religar Consolidação;
7. aguardar backlog;
8. verificar convergência e ausência de dupla contabilização.

Aceite: indisponibilidade da Consolidação não impede lançamento.

## 6. Fila indisponível

1. tornar o adaptador/fila indisponível;
2. registrar lançamento;
3. confirmar lançamento + Outbox;
4. verificar tentativas/backoff sem bloquear API;
5. restaurar fila;
6. Publisher entrega pendência;
7. consolidado converge.

## 7. Concorrência e idempotência

### POST concorrente

Disparar duas requisições simultâneas com a mesma chave e payload. Aceite: exatamente um lançamento financeiro; ambas recebem semântica determinística. Repetir com payloads diferentes: um vence e o outro recebe 409.

### Publishers concorrentes

Executar ao menos duas instâncias capturando o mesmo conjunto de Outbox. Aceite: `SKIP LOCKED` distribui trabalho; eventual duplicação por crash window continua segura no consumidor.

### Consumers concorrentes

Entregar eventos distintos para a mesma `BusinessDate` em paralelo. Aceite: somas e contagem finais exatas, sem lost update.

### Redelivery

Simular commit do consumidor seguido de falha antes de `DeleteMessage`. Aceite: redelivery não altera novamente a projeção.

## 8. Retry, DLQ e redrive

Produzir mensagem que falha persistentemente. Aceite: após política configurada, mensagem chega à DLQ e métrica/alerta é observável. Corrigir causa e executar redrive; mensagem é processada uma única vez em efeito.

Os valores de `VisibilityTimeout`, `maxReceiveCount` e backoff usados no teste devem ser registrados como configuração do ensaio, não como requisito produtivo.

## 9. Reconciliação e reconstrução

Criar conjunto conhecido de lançamentos e projeção divergente. Rodar reconciliação por `BusinessDate`; ela deve detectar diferenças em contagem/créditos/débitos. Exercitar rebuild controlado a partir de `financial_transactions` e confirmar convergência.

## 10. Carga — 50 req/s e falha <= 5%

O case associa 50 req/s ao serviço de consolidado, mas não identifica inequivocamente a operação. Para evidência do MVP, testar **`GET /consolidations/{date}`** a 50 req/s, pois é a operação pública de consulta do consolidado. Essa é hipótese de ensaio documentada.

Antes da janela medida, fazer warm-up e pré-carregar a data consultada. Durante a janela, não contar respostas HTTP de sucesso como perda. Definir:

`failure_rate = requests_failed_or_without_valid_response / requests_sent`

Critério: `failure_rate <= 0.05` durante a janela estável.

Registrar também P50/P95/P99, CPU/memória e configuração do ambiente, sem inventar meta de latência. Processamento assíncrono é medido separadamente por profundidade/idade da SQS e idade do evento ao consolidar.

Executar adicionalmente um ensaio de escrita/backlog para observar comportamento dos Workers, mas não apresentá-lo como interpretação oficial do requisito de 50 req/s.

## 11. Segurança mínima

Testar validação de entrada, ausência de segredos em configuração versionada/logs, autenticação 401 e autorização 403 quando o modo de segurança estiver habilitado. Na AWS, IAM mínimo é validado por revisão/IaC quando existir implementação.

## 12. Observabilidade

Em cenários de falha, confirmar presença de correlação/traces e métricas: Outbox pendente/idade, tentativas do Publisher, profundidade/idade da fila, DLQ e falhas/tempo do consumidor.

## 13. Ferramentas

xUnit, `WebApplicationFactory`, Testcontainers/Docker e k6. Testes E2E pertencem a projeto próprio porque cruzam os dois serviços.

## 14. Evidências

Resultados futuros devem ir para `docs/08-testes/` com data, commit, ambiente, comando/script, configuração, duração, métricas e resultado. Matrizes só mudam para `VALIDADO` após evidência executada.