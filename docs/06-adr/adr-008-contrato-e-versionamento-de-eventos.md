# ADR-008 — Contrato canônico do evento

- **Direcionadores:** RNF-006/010/014, ASR-003/004/009.

## Contrato TransactionCreatedV1

Este é o único exemplo normativo do payload. DTOs futuros em `CashFlow.Contracts` conterão apenas contratos de integração, sem domínio compartilhado.

```json
{
  "schemaVersion": 1,
  "eventId": "b33d057a-151a-4b90-a88b-38911f3dbac7",
  "eventType": "TransactionCreated",
  "eventOccurredAt": "2026-10-22T12:00:00Z",
  "transactionId": "4608957e-b195-4370-9ae7-aa9e4b16ead3",
  "transactionType": "CREDIT",
  "amount": "100.00",
  "businessDate": "2026-10-22"
}
```

- `eventId`: UUID gerado uma vez e persistido no lançamento como `creation_event_id` e na Outbox. Republicação/redrive preservam esse valor; reconstrução recupera-o da fonte.
- `transactionId`: identidade financeira imutável, chave de efeito único por geração.
- `eventOccurredAt`: criação do evento em UTC; não substitui `occurredAt` financeiro.
- `amount`: decimal textual positivo com duas casas, ponto decimal, sem separador de milhar ou expoente; limites iguais ao contrato HTTP.
- `businessDate`: data ISO calculada e persistida por Lançamentos; consumidor não recalcula fuso.
- `transactionType`: somente `DEBIT` ou `CREDIT`; moeda única BRL é configuração do escopo.
- Metadados: `traceparent`, `tracestate` quando aplicável e `correlation-id`; não incluir tokens, descrição ou identidade pessoal no evento.

Validar campos obrigatórios, UUIDs, versão, enum, data e valor antes da transação. Hash financeiro usa transactionId, businessDate, transactionType e amount canônicos; metadados de transporte não alteram o fato. Evento divergente com identidade já conhecida é incidente, não atualização.

## Evolução

Campos opcionais adicionais são compatíveis e podem ser ignorados. Mudança incompatível cria nova versão e consumidor capaz de coexistência antes da mudança do produtor. Preservar transactionId na migração; deduplicar entre versões. O tipo v1 usa a rota definida no ADR-010; nova versão requer atualização explícita de rotas e consumidores. Versão desconhecida não é descartada: segue fluxo de erro permanente/DLQ.

Compartilhar entidades de domínio foi rejeitado por acoplamento. Um envelope amplo sem consumidor definido foi rejeitado por complexidade. O contrato plano mantém o necessário à projeção e sua verificação.
