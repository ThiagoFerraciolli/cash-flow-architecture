# Matriz de Conformidade da Avaliação

## Objetivo

Esta matriz será utilizada até a entrega para garantir que requisitos obrigatórios das perspectivas de Solutions Architect e Software Architect possuam documentação, implementação e evidência quando aplicável.

| Item de avaliação | Perspectiva | Artefato planejado | Implementação/Evidência planejada | Status |
|---|---|---|---|---|
| Domínios e capacidades de negócio | Solutions | DAS / capacidades | Mapeamento documental | Planejado |
| Refinamento de requisitos funcionais | Solutions | RF / questões abertas | Matriz de rastreabilidade | Em elaboração |
| Refinamento de requisitos não funcionais | Solutions | RNF / ASR | Testes e evidências | Em elaboração |
| ASRs | Solutions | ASR | Rastreabilidade RF/RNF → decisões | Em elaboração |
| DAS — Documento de Arquitetura da Solução | Solutions | DAS | Consolidação das visões arquiteturais | Planejado |
| Arquitetura-alvo completa | Solutions | DAS / C4 / Deployment | Diagramas local e AWS | Planejado |
| Visão de Dados | Solutions / Software | DAS / Data View / SDD | Modelo, ownership, consistência, lifecycle, backup/restore | Planejado |
| Visão de Infraestrutura | Solutions | DAS / Infrastructure View / Deployment | Docker local + arquitetura-alvo AWS | Planejado |
| Visão de Integração | Solutions | DAS / Integration View / ADR | Contratos e processamento assíncrono | Planejado |
| Visão de Segurança | Solutions | DAS / Security View | Auth local + target architecture | Planejado |
| Visão de Resiliência | Solutions / Software | DAS / SDD / ADR | cenários de falha e recuperação | Planejado |
| Visão de Observabilidade | Solutions / diferencial | DAS / Observability View | logs, métricas, tracing | Planejado |
| C4 — Context | Ambos | C4 Context | Diagrama | Planejado |
| C4 — Container | Ambos | C4 Container | Diagrama | Planejado |
| C4 — Component | Software | C4 Component | Transaction e Consolidation | Planejado |
| Diagramas dinâmicos | Ambos | Sequence diagrams | registro, processamento, falha/recuperação | Planejado |
| Diagramas de deployment | Solutions | Deployment diagrams | Local e AWS | Planejado |
| ADRs | Ambos | ADR | alternativas, decisão, consequências e trade-offs | Planejado |
| SDD — Software Design Document | Software | SDD | design interno da solução .NET | Planejado |
| Justificativa de tecnologias e estilo arquitetural | Solutions | ADRs / DAS | Decisões rastreáveis | Planejado |
| Escalabilidade | Solutions | DAS / ASR / infraestrutura | Load test e target architecture | Planejado |
| Alta disponibilidade e recuperação | Solutions | DAS / infraestrutura | Cenários e documentação | Planejado |
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
