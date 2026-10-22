# Premissas

## Objetivo

Premissas são hipóteses temporárias onde o desafio não fornece informação suficiente. Não são requisitos confirmados. Tecnologias e padrões são decididos em ADRs.

| ID | Premissa | Motivo | Validação futura |
|---|---|---|---|
| PRE-001 | Lançamento confirmado não deve ser permanentemente perdido. | Dados financeiros e confiabilidade, embora RPO não esteja definido. | Validar RPO e recuperação. |
| PRE-002 | Lançamentos são a informação financeira primária; consolidado diário é derivado. | O problema descreve fatos e relatório diário. | Validar com negócio. |
| PRE-003 | Consolidado pode ficar temporariamente defasado, mas deve convergir sem perda/duplicidade. | Lançamentos precisa sobreviver à indisponibilidade da Consolidação. | Validar janela máxima. |
| PRE-004 | Retries/reprocessamentos não podem produzir efeito financeiro adicional. | At-least-once e falhas geram repetição. | Validar idempotência. |
| PRE-005 | Lançamentos confirmados são imutáveis no MVP. | Não há edição/exclusão/estorno no enunciado. | Definir política futura. |
| PRE-006 | A tolerância de até 5% de falha/perda de requisições do consolidado não autoriza perda permanente de lançamento confirmado. | Evita interpretação perigosa do RNF de carga. | Confirmar métrica. |
| PRE-007 | MVP considera uma única moeda; multi-moeda não é modelada. | Moeda não é especificada. | Confirmar moeda/regras. |
| PRE-008 | Para a demonstração, `BusinessDate` é derivado de `OccurredAt` usando um fuso configurável único; não existe fechamento intradiário. | O desafio pede consolidado diário mas não define timezone/fechamento. Calcular uma vez evita divergência entre serviços. | Validar timezone, calendário e fechamento com negócio. |
| PRE-009 | `GET /consolidations/{date}` para data válida sem lançamentos retorna consolidado zero. | Permite representar semanticamente “sem movimento” e torna consulta/carga determinística; o enunciado não define 404 nesse caso. | Validar contrato de consulta. |
| PRE-010 | Janelas de idempotência, retenção de Outbox/ProcessedEvent, visibility timeout e limites de retry são configurações técnicas do MVP. | Valores produtivos dependem de SLA/volume/operabilidade ausentes. | Definir com SLOs e operação. |

## Governança

Cada premissa deve ser confirmada, substituída ou removida quando houver informação de negócio. Nenhuma premissa deve ser apresentada como requisito original dos desafios.