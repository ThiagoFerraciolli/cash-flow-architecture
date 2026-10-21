# Premissas

## Objetivo

Premissas são hipóteses adotadas temporariamente quando o desafio não fornece informação suficiente para concluir uma regra de negócio ou atributo de qualidade. Elas não são requisitos confirmados nem decisões arquiteturais.

Tecnologias, padrões, estilo arquitetural e serviços de infraestrutura não são definidos neste documento. Essas escolhas devem ser justificadas posteriormente por ADRs, a partir dos requisitos, ASRs, alternativas e trade-offs.

| ID | Premissa | Motivo | Validação futura |
|---|---|---|---|
| PRE-001 | Um lançamento que recebeu confirmação de sucesso não deve ser permanentemente perdido. | O desafio trata dados financeiros e exige confiabilidade, embora não defina RPO. | Validar RPO formal e estratégia de recuperação. |
| PRE-002 | Os lançamentos constituem a informação financeira primária e o consolidado diário é informação derivada desses lançamentos. | O problema descreve lançamentos e um relatório de saldo diário consolidado. | Validar com stakeholders de negócio. |
| PRE-003 | O consolidado pode apresentar defasagem temporária em relação aos lançamentos, desde que posteriormente convirja sem perda ou dupla contabilização. | O serviço de lançamentos deve permanecer disponível quando a consolidação estiver indisponível. | Validar a janela máxima aceitável de defasagem. |
| PRE-004 | Reprocessamentos e tentativas repetidas não devem alterar o resultado financeiro mais de uma vez. | Falhas e recuperação podem provocar repetição de operações ou processamento. | Validar critérios de idempotência por operação. |
| PRE-005 | Lançamentos confirmados serão tratados como imutáveis no MVP. Correções, quando necessárias, deverão ser modeladas posteriormente por regra explícita de negócio. | O desafio não define edição, exclusão ou estorno de lançamentos. | Definir política de correção/estorno. |
| PRE-006 | A tolerância de até 5% de perda citada no desafio não autoriza perda permanente de lançamentos já confirmados. | Evita interpretar uma meta de requisições do consolidado como permissão para perda financeira. | Confirmar a semântica da métrica com stakeholders. |
| PRE-007 | Para fins do MVP, será considerada uma única moeda e um único calendário financeiro, sem regra de fechamento definida. | O desafio não especifica multi-moeda, timezone ou fechamento. | Definir moeda, timezone e regras de fechamento. |

## Regra de governança

Cada premissa deverá, quando houver informação suficiente, ser confirmada como requisito, substituída por uma regra validada ou removida. Decisões arquiteturais decorrentes dessas premissas deverão ser registradas separadamente em ADR.