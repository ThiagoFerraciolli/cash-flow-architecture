# Questões em Aberto e Lacunas de Requisitos

## Objetivo

O desafio não especifica todas as regras de negócio e atributos de qualidade necessários para uma operação financeira real. Este documento torna essas lacunas explícitas para evitar que hipóteses sejam apresentadas como requisitos confirmados.

## Questões funcionais

| ID | Questão | Impacto principal |
|---|---|---|
| QRF-001 | É permitido registrar débito superior ao saldo disponível? | Regra de validação, concorrência e possível necessidade de saldo consistente. |
| QRF-002 | Existe operação de estorno? Qual seu fluxo e autorização? | Modelo de domínio, auditoria e eventos. |
| QRF-003 | É permitido lançamento retroativo ou futuro? | Consolidação, reprocessamento e regras de fechamento. |
| QRF-004 | Qual timezone determina o dia financeiro? | Particionamento temporal e consolidação diária. |
| QRF-005 | Existe horário de fechamento do dia financeiro? | Processamento e definição de freshness. |
| QRF-006 | Existem limites mínimos/máximos de valor? | Validação e segurança. |
| QRF-007 | Quais perfis de usuário e permissões são necessários? | Modelo de autorização/RBAC. |
| QRF-008 | Lançamentos podem ser corrigidos? | Imutabilidade, estorno e auditoria. |
| QRF-009 | O sistema deverá suportar múltiplos comerciantes/tenants? | Particionamento, segurança, dados e escalabilidade. |
| QRF-010 | O consolidado precisa ser apenas diário ou também por outros períodos/dimensões? | Modelo de leitura e projeções. |

## Questões não funcionais

| ID | Questão | Impacto arquitetural |
|---|---|---|
| QRNF-001 | Qual SLA/SLO esperado por serviço? | HA, redundância, operação e custo. |
| QRNF-002 | Qual RTO do Transaction Service e do Consolidation Service? | Failover, automação de recuperação e DR. |
| QRNF-003 | Qual RPO aceitável para lançamentos, eventos e consolidados? | Replicação, backup, PITR e DR. |
| QRNF-004 | Qual latência P95/P99 aceitável para APIs? | Sizing, cache, banco e observabilidade. |
| QRNF-005 | Qual pico de requisições de registro de lançamentos? | Capacidade e escalabilidade do caminho de escrita. |
| QRNF-006 | Qual volume diário/anual e crescimento esperado? | Particionamento, storage, índices e custos. |
| QRNF-007 | Qual período de retenção dos dados financeiros e logs de auditoria? | Storage, arquivamento, compliance e custo. |
| QRNF-008 | Há exigência de disaster recovery em outra região? | Topologia multi-region e custo. |
| QRNF-009 | Quais normas regulatórias/compliance devem ser atendidas? | Segurança, retenção, auditoria e controles. |
| QRNF-010 | Existem requisitos específicos de LGPD e localização de dados? | Modelo de dados, acesso, retenção e infraestrutura. |
| QRNF-011 | Existe Identity Provider corporativo? | Autenticação, autorização e integração. |
| QRNF-012 | Qual janela máxima aceitável de defasagem do consolidado? | Consumer lag, alertas e capacidade de recuperação. |
| QRNF-013 | Como deve ser interpretada a tolerância de até 5% de perda indicada no desafio? | Semântica de erro, retry e critérios de aceite. |
| QRNF-014 | Quais são os requisitos de criptografia, rotação de chaves e gestão de segredos? | KMS, Secrets Manager e políticas operacionais. |

## RTO e RPO

RTO e RPO não serão inventados para o ambiente produtivo.

A natureza financeira sugere forte preocupação com perda de lançamentos confirmados, mas afirmar formalmente `RPO = 0` exigiria uma estratégia de infraestrutura e disaster recovery compatível. Da mesma forma, definir RTO sem requisito de negócio poderia levar a complexidade e custo desnecessários.

Esses valores devem ser definidos com stakeholders e então refletidos nos ASRs, ADRs, arquitetura de infraestrutura, estratégia de backup/restore e testes de recuperação.