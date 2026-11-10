# Matriz de conformidade com os PDFs

Esta matriz confronta a entrega com os dois documentos recebidos: **Case — Arquiteto de Soluções** e **Case — Arquiteto de Software**. A coluna "Fonte no desafio" usa o nome do documento e a página do PDF, evitando referências ambíguas.

Legenda: **D** = cobertura documental; **I** = implementação; **E** = execução/evidência local; **H** = homologação externa; **NA** = não aplicável. D não significa atendimento executável. O corte de implementação está em [escopo-implementacao.md](escopo-implementacao.md); os resultados executados estão em [validação local final](../08-testes/validacao-local-final.md).

| Critério | Fonte no desafio | Artefato principal | Cobertura | Entrega |
|---|---|---|---|---|
| Serviço de controle de lançamentos | Arquiteto de Soluções, p.2; Arquiteto de Software, p.2 | RF/SDD/contratos HTTP | D | I + E local: criação/consulta e E2E real |
| Serviço do consolidado diário | Arquiteto de Soluções, p.2; Arquiteto de Software, p.2 | RF/contratos/visão de dados | D | I + E local + E2E real: projeção diária consultada pela API |
| Mapeamento de domínios funcionais e capacidades de negócio | Arquiteto de Soluções, p.2 | [capacidades L1/L2/L3](../03-arquitetura-solucao/capacidades-e-dominios.md); [DAS](../03-arquitetura-solucao/das.md) | D | Cadeia de valor, capacidades, subdomínios e ownership **propostos**; sem validação com comerciante real |
| Refinamento de requisitos funcionais e não funcionais | Arquiteto de Soluções, p.2 | requisitos/ASR/parâmetros | D | Requisitos refinados e mecanismos implementados/evidenciados nos limites indicados nesta matriz |
| Desenho da solução / Arquitetura Alvo | Arquiteto de Soluções, p.2; Arquiteto de Software, p.2 | [DAS](../03-arquitetura-solucao/das.md); [C4](../04-visoes/c4.md); [integração](../04-visoes/visao-integracao.md); [AWS](../07-infraestrutura/arquitetura-aws.md) | D | Núcleo distribuído implementado localmente; contexto corporativo **hipotético**; ALB/NAT/protocolos e topologia AWS **projetados**, não provisionados |
| Justificativa de ferramentas, tecnologias e tipo de arquitetura | Arquiteto de Soluções, p.2 | ADR-001 a ADR-011 | D | Documentado |
| Linguagem | Arquiteto de Soluções, p.2: linguagem dominada; Arquiteto de Software, p.2: C# obrigatório | SDD | D | I: aplicação C# |
| Testes | Arquiteto de Soluções, p.2; Arquiteto de Software, p.2 | estratégia-testes/validação local final | D | E local corrente: **90 passed / 0 failed / 0 skipped**, incluindo unitários, contrato, integração e E2E; cenários AWS reais são H |
| README com funcionamento e execução local | Arquiteto de Soluções, p.2; Arquiteto de Software, p.2 | README/execucao-local | D | D + I + E local: `sh tools/validate-local.sh` executa o golden path completo e cria `.env` quando ausente |
| Repositório público | Arquiteto de Soluções, p.2; Arquiteto de Software, p.2 | GitHub ThiagoFerraciolli/cash-flow-architecture | Confirmado público durante a revisão | Reconfirmar acesso externo no gate de entrega |
| Documentação no repositório | Arquiteto de Soluções, p.2; Arquiteto de Software, p.2 | README e docs | D | Versionada junto à implementação |
| Independência: Lançamentos disponível se Consolidado cair | Arquiteto de Soluções, p.3; Arquiteto de Software, p.2 | ADR-001/002/004; arquitetura local/alvo | D | I + E T06 local: Transactions live/ready e POST 201 durante indisponibilidade de Consolidation; [evidência](../08-testes/validacao-local-final.md) |
| Pico do Consolidado: 50 req/s, no máximo 5% de perda | Arquiteto de Soluções, p.3; Arquiteto de Software, p.2 | RNF/estratégia-testes | D | I + E T15 local limitado: 6.000/6.000, 50 req/s, failure rate 0%, dropped iterations 0; ensaio normativo estendido não executado |
| Escalabilidade e resiliência | Objetivo do desafio: Arquiteto de Soluções, p.1; Arquiteto de Software, p.1 | AWS/parâmetros/recuperação | D | E local para mecanismos implementados; H para infraestrutura AWS |
| Segurança | Objetivo do desafio: Arquiteto de Soluções, p.1–2; Arquiteto de Software, p.1 | ADR-011/segurança/contratos/AWS | D | I + E local via Keycloak/JWT, incluindo autorização de leitura/escrita; Cognito/IAM/KMS apenas projetados |
| Boas práticas, Design Patterns, padrões de arquitetura e SOLID | Arquiteto de Software, p.2 | SDD/componentes/ADRs | D | I: separação de responsabilidades, Ports & Adapters, DIP e padrões aplicados com propósito; T20 permanece mapa de cobertura, sem alegação de execução específica |
| Arquitetura de Transição, se necessária, considerando migração de legado | Diferencial — Arquiteto de Soluções, p.3 | [transição F0–F7](../03-arquitetura-solucao/arquitetura-transicao-hipotetica.md); [runbook](../07-infraestrutura/recuperacao-e-reconciliacao.md) | D hipotético | Snapshot + captura de deltas, manifesto, reconciliação, ownership, fencing, cutover e rollback por fase **propostos**; sem legado real, CDC ou migração executada |
| Estimativa de custos com infraestrutura e licenças | Diferencial — Arquiteto de Soluções, p.3 | [comparação RDS](../07-infraestrutura/estimativa-custos.md); [ADR-003](../06-adr/adr-003-persistencia-postgresql.md) | D: memória de cálculo e trade-off | Dois RDS versus um RDS compartilhado; custos e economia **hipotéticos**, sizing e risco de falha/restore pendentes de validação |
| Monitoramento e Observabilidade | Diferencial — Arquiteto de Soluções, p.3 | segurança/observabilidade/parâmetros | D | I + E T14 local: logs estruturados, OpenTelemetry, `/metrics`, traces OTLP/Jaeger; stack AWS não homologada |
| Critérios de segurança para consumo/integração de serviços | Diferencial — Arquiteto de Soluções, p.3 | ADR-010/011/segurança | D | I + E local para JWT/RabbitMQ e fronteiras locais; SQS/IAM/TLS/KMS de produção pertencem à H |

