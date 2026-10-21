# Premissas

## Objetivo

Premissas são decisões temporárias necessárias para viabilizar o MVP quando o desafio não fornece informação suficiente. Elas não devem ser confundidas com requisitos confirmados de negócio.

| ID | Premissa | Motivo | Validação futura |
|---|---|---|---|
| PRE-001 | Um lançamento confirmado não deve ser permanentemente perdido por falha entre banco e mensageria. | Natureza financeira do dado e necessidade de confiabilidade. | Validar RPO formal e estratégia de DR. |
| PRE-002 | Lançamentos são a fonte primária da informação financeira; o consolidado é uma projeção derivada. | Permite desacoplamento e reconstrução do consolidado. | Validar com negócio. |
| PRE-003 | O consolidado pode ser eventualmente consistente. | Necessário para manter lançamento independente da consolidação. | Validar janela máxima aceitável de defasagem. |
| PRE-004 | Reentrega de eventos é esperada e o consumidor deve ser idempotente. | Modelo de entrega at-least-once. | Confirmar parâmetros operacionais do broker. |
| PRE-005 | Lançamentos são imutáveis no MVP. | Preservar auditabilidade e evitar regras de edição não fornecidas. | Definir política de estorno/correção. |
| PRE-006 | O MVP terá autenticação simples baseada em token para demonstrar proteção das APIs. | O desafio não fornece IdP corporativo. | Produção deve integrar com mecanismo corporativo OAuth2/OIDC. |
| PRE-007 | AWS será utilizada como cloud de referência para a arquitetura-alvo. | Permite detalhar uma arquitetura produtiva concreta sem projetar três soluções completas. | Validar cloud corporativa do cliente. |
| PRE-008 | Azure e GCP serão tratados como alternativas de portabilidade por capacidades, sem assumir equivalência semântica entre serviços. | Evitar acoplamento conceitual e comparações incorretas. | Validar conforme estratégia cloud. |
| PRE-009 | PostgreSQL será usado como banco relacional de referência. | Transações ACID, integridade e adequação aos dados estruturados do domínio. | Confirmar padrões tecnológicos corporativos. |
| PRE-010 | Kafka será usado para integração assíncrona entre lançamento e consolidação. | Durabilidade, retenção/replay, consumer offsets, particionamento e escalabilidade. | Validar volume, ordenação, operação e padrões corporativos. |

## Regra de governança

Premissas devem ser removidas, confirmadas como requisitos ou atualizadas quando a informação correspondente for obtida. Decisões arquiteturais significativas derivadas dessas premissas devem ser registradas em ADR.