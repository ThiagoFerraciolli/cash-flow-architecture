# Matriz de Conformidade da Avaliação

- **Status:** PRONTA PARA VALIDAÇÃO HUMANA

Estados: **DOCUMENTADO** (especificado), **PENDENTE DE IMPLEMENTAÇÃO**, **PENDENTE DE EVIDÊNCIA** (exige execução), **PENDENTE DE VALIDAÇÃO DE NEGÓCIO** e **ATENDIDO** (comprovável agora).

| Critério | Perspectiva | Artefato | Estado |
|---|---|---|---|
| Controle de lançamentos | Ambos | RF + DAS/SDD | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Consolidado diário | Ambos | RF + DAS/SDD | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Domínios/capacidades | Soluções | capacidades-e-dominios | DOCUMENTADO |
| RF/RNF/ASRs | Soluções | 01-requisitos + ASR | DOCUMENTADO |
| Arquitetura-alvo | Soluções | DAS/C4/views/AWS | DOCUMENTADO |
| Justificativas/trade-offs | Soluções | ADR-001..009 | DOCUMENTADO |
| Linguagem dominada | Soluções | C#/.NET no SDD | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Desenho de software | Software | SDD + C4 Component + Dynamic Views | DOCUMENTADO |
| Implementação C# | Software | `src/` futuro | PENDENTE DE IMPLEMENTAÇÃO |
| Testes automatizados | Ambos | estratégia + código futuro | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Evidências executadas | Ambos | `docs/08-testes/` futuro | PENDENTE DE EVIDÊNCIA |
| README/índice | Ambos | `README.md` | DOCUMENTADO; comandos reais PENDENTES DE IMPLEMENTAÇÃO |
| Repositório público | Ambos | GitHub | ATENDIDO |
| Documentação versionada | Ambos | `docs/` | ATENDIDO |
| Independência da Consolidação | Ambos | ADR-001/002/004 + fault test | DOCUMENTADO / PENDENTE DE EVIDÊNCIA |
| 50 req/s | Ambos | estratégia de carga | DOCUMENTADO / PENDENTE DE EVIDÊNCIA |
| <=5% falha/perda de requests | Ambos | estratégia de carga | DOCUMENTADO / PENDENTE DE EVIDÊNCIA |
| Arquitetura de transição | Soluções diferencial | documento hipotético | DOCUMENTADO como hipótese |
| Custos/licenciamento | Soluções diferencial | estimativa-custos | DOCUMENTADO como estimativa |
| Observabilidade | Soluções diferencial | view + AWS | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Segurança | Ambos | view + AWS + SDD | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Escalabilidade | Ambos | DAS/ADR-006/AWS | DOCUMENTADO / PENDENTE DE EVIDÊNCIA |
| Resiliência/recuperação | Ambos | ADR-002/004/005/009 + Dynamic Views/testes | DOCUMENTADO / PENDENTE DE EVIDÊNCIA |
| Integração/protocolos | Ambos | Integration View/ADR-008/SDD | DOCUMENTADO |
| C4 Context/Container | Ambos | C4.md | DOCUMENTADO |
| C4 Component | Software | C4-Component.md | DOCUMENTADO |
| Data/Dynamic/Security Views | Ambos | docs/04-views | DOCUMENTADO |
| DAS | Soluções | DAS.md | DOCUMENTADO |
| SDD | Software | SDD.md | DOCUMENTADO |
| SOLID/padrões | Software | SDD | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |

## Pontos de negócio ainda abertos

SLA/SLO, RTO/RPO, DR, região produtiva, volume diário/crescimento, retenções produtivas, IdP/RBAC, classificação regulatória, timezone/calendário/fechamento e interpretação oficial da operação de 50 req/s. O MVP documenta premissas de teste sem inventar confirmação.

## Resultado da revisão comparativa

Os arquivos alternativos fornecidos pelo usuário foram comparados como propostas, não aplicados automaticamente. Foram incorporadas as partes que aumentam rastreabilidade sem criar requisitos: ADR explícito para contrato/versionamento de evento e ADR para retry/DLQ/reconciliação/rebuild. Foram rejeitados como defaults obrigatórios, por falta de driver nos desafios: Kafka/MSK, `America/Sao_Paulo`, janela fixa de idempotência, retenções fixas, aceitação irrestrita de lançamentos retroativos/futuros, escopos OAuth inventados e um IdP específico.

## Gate

A documentação passou por revisão transversal e por segunda passagem nas perspectivas de Solutions Architect e Software Architect. Está pronta para **validação humana da branch**. Itens dependentes de código ou execução continuam corretamente pendentes e não são apresentados como validados. Nenhum PR/merge decorre automaticamente deste estado.