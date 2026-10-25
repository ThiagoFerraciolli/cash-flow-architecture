# Contratos HTTP

Este documento descreve os contratos HTTP **implementados** pelas APIs ASP.NET Core da entrega local. Os exemplos representam o comportamento suportado pelo código; a cobertura efetivamente exercitada é registrada separadamente em `docs/08-testes`. A entrega não publica especificação OpenAPI/Swagger; isso não é requisito obrigatório do desafio e permanece possível evolução de documentação de API.

## Convenções

JSON UTF-8, datas ISO 8601, instantes com offset na entrada de lançamentos e instantes UTC nas respostas. Valores monetários são strings decimais com exatamente duas casas. As rotas de negócio exigem bearer token e autorização por permissões técnicas conforme o [ADR-011](../06-adr/adr-011-autenticacao-e-autorizacao-das-apis.md). Base de escopo: comerciante único, BRL, calendário e limites dos [parâmetros operacionais](../07-infraestrutura/parametros-operacionais.md).

`X-Correlation-Id` UUID é opcional, gerado novamente se ausente ou inválido e retornado nas respostas. A instrumentação OpenTelemetry/ASP.NET Core trata o contexto distribuído W3C; `X-Correlation-Id` não participa da autenticação. `Cache-Control: no-store` é aplicado às respostas das APIs locais.

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

`GET /consolidations?from=&to=&pageSize=&cursor=`: intervalo inclusivo, paginação por data, uma linha por dia inclusive zeros; generationId e estado no envelope. Cursor inclui geração; se ela mudou, retorna 409 `projection-generation-changed` e orienta reinício da paginação. Não há promessa de snapshot estável dos valores entre páginas durante consumo normal.

## Erros e proteção

Erros produzidos explicitamente pelas APIs usam Problem Details (`application/problem+json`) com status, título/detalhe sanitizado e, quando processados pelo middleware de Problem Details, `correlationId`. Os códigos implementados pelas rotas e handlers incluem: 400 validação; 401 token inválido/ausente; 403 permissão insuficiente; 404 lançamento inexistente; 409 conflito de idempotência/geração; 413 corpo de Transactions excedido; 503 persistência indisponível; 500 erro inesperado.

**Rate limiting HTTP/429 não está implementado nesta entrega local.** Proteção de borda/rate limiting pertence à arquitetura de produção e não deve ser inferida do contrato local.

As APIs validam JWT usando o middleware JwtBearer e configuração de Authority/JWKS; não fazem chamada síncrona à Consolidação ou ao broker para atender uma requisição de Transactions. Falha de validação de token nega acesso. `/health/live`, `/health/ready` e `/metrics` são endpoints operacionais expostos no host local para demonstração; em produção devem ficar em superfície administrativa/privada e não ser tratados como endpoints públicos de negócio.

## Estado de validação

Os contratos acima descrevem o comportamento implementado. A existência de uma regra neste documento não significa que cada combinação tenha sido exercitada operacionalmente. Testes automatizados e ensaios executados estão registrados separadamente em [validação local final](../08-testes/validacao-local-final.md) e [rastreabilidade da entrega](../08-testes/production-grade-local-traceability.md).