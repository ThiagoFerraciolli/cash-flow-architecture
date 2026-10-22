# Matriz de Conformidade da Avaliação

- **Status:** EM REVISÃO FINAL — aguardando validação humana

Estados: **DOCUMENTADO** (especificado), **PENDENTE DE IMPLEMENTAÇÃO**, **PENDENTE DE EVIDÊNCIA** (exige execução), **PENDENTE DE VALIDAÇÃO DE NEGÓCIO** e **ATENDIDO** (comprovável agora).

| Critério | Perspectiva | Artefato | Estado |
|---|---|---|---|
| Controle de lançamentos | Ambos | RF + DAS/SDD | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Consolidado diário | Ambos | RF + DAS/SDD | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Domínios/capacidades | Soluções | capacidades-e-dominios | DOCUMENTADO |
| RF/RNF/ASRs | Soluções | 01-requisitos + ASR | DOCUMENTADO |
| Arquitetura-alvo | Soluções | DAS/C4/views/AWS | DOCUMENTADO |
| Justificativas/trade-offs | Soluções | ADR-001..007 | DOCUMENTADO |
| Linguagem dominada | Soluções | C#/.NET no SDD | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Desenho de software | Software | SDD + C4 Component + Dynamic Views | DOCUMENTADO |
| Implementação C# | Software | `src/` futuro | PENDENTE DE IMPLEMENTAÇÃO |
| Testes automatizados | Ambos | estratégia + código futuro | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Evidências executadas | Ambos | `docs/08-testes/` futuro | PENDENTE DE EVIDÊNCIA |
| README/índice | Ambos | `README.md` | DOCUMENTADO; comandos de execução aguardam implementação real |
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
| Resiliência/recuperação | Ambos | ADRs/Dynamic Views/testes | DOCUMENTADO / PENDENTE DE EVIDÊNCIA |
| Integração/protocolos | Ambos | Integration View/SDD | DOCUMENTADO |
| C4 Context/Container | Ambos | C4.md | DOCUMENTADO |
| C4 Component | Software | C4-Component.md | DOCUMENTADO |
| Data/Dynamic/Security Views | Ambos | docs/04-views | DOCUMENTADO |
| DAS | Soluções | DAS.md | DOCUMENTADO |
| SDD | Software | SDD.md | DOCUMENTADO |
| SOLID/padrões | Software | SDD | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |

## Pontos de negócio ainda abertos

SLA/SLO, RTO/RPO, DR, região produtiva, volume diário/crescimento, retenções produtivas, IdP/RBAC, classificação regulatória, timezone/calendário/fechamento e interpretação oficial da operação de 50 req/s. O MVP documenta premissas de teste sem inventar confirmação.

## Gate

A documentação pode ficar pronta para validação humana sem transformar itens de código/teste em atendidos. Implementação, PR e merge permanecem fora desta revisão. Evidências dependentes de execução continuam pendentes até existirem resultados reais.