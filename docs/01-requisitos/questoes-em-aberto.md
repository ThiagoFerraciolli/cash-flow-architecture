# Questões em Aberto e Lacunas de Requisitos

## Objetivo

O desafio não especifica todas as regras de negócio e atributos de qualidade necessários para uma operação financeira real. Este documento torna essas lacunas explícitas para evitar que hipóteses sejam apresentadas como requisitos confirmados.

## Questões funcionais

| ID | Questão | Impacto principal |
|---|---|---|
| QRF-001 | É permitido registrar débito superior ao saldo disponível? | Regra de validação, concorrência e possível necessidade de saldo consistente. |
| QRF-002 | Existe operação de estorno? Qual seu fluxo e autorização? | Modelo de domínio, auditoria e processamento. |
| QRF-003 | É permitido lançamento retroativo ou futuro? | Consolidação, reprocessamento e regras de fechamento. |
| QRF-004 | Qual timezone determina o dia financeiro? | Agrupamento temporal e consolidação diária. |
| QRF-005 | Existe horário de fechamento do dia financeiro? | Processamento e definição de freshness. |
| QRF-006 | Existem limites mínimos/máximos de valor? | Validação e segurança. |
| QRF-007 | Quais perfis de usuário e permissões são necessários? | Modelo de autorização. |
| QRF-008 | Como um lançamento incorreto deve ser corrigido? | Imutabilidade, estorno/compensação e auditoria. |
| QRF-009 | O sistema deverá suportar múltiplos comerciantes/tenants? | Particionamento, segurança, dados e escalabilidade. |
| QRF-010 | O consolidado precisa ser apenas diário ou também por outros períodos/dimensões? | Modelo de leitura e projeções. |
| QRF-011 | Qual moeda deve ser utilizada? Há necessidade de múltiplas moedas? | Modelo monetário, precisão e consolidação. |
| QRF-012 | Existe conceito de conta, caixa ou outra unidade à qual o lançamento pertence? | Modelo de domínio, chave de agregação e regras de saldo. |

## Questões não funcionais

| ID | Questão | Impacto arquitetural |
|---|---|---|
| QRNF-001 | Qual SLA/SLO esperado para as capacidades de lançamento e consolidação? | HA, redundância, operação e custo. |
| QRNF-002 | Qual RTO esperado para lançamento e consolidação? | Failover, automação de recuperação e DR. |
| QRNF-003 | Qual RPO aceitável para lançamentos e dados necessários à consolidação? | Replicação, backup, PITR e DR. |
| QRNF-004 | Qual latência P95/P99 aceitável para as APIs? | Sizing, persistência, cache e observabilidade. |
| QRNF-005 | Qual pico de requisições de registro de lançamentos? | Capacidade e escalabilidade do caminho de escrita. |
| QRNF-006 | Qual volume diário/anual e crescimento esperado? | Particionamento, storage, índices e custos. |
| QRNF-007 | Qual período de retenção dos dados financeiros e logs de auditoria? | Storage, arquivamento, compliance e custo. |
| QRNF-008 | Há exigência de disaster recovery em outra região? | Topologia multi-region e custo. |
| QRNF-009 | Quais normas regulatórias/compliance devem ser atendidas? | Segurança, retenção, auditoria e controles. |
| QRNF-010 | Existem requisitos específicos de LGPD e localização de dados? | Modelo de dados, acesso, retenção e infraestrutura. |
| QRNF-011 | Existe Identity Provider corporativo e quais mecanismos de autenticação/autorização são exigidos? | Segurança de usuários e serviços. |
| QRNF-012 | Qual janela máxima aceitável de defasagem do consolidado? | Capacidade de recuperação, alertas e dimensionamento do processamento. |
| QRNF-013 | O que exatamente significa “no máximo 5% de perda de requisições” no critério do desafio: timeout, erro HTTP, descarte, indisponibilidade ou outro indicador? | Semântica da métrica e critérios de aceite. |
| QRNF-014 | Quais são os requisitos de criptografia, rotação de chaves e gestão de segredos? | Gestão de chaves/segredos e políticas operacionais. |
| QRNF-015 | A carga de 50 req/s refere-se a qual operação do consolidado: consulta, processamento/atualização ou combinação de operações? | Cenário correto de teste de carga e dimensionamento. |
| QRNF-016 | Há requisitos específicos para autenticação serviço-a-serviço, como mTLS, client credentials ou identidade de workload? | Segurança de integração e trust boundaries. |

## Decisões que dependem dessas respostas

Enquanto as respostas não estiverem disponíveis:
- não será criada regra que impeça saldo negativo;
- não será inventado fluxo de estorno, fechamento de caixa ou multi-tenant;
- não serão declarados SLA, RTO, RPO ou metas P95/P99 como compromissos produtivos;
- a interpretação da carga e da perda do consolidado usada nos testes do MVP será explicitamente registrada como premissa de teste;
- a arquitetura-alvo apresentará mecanismos de disponibilidade, recuperação e segurança, mas indicará quais configurações dependem dessas metas.

## RTO e RPO

RTO e RPO não serão inventados para o ambiente produtivo.

A natureza financeira sugere forte preocupação com perda de lançamentos confirmados, mas afirmar formalmente `RPO = 0` exigiria uma estratégia de infraestrutura e disaster recovery compatível. Da mesma forma, definir RTO sem requisito de negócio poderia levar a complexidade e custo desnecessários.

Esses valores devem ser definidos com stakeholders e então refletidos nos ASRs, ADRs, arquitetura de infraestrutura, estratégia de backup/restore e testes de recuperação.