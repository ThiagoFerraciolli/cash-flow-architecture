# Matriz de Conformidade da Avaliação

## Objetivo

Esta matriz consolida os critérios dos desafios de **Arquiteto de Soluções** e **Arquiteto de Software** e mostra o estado real da entrega.

Os estados utilizados são:

- **DOCUMENTADO** — decisão, desenho ou especificação já existe na documentação;
- **PENDENTE DE IMPLEMENTAÇÃO** — arquitetura definida, mas depende do código ou execução de testes;
- **PENDENTE DE VALIDAÇÃO** — informação não fornecida pelo desafio e que não deve ser inventada;
- **ATENDIDO** — requisito já comprovável no repositório.

> Esta matriz não marca como concluído aquilo que ainda depende da implementação. A documentação arquitetural pode estar fechada para início do desenvolvimento mesmo existindo itens de implementação e evidência pendentes.

| Critério | Perspectiva | Origem | Artefato/Evidência | Estado atual |
|---|---|---|---|---|
| Controle de lançamentos | Ambos | Requisito de negócio | RF-001/002/003 + SDD + implementação C# + testes | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Consolidado diário | Ambos | Requisito de negócio | RF-004/005/006 + DAS + SDD + implementação C# + testes | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Domínios funcionais e capacidades de negócio | Soluções | Obrigatório | `docs/03-arquitetura-solucao/capacidades-e-dominios.md` | DOCUMENTADO |
| Refinamento de requisitos funcionais e não funcionais | Soluções | Obrigatório | RF/RNF, premissas, questões abertas, ASRs e rastreabilidade | DOCUMENTADO |
| Arquitetura-alvo completa | Soluções | Obrigatório | DAS, C4, visões arquiteturais e arquitetura AWS | DOCUMENTADO |
| Justificativa de ferramentas, tecnologias e arquitetura | Soluções | Obrigatório | ADR-001 a ADR-007 | DOCUMENTADO |
| Linguagem dominada | Soluções | Obrigatório | C#/.NET definido no SDD e compartilhado com desafio de Software | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Desenho da solução | Software | Obrigatório | SDD + C4 de componentes + diagramas dinâmicos | DOCUMENTADO |
| Implementação em C# | Software | Obrigatório | `src/` + compilação executável | PENDENTE DE IMPLEMENTAÇÃO |
| Testes | Ambos | Obrigatório | estratégia de testes + futuros testes automatizados/evidências | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| README com funcionamento e execução local | Ambos | Obrigatório | README final e comandos reproduzíveis | PENDENTE DE IMPLEMENTAÇÃO |
| Repositório público | Ambos | Obrigatório | GitHub público | ATENDIDO |
| Documentação no repositório | Ambos | Obrigatório | `docs/` | ATENDIDO |
| Independência da consolidação | Ambos | RNF explícito | RNF-001, ASR-001/008, ADR-001/002/004 e teste de resiliência | DOCUMENTADO / PENDENTE DE VALIDAÇÃO POR TESTE |
| 50 req/s no consolidado | Ambos | RNF explícito | RNF-002, ASR-006 e estratégia de carga | DOCUMENTADO / PENDENTE DE VALIDAÇÃO POR TESTE |
| No máximo 5% de perda de requisições | Ambos | RNF explícito | RNF-003 e estratégia de carga | DOCUMENTADO / PENDENTE DE VALIDAÇÃO POR TESTE |
| Arquitetura de transição | Soluções | Diferencial | `arquitetura-transicao-hipotetica.md` | DOCUMENTADO como cenário hipotético |
| Estimativa de custos de infraestrutura/licenças | Soluções | Diferencial | `docs/07-infraestrutura/estimativa-custos.md` | DOCUMENTADO com premissas explícitas |
| Monitoramento e observabilidade | Soluções | Diferencial | visão de segurança/resiliência/observabilidade + AWS | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Segurança no consumo/integração | Soluções | Diferencial | visão de segurança + DAS + arquitetura AWS | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Escalabilidade | Ambos | Objetivo técnico | ASRs, DAS, ADR-001/006 e arquitetura AWS | DOCUMENTADO / PENDENTE DE VALIDAÇÃO POR TESTE |
| Resiliência e recuperação | Ambos | Objetivo técnico | ASRs, ADR-002/004/005 e diagramas dinâmicos | DOCUMENTADO / PENDENTE DE VALIDAÇÃO POR TESTE |
| Segurança geral | Ambos | Objetivo técnico | DAS + visão de segurança + arquitetura AWS | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Padrões arquiteturais e consequências | Ambos | Objetivo técnico | ADRs + SDD | DOCUMENTADO |
| Integração, protocolos e formatos | Ambos | Objetivo técnico | visão de integração + ADR-002/004/005 + SDD | DOCUMENTADO |
| Métricas de confiabilidade, integridade e disponibilidade | Software | Papel/objetivo | RNFs + SDD + visão de observabilidade | DOCUMENTADO; metas não fornecidas permanecem PENDENTES DE VALIDAÇÃO |
| C4 — Contexto | Ambos | Documentação adotada | `docs/04-views/C4.md` | DOCUMENTADO |
| C4 — Contêineres | Ambos | Documentação adotada | `docs/04-views/C4.md` | DOCUMENTADO |
| C4 — Componentes | Software | Documentação adotada | `docs/05-arquitetura-software/C4-Component.md` | DOCUMENTADO |
| Diagramas dinâmicos | Ambos | Documentação adotada | `docs/04-views/Dynamic-Views.md` | DOCUMENTADO |
| Visão de dados | Ambos | Documentação adotada | `docs/04-views/Data-View.md` | DOCUMENTADO |
| Visão de integração | Ambos | Documentação adotada | `docs/04-views/Integration-View.md` | DOCUMENTADO |
| Visão de segurança, resiliência e observabilidade | Ambos | Documentação adotada | `docs/04-views/Security-Resilience-Observability.md` | DOCUMENTADO |
| Visão de infraestrutura | Soluções | Documentação adotada | `docs/07-infraestrutura/arquitetura-aws.md` | DOCUMENTADO |
| DAS | Soluções | Documentação adotada | `docs/03-arquitetura-solucao/DAS.md` | DOCUMENTADO |
| SDD | Software | Documentação adotada | `docs/05-arquitetura-software/SDD.md` | DOCUMENTADO |
| ADRs | Ambos | Documentação adotada | ADR-001 a ADR-007 | DOCUMENTADO |
| SOLID e boas práticas | Software | Bem-vindo pelo desafio | SDD + futura implementação | DOCUMENTADO / PENDENTE DE IMPLEMENTAÇÃO |
| Evoluções futuras | Ambos | Observação do desafio | DAS/SDD e questões em aberto | DOCUMENTADO sem transformar evolução em requisito atual |

## Questões que permanecem abertas sem bloquear o desenvolvimento

O desafio não informa SLA, RTO, RPO, volume diário de lançamentos, retenção, crescimento, região produtiva, requisitos regulatórios específicos ou a semântica exata dos 50 req/s e dos 5% de perda. Esses pontos permanecem registrados em `questoes-em-aberto.md`.

Eles **não autorizam a criação de requisitos fictícios**. A arquitetura define mecanismos compatíveis com posterior refinamento dessas metas.

## Regra para início da implementação

A implementação somente pode começar quando:

1. todos os itens arquiteturais obrigatórios estiverem documentados;
2. não existirem decisões arquiteturais essenciais marcadas como “a definir” que bloqueiem o código;
3. DAS, SDD, ADRs, diagramas e matrizes estiverem coerentes entre si;
4. custos e diferenciais estiverem documentados com suas premissas;
5. o repositório inteiro tiver passado por revisão transversal;
6. a documentação tiver sido validada pelo responsável pela entrega.

Itens explicitamente dependentes de código, execução ou medição permanecem como **PENDENTE DE IMPLEMENTAÇÃO/VALIDAÇÃO POR TESTE** até existirem evidências reais.