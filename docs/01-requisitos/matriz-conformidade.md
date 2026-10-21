# Matriz de Conformidade da Avaliação

## Objetivo

Esta matriz transforma o enunciado dos dois desafios em checklist de entrega. A coluna **Origem** distingue itens obrigatórios, diferenciais e objetivos/observações do desafio para evitar tanto omissões quanto escopo artificial.

| Critério | Perspectiva | Origem | Artefato/Evidência planejada | Status |
|---|---|---|---|---|
| Controle de lançamentos | Ambos | Requisito de negócio | RF + implementação C# + testes | Em elaboração |
| Consolidado diário | Ambos | Requisito de negócio | RF + implementação C# + testes | Em elaboração |
| Domínios funcionais e capacidades de negócio | Solutions | Obrigatório | mapa de capacidades + bounded contexts/domínios no DAS | Planejado |
| Refinamento de requisitos funcionais e não funcionais | Solutions | Obrigatório | RF/RNF/premissas/questões/ASRs/rastreabilidade | Em elaboração |
| Arquitetura-alvo completa | Solutions | Obrigatório | DAS + C4 + visões de dados, integração, segurança, infraestrutura e deployment | Planejado |
| Justificativa de ferramentas, tecnologias e tipo de arquitetura | Solutions | Obrigatório | ADRs com alternativas, decisão, consequências e trade-offs | Planejado |
| Linguagem dominada | Solutions | Obrigatório | C# compartilhado com a entrega de Software Architect | Planejado |
| Desenho da solução | Software | Obrigatório | SDD + C4 Component + diagramas dinâmicos | Planejado |
| Implementação em C# | Software | Obrigatório | `src/` + build executável | Planejado |
| Testes | Ambos | Obrigatório | unitários, integração, resiliência e carga | Planejado |
| README com funcionamento e execução local | Ambos | Obrigatório | README + comandos reproduzíveis | Planejado |
| Repositório público | Ambos | Obrigatório | GitHub público | Atendido |
| Documentação no repositório | Ambos | Obrigatório | `docs/` + diagramas + ADRs | Em elaboração |
| Independência da consolidação | Ambos | RNF explícito | RNF/ASR + teste com consolidação indisponível | Planejado |
| 50 req/s no consolidado | Ambos | RNF explícito | teste de carga reproduzível + resultados | Planejado |
| No máximo 5% de perda de requisições | Ambos | RNF explícito | métrica e critério de aceite no teste de carga | Planejado |
| Arquitetura de transição | Solutions | Diferencial, se necessária | decisão explícita sobre aplicabilidade; desenho somente se houver legado/migração | Planejado |
| Estimativa de custos de infraestrutura/licenças | Solutions | Diferencial | estimativa da arquitetura-alvo com premissas de sizing | Planejado |
| Monitoramento e observabilidade | Solutions | Diferencial | Observability View + implementação/evidências locais quando aplicável | Planejado |
| Segurança no consumo/integração de serviços | Solutions | Diferencial | Security View + trust boundaries + autenticação/autorização/TLS/segredos | Planejado |
| Escalabilidade | Ambos | Objetivo técnico | ASR + DAS/SDD + arquitetura-alvo + teste de carga | Planejado |
| Resiliência/recuperação de falhas | Ambos | Objetivo técnico | ASRs + ADRs + cenários de falha/recuperação | Planejado |
| Segurança geral | Ambos | Objetivo técnico | Security View + controles implementados/alvo | Planejado |
| Padrões arquiteturais e trade-offs | Ambos | Objetivo técnico | ADR de estilo arquitetural; padrões somente quando justificados | Planejado |
| Integração, protocolos e formatos | Ambos | Objetivo técnico | Integration View + contratos + ADR | Planejado |
| Métricas de confiabilidade, integridade e disponibilidade | Software | Papel/objetivo | SDD + Observability View; metas não informadas ficam pendentes | Planejado |
| C4 Context | Ambos | Documentação adotada | diagrama | Planejado |
| C4 Container | Ambos | Documentação adotada | diagrama | Planejado |
| C4 Component | Software | Documentação adotada | diagramas dos componentes relevantes | Planejado |
| Diagramas dinâmicos | Ambos | Documentação adotada | registro, consolidação, falha e recuperação | Planejado |
| Visão de Dados | Ambos | Documentação adotada | ownership, modelo, consistência, lifecycle, backup/restore | Planejado |
| Visão de Infraestrutura | Solutions | Documentação adotada | local + arquitetura-alvo | Planejado |
| DAS | Solutions | Documentação adotada | documento consolidado da arquitetura de solução | Planejado |
| SDD | Software | Documentação adotada | design interno da aplicação C# | Planejado |
| ADRs | Ambos | Documentação adotada | decisões significativas rastreáveis aos ASRs | Planejado |
| SOLID e boas práticas | Software | Bem-vindo pelo desafio | design e código; sem pattern bombing | Planejado |
| Evoluções futuras | Ambos | Observação do desafio | seção de evolução funcional, sistêmica e infraestrutura | Planejado |

## Regra de conclusão

Antes da entrega:
1. nenhum item **obrigatório** pode permanecer sem artefato/evidência;
2. cada diferencial deve estar implementado/documentado ou ter sua não aplicabilidade explicitamente justificada;
3. itens de objetivo técnico devem aparecer no design mesmo quando não forem integralmente reproduzíveis localmente;
4. a documentação deve diferenciar o que está `IMPLEMENTADO`, `VALIDADO`, na `ARQUITETURA-ALVO` e o que é `EVOLUÇÃO`;
5. nenhuma tecnologia ou pattern deve ser incluído apenas para tornar a solução mais sofisticada.