A suíte T01–T20 permanece como mapa de cobertura, não como alegação de execução integral de todos os cenários. T06/T09/T10/T13/T14/T15/T18 têm evidência operacional local limitada em [rastreabilidade](../08-testes/production-grade-local-traceability.md); T07 completo não foi executado. Testes que dependem de SQS, IAM, KMS, Cognito ou infraestrutura AWS real pertencem à homologação futura; nenhuma infraestrutura AWS foi provisionada ou testada neste estudo.

O gate automatizado local corrente de 23/10/2026 é **90/0/0**, com build **0 warnings / 0 errors**. Resultados anteriores (80/84/89) são histórico de evolução e não representam o estado final da branch de validação.

## Rastreabilidade adicional do feedback de Arquitetura de Soluções

| Lacuna apontada na revisão | Evidência documental | Natureza da cobertura | Pendência verificável |
|---|---|---|---|
| Alinhamento entre negócio, capacidades e bounded contexts | [Capacidades e domínios](../03-arquitetura-solucao/capacidades-e-dominios.md) → [DAS](../03-arquitetura-solucao/das.md) → [C4](../04-visoes/c4.md) | D, modelo proposto | Validação com stakeholders de negócio em cenário real |
| Ecossistema de comerciante e sistemas adjacentes | [C4 corporativo](../04-visoes/c4.md); [capacidades](../03-arquitetura-solucao/capacidades-e-dominios.md) | D, hipóteses de descoberta | Confirmar integrações e ownership; não há ERP/PDV implementado |
| Componentes e protocolos de rede | [AWS](../07-infraestrutura/arquitetura-aws.md); [integração](../04-visoes/visao-integracao.md) | D, arquitetura-alvo | Renderizar Mermaid, conferir topologia e homologar AWS |
| Overengineering de persistência | [ADR-003](../06-adr/adr-003-persistencia-postgresql.md); [custos](../07-infraestrutura/estimativa-custos.md) | D, decisão condicional | Benchmark, isolamento, restore seletivo e cotação AWS |
| Migração histórica, coexistência e rollback | [Transição](../03-arquitetura-solucao/arquitetura-transicao-hipotetica.md) | D hipotético | Inventário de legado, captura, reconciliação e ensaios de corte |
| Governança do fechamento do feedback | [Auditoria](../09-revisao/auditoria-feedback-solutions-architect.md) | D, controle de pendências | Gates V1–V6, sem equivaler a execução/homologação |

**Interpretação:** a cobertura documental adicional não altera automaticamente o status I/E/H da solução. Os resultados de testes citados foram registrados em 23/10/2026 e **não foram reexecutados nesta revisão documental**.
