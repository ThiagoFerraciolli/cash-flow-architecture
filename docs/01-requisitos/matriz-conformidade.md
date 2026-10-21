# Matriz de Conformidade da Avaliação

## Objetivo

Esta matriz será utilizada até a entrega para garantir que requisitos obrigatórios das perspectivas de Solutions Architect e Software Architect possuam documentação, implementação e evidência quando aplicável.

| Item de avaliação | Perspectiva | Artefato planejado | Implementação/Evidência planejada | Status |
|---|---|---|---|---|
| Domínios e capacidades de negócio | Solutions | DAS / capacidades | Mapeamento documental | Planejado |
| Refinamento de requisitos funcionais | Solutions | RF / questões abertas | Matriz de rastreabilidade | Em elaboração |
| Refinamento de requisitos não funcionais | Solutions | RNF / ASR | Testes e evidências | Em elaboração |
| Arquitetura-alvo completa | Solutions | DAS / C4 / Deployment | Diagramas local e AWS | Planejado |
| Justificativa de tecnologias e estilo arquitetural | Solutions | ADRs | Decisões rastreáveis | Planejado |
| Escalabilidade | Solutions | DAS / ASR / infraestrutura | Load test e target architecture | Planejado |
| Alta disponibilidade e recuperação | Solutions | DAS / infraestrutura | Cenários e documentação | Planejado |
| Segurança | Solutions | visão de segurança | Auth local + target architecture | Planejado |
| Integração | Solutions | visão de integração / ADR | Kafka + contratos de eventos | Planejado |
| Observabilidade | Solutions / diferencial | visão de observabilidade | logs, métricas, tracing | Planejado |
| Estimativa de custos/licenciamento | Solutions / diferencial | infraestrutura/custos | estimativa da arquitetura-alvo | Planejado |
| Arquitetura de transição/evolução | Solutions / diferencial | evolução | roadmap e deltas local→cloud | Planejado |
| Desenho da solução | Software | SDD / C4 Component | estrutura .NET | Planejado |
| Implementação em C# | Software | SDD | `src/` | Planejado |
| SOLID e boas práticas | Software | SDD / ADR | código e testes | Planejado |
| Patterns justificados | Software | SDD / ADR | código quando necessário | Planejado |
| Testes | Ambos | estratégia de testes | unitários, integração, resiliência, carga | Planejado |
| Independência da consolidação | Ambos | RNF / ASR / ADR | teste derrubando Consolidation Service | Planejado |
| 50 req/s | Ambos | RNF / ASR | teste de carga | Planejado |
| README e execução local | Ambos | README | Docker Compose | Planejado |
| Repositório público e documentação | Ambos | repositório | GitHub | Atendido |

## Regra de conclusão

Antes da entrega, nenhum requisito mínimo poderá permanecer sem artefato ou evidência correspondente. Itens documentais devem apontar para a decisão ou diagrama pertinente; itens implementáveis devem apontar também para código e teste.