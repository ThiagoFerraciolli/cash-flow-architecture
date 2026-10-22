# ADR-008 — Contrato e versionamento de eventos

- **Status:** ACEITO PARA A ARQUITETURA DO DESAFIO — sujeito à validação humana da branch
- **Drivers:** RNF-006, RNF-010, ASR-003, ASR-004, ASR-009
- **Relacionados:** ADR-002, ADR-004, ADR-005, ADR-007

## Contexto

Lançamentos e Consolidação evoluem de forma independente e se comunicam por evento assíncrono. O contrato precisa ser mínimo, versionável e sem ambiguidade de precisão monetária ou regra temporal, preservando a independência do broker e a correlação ponta a ponta.

## Alternativas

1. **Compartilhar entidades de domínio** — reduz mapeamento, mas acopla os bounded contexts e seus ciclos de evolução.
2. **Envelope genérico amplo** — oferece flexibilidade, porém adiciona campos e abstrações sem necessidade atual.
3. **Contrato de integração explícito e versionado** — mantém baixo acoplamento e torna compatibilidade testável.

## Decisão

Adotar `TransactionCreatedV1` como DTO de integração explícito, plano e versionado:

```json
{
  "schemaVersion": 1,
  "eventId": "uuid",
  "eventType": "TransactionCreated",
  "eventOccurredAt": "2026-10-22T12:00:00Z",
  "transactionId": "uuid",
  "transactionType": "CREDIT",
  "amount": "100.00",
  "businessDate": "2026-10-22"
}
```

Regras:
- `eventId` identifica unicamente a ocorrência publicada e é a chave de deduplicação do consumidor;
- `eventOccurredAt` é o instante UTC de criação do evento; não substitui o instante de ocorrência do lançamento;
- `transactionType` aceita `DEBIT` ou `CREDIT`;
- `amount` é serializado como string decimal para não introduzir semântica de ponto flutuante no JSON;
- `businessDate` é calculado uma única vez pelo serviço de Lançamentos a partir da regra temporal configurada e é propagado sem recálculo pela Consolidação;
- `traceparent` e `correlation-id` pertencem aos metadados da mensagem/Outbox, não ao domínio financeiro.

O contrato fica em `CashFlow.Contracts`, que contém DTOs de integração e nenhuma entidade ou lógica de domínio.

## Evolução

- mudança compatível: adicionar campo opcional e manter `schemaVersion`;
- mudança incompatível: criar nova versão de contrato e período de coexistência/migração;
- consumidores devem tolerar campos desconhecidos compatíveis;
- versão não suportada é falha permanente e segue o tratamento definido no ADR-009.

A nomenclatura física da fila não faz parte do contrato de domínio. Na arquitetura AWS vigente, o transporte é SQS Standard; Kafka não é assumido por este ADR.

## Consequências

- os dois serviços podem evoluir sem compartilhar modelo interno;
- precisão monetária e `BusinessDate` têm semântica única;
- testes de contrato devem cobrir serialização, desserialização, compatibilidade e versão não suportada;
- qualquer nova informação no evento precisa ser justificada pela necessidade do consumidor, evitando transformar o evento em réplica da entidade.