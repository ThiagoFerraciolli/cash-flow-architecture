# Estratégia de Testes

## Objetivo

Demonstrar comportamento funcional e, principalmente, os riscos arquiteturais do desafio. A suíte não deve buscar apenas cobertura de linhas.

## 1. Unitários

Transactions:
- cria Credit válido;
- cria Debit válido;
- rejeita amount <= 0;
- rejeita tipo inválido;
- mantém cálculo/estado determinístico.

Consolidation:
- crédito incrementa `TotalCredits`;
- débito incrementa `TotalDebits`;
- `Balance = Credits - Debits`;
- incrementa quantidade;
- evento repetido não altera projeção quando idempotência é aplicada.

## 2. Application tests

- `CreateTransaction` persiste lançamento e Outbox na mesma unidade de trabalho;
- mesma `Idempotency-Key` não cria segundo lançamento;
- queries respeitam filtros/paginação;
- aplicação de evento atualiza projeção e processed-event atomicamente.

## 3. Integração

Preferência: PostgreSQL real em container, evitando substituir semântica relacional por provider in-memory.

Casos:
- migrations sobem em banco vazio;
- unique constraints de idempotência;
- commit/rollback lançamento + Outbox;
- commit/rollback consolidação + ProcessedEvent;
- publisher lê mensagens pendentes;
- redelivery não duplica consolidado.

## 4. API

- POST válido retorna sucesso e identificador;
- payload inválido retorna 400;
- GET existente/inexistente;
- filtros de listagem;
- consulta de consolidado por data/período;
- health/readiness.

## 5. Teste arquitetural crítico — independência

### Objetivo

Validar RNF explícito: indisponibilidade da Consolidação não torna indisponível o controle de lançamentos.

### Procedimento reproduzível

1. subir ambiente;
2. parar API/worker de Consolidação;
3. manter PostgreSQL de Transactions disponível;
4. executar `POST /transactions`;
5. verificar resposta de sucesso;
6. verificar lançamento persistido;
7. verificar Outbox pendente/publicável;
8. religar Consolidação;
9. aguardar processamento do backlog;
10. verificar convergência do consolidado.

### Aceite

O passo 4 não depende da disponibilidade da Consolidação. Após recuperação, o evento pendente deve convergir sem dupla contabilização.

## 6. Teste de falha do broker

1. indisponibilizar Kafka;
2. registrar lançamento;
3. confirmar que lançamento + Outbox persistem;
4. restabelecer Kafka;
5. publisher entrega pendência;
6. consolidação converge.

Esse teste distingue disponibilidade do domínio de Lançamentos da disponibilidade do mecanismo assíncrono.

## 7. Carga — 50 req/s e perda <= 5%

O case informa 50 req/s para o serviço de consolidação, mas não especifica qual operação. Portanto, o teste deve declarar a hipótese em vez de transformar uma interpretação em requisito.

### Hipótese de teste

Para evidência local, aplicar 50 req/s à leitura `GET /consolidations/{date}`, por ser a operação pública de consulta do consolidado definida no refinamento. O processamento assíncrono será medido separadamente por consumer lag/backlog.

### Métricas

- requests enviados;
- responses por status;
- requests sem resposta/erro;
- taxa de perda/falha observada;
- latência P50/P95/P99 como observação, sem inventar meta;
- CPU/memória;
- consumer lag em teste separado.

### Critério

`failed_requests / total_requests <= 0.05` durante a janela definida do ensaio.

Isso é evidência do ambiente local, não certificação de capacidade produtiva.

## 8. Ferramentas

- xUnit para unitários/integração;
- `WebApplicationFactory` para API;
- Testcontainers ou containers Docker reais para dependências de integração;
- k6 para carga reproduzível.

## 9. Evidências

Resultados executados devem ser registrados em `docs/08-testes/`, contendo data, ambiente, comando, cenário e resultado. Não marcar requisito como VALIDADO antes da execução real.