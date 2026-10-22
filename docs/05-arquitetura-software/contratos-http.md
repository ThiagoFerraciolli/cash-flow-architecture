# Contratos HTTP

**Estado: DOCUMENTAÇÃO APROVADA.** Contrato a implementar em ASP.NET Core e publicar futuramente como OpenAPI. Exemplos são especificação, não respostas de uma aplicação existente.

## Convenções

JSON UTF-8, datas ISO 8601, instantes com offset e saída UTC. Valores monetários são strings decimais com exatamente duas casas. Autenticação bearer obrigatória e autorização por permissões técnicas conforme o [ADR-011](../06-adr/adr-011-autenticacao-e-autorizacao-das-apis.md). Base de escopo: comerciante único, BRL, calendário e limites dos [parâmetros operacionais](../07-infraestrutura/parametros-operacionais.md).

`X-Correlation-Id` UUID opcional, gerado se ausente/inválido; retornado em todas as respostas. `traceparent` validado e propagado quando válido. Nunca confiar em correlação para autenticar. `Cache-Control: no-store` em respostas financeiras.

## Criar lançamento

`POST /transactions`, permissão `transactions.write`, header `Idempotency-Key` UUID obrigatório.

```json
{
  "type": "CREDIT",
  "amount": "100.00",
  "occurredAt": "2026-10-22T09:00:00-03:00",
  "description": "Venda"
}
```

Campos desconhecidos de comando são rejeitados com 400 para evitar interpretação silenciosa. `description` omitida ou null são equivalentes; string vazia equivale a null; demais strings preservam espaços e Unicode, sem normalização oculta. `occurredAt` é obrigatório e não recebe relógio do servidor como default. O servidor calcula businessDate e createdAt; identidade financeira não vem do cliente.

Resposta 201 e `Location: /transactions/{id}`: id, type, amount, occurredAt UTC, businessDate, createdAt e description. O mesmo corpo lógico é reproduzido em retry idempotente, com `Idempotent-Replayed: true`. Identidade autenticada responsável é armazenada para auditoria, sem exposição obrigatória no DTO público. Sem expiração da chave neste escopo. Regras de conflito e canonicalização: [ADR-005](../06-adr/adr-005-idempotencia-do-processamento.md).

## Consultar lançamentos

`GET /transactions/{id}`, permissão `transactions.read`: 200 ou 404.

`GET /transactions?from=2026-10-01&to=2026-10-22&type=CREDIT&pageSize=50&cursor=...`: filtra por businessDate, limites inclusivos. from/to obrigatórios, tipo opcional. Ordenação `(businessDate, id)` crescente, cursor opaco vinculado aos filtros; resposta `items` e `nextCursor`. A listagem paginada representa leitura corrente, não snapshot para auditoria; inserções retroativas durante navegação podem exigir nova consulta. Recuperação usa snapshot próprio.

## Consultar consolidação

`GET /consolidations/{date}`, permissão `consolidations.read`.

```json
{
  "businessDate": "2026-10-22",
  "currency": "BRL",
  "totalCredits": "100.00",
  "totalDebits": "20.00",
  "balance": "80.00",
  "transactionCount": 2,
  "lastUpdatedAt": "2026-10-22T12:00:05Z",
  "generationId": "9b97e73a-d677-4225-aee0-8eac1f48f858",
  "consistency": "EVENTUAL",
  "projectionState": "AVAILABLE",
  "completeness": "UNKNOWN"
}
```

`projectionState`: `AVAILABLE` ou `REBUILDING` (inclui pausa de reconciliação). Não equivale à saúde do broker. `completeness` permanece `UNKNOWN`: sem coordenação síncrona, não se garante que todos os lançamentos já estejam refletidos. `lastUpdatedAt` indica último efeito aplicado, não watermark da fonte nem medida suficiente de lag.

Sem linha de projeção: 200, valores e contagem zero, lastUpdatedAt null, mesmas indicações de consistência/completude. Significa **nenhum movimento refletido nessa projeção**, nunca prova ausência de lançamentos. Banco de leitura indisponível retorna 503, nunca zero fictício. Durante reconstrução, servir geração antiga até troca atômica, com estado REBUILDING.

`GET /consolidations?from=&to=&pageSize=&cursor=`: intervalo inclusivo, paginação por data, uma linha por dia inclusive zeros; generationId e estado no envelope. Cursor inclui geração; se ela mudou, retornar 409 `projection-generation-changed` e orientar reinício da paginação. Não prometer snapshot estável de valores entre páginas durante consumo normal.

## Erros e proteção

`application/problem+json` com type, title, status, detail sanitizado e correlationId. Códigos: 400 validação; 401 token inválido/ausente; 403 permissão insuficiente; 404 lançamento inexistente; 409 conflito de idempotência/geração; 413 corpo excedido; 429 limite de taxa; 503 dependência local indisponível; 500 erro inesperado. Retentativas reutilizam a chave original e aplicam backoff. 429 inclui Retry-After.

A API de Lançamentos não consulta Consolidação, broker ou IdP remoto a cada chamada: valida JWT e usa JWKS em cache. Chave de assinatura desconhecida exige atualização controlada; falha sem chave confiável nega acesso, nunca ignora validação. Health/readiness ficam fora das rotas públicas e não expõem informações sensíveis.
