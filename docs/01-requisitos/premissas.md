# Premissas e escolhas aprovadas de escopo

| ID | Premissa/decisão aprovada | Consequência |
|---|---|---|
| PRE-001 | Fato confirmado deve permanecer recuperável nas falhas de integração | Commit local, backup e RPO de desastre explícito |
| PRE-002 | Lançamentos são fonte de verdade; consolidado é derivado | Reconstrução por snapshot da fonte |
| PRE-003 | Consistência eventual aceita para a consulta | Completude desconhecida na API; metas de convergência |
| PRE-004 | Retry e reprocessamento não adicionam efeito financeiro | Identidade persistente, sem expiração automática |
| PRE-005 | Lançamentos imutáveis, sem edição/estorno | Somatórios comutativos e auditoria |
| PRE-006 | 5% não autoriza perder fatos financeiros | Métrica de erro HTTP separada de integridade |
| PRE-007 | Um comerciante/caixa lógico e BRL | Sem tenant/conta/moeda no particionamento |
| PRE-008 | Dia por America/Sao_Paulo, sem fechamento | Derivar BusinessDate uma vez; retroativos aceitos |
| PRE-009 | Ausência de projeção retorna zero projetado | Não afirmar ausência de movimento; completeness UNKNOWN |
| PRE-010 | Defaults operacionais definidos pela arquitetura | Fonte única na tabela de parâmetros; mudanças exigem testes |
| PRE-011 | Pico de 50 req/s exercitado em GET por data | Ensaio principal de consulta e complementar de processamento |
| PRE-012 | Autenticação de clientes técnicos | Keycloak local, Cognito AWS e permissões explícitas |
| PRE-013 | Referência AWS us-east-1 em duas zonas | Dados sintéticos até validação de residência para cliente real |
| PRE-014 | Execução local usa RabbitMQ; SQS Standard é a decisão de transporte da arquitetura-alvo AWS | Somente o adaptador RabbitMQ existe na entrega; SQS exige implementação e homologação antes de adoção AWS |

Limites monetários, de data, retenção e metas estão em [parâmetros operacionais](../07-infraestrutura/parametros-operacionais.md). Não há pendência de aprovação dessas escolhas para o desafio. Evolução de negócio altera premissas por nova decisão e atualização da rastreabilidade.
