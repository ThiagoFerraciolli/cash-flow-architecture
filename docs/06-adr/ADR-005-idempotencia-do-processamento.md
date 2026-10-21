# ADR-005 — Idempotência no registro e no processamento

- **Status:** PROPOSTO — aguardando validação
- **Drivers:** RNF-006, ASR-004

## Contexto

Retries de cliente, falhas de rede e semântica at-least-once podem repetir uma operação. Para um fluxo financeiro, repetição não pode criar um novo efeito financeiro indevido.

## Decisão proposta

Aplicar idempotência em duas fronteiras diferentes.

### Entrada da API de lançamentos

`POST /transactions` aceitará uma chave de idempotência fornecida pelo consumidor (`Idempotency-Key`). Para uma mesma chave e mesma operação, retries devem retornar o resultado lógico já produzido em vez de criar outro lançamento.

A política de retenção das chaves é operacional e dependerá do contrato/volume; não será declarada arbitrariamente como requisito de negócio.

### Consumo no Consolidation Service

Cada evento possuirá `eventId` único. O consumidor manterá controle persistente dos eventos já aplicados.

A atualização de `daily_consolidations` e o registro de `eventId` processado ocorrerão na mesma transação local do banco da consolidação.

Fluxo conceitual:

```text
BEGIN
  if eventId already processed:
      no-op
  else:
      update daily_consolidation
      insert processed_event(eventId)
COMMIT
```

## Alternativas consideradas

- confiar apenas em retries do broker: insuficiente;
- deduplicação somente em memória: perde estado após restart e não funciona entre instâncias;
- exactly-once como promessa ponta a ponta: evitado, pois depende de limites transacionais e integrações que não abrangem todo o fluxo.

## Consequências

- retries são seguros dentro do contrato definido;
- redelivery não causa dupla contabilização;
- é necessário lifecycle/retention para registros técnicos;
- constraints únicas no banco reforçam a garantia;
- a solução permanece compatível com semântica at-least-once.