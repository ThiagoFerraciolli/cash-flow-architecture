# Matriz de conformidade com os PDFs

Legenda: **D** = cobertura documental; **I** = implementação; **E** = execução/evidência local; **H** = homologação externa; **NA** = não aplicável. D não significa atendimento executável. O corte de implementação está em [escopo-implementacao.md](escopo-implementacao.md); os resultados executados estão em [validação local final](../08-testes/validacao-local-final.md).

| Critério e localização na fonte | Artefato principal | Cobertura documental | Entrega |
|---|---|---|---|
| Controle de lançamentos — ambos p.2 | RF/SDD/contratos HTTP | D | I + E local: criação/consulta e E2E real |
| Relatório diário — ambos p.2 | RF/contratos/visão de dados | D | I + E local + E2E real: projeção diária consultada pela API |
| Capacidades/domínios — Soluções p.2 | capacidades-e-dominios | D | Documentado |
| RF/RNF refinados — Soluções p.2 | requisitos/ASR/parâmetros | D | Requisitos refinados e mecanismos implementados/evidenciados nos limites indicados nesta matriz |
| Desenho completo — Soluções p.2; Software p.2 | DAS/C4/views/AWS/local/operação | D | Núcleo distribuído implementado localmente; topologia AWS projetada |
| Justificativas — Soluções p.2 | ADR-001 a ADR-011 | D | Documentado |
| Linguagem dominada — Soluções p.2; C# — Software p.2 | SDD | D | I: aplicação C# |
| Testes — ambos p.2 | estratégia-testes/validação local final | D | E local corrente: **90 passed / 0 failed / 0 skipped**, incluindo unitários, contrato, integração e E2E; cenários AWS reais são H |
| README e execução local — ambos p.2 | README/execucao-local | D | D + I + E local: `.env.example` + `tools/validate-local.sh`, topologia completa, restore locked, build e testes |
| Repositório público — ambos p.2 | GitHub ThiagoFerraciolli/cash-flow-architecture | Confirmado público durante a revisão | Reconfirmar acesso externo no gate de entrega |
| Documentação no repositório — ambos p.2 | README e docs | D | Versionada junto à implementação |
| Independência — Soluções p.3; Software p.2 | ADR-001/002/004; arquitetura AWS/local | D | I + E T06 local: Transactions live/ready e POST 201 durante indisponibilidade de Consolidation; [evidência](../08-testes/validacao-local-final.md) |
| 50 req/s e perda ≤ 5% — Soluções p.3; Software p.2 | RNF/estratégia-testes | D | I + E T15 local limitado: 6.000/6.000, 50 req/s, failure rate 0%, dropped iterations 0; ensaio normativo estendido não executado |
| Escalabilidade/resiliência — ambos p.1 | AWS/parâmetros/recuperação | D | E local para mecanismos implementados; H para infraestrutura AWS |
| Segurança — Soluções p.1–2; Software p.1 | ADR-011/segurança/contratos/AWS | D | I + E local via Keycloak/JWT, incluindo autorização de leitura/escrita; Cognito/IAM/KMS apenas projetados |
| Padrões/SOLID — Software p.2 | SDD/componentes/ADRs | D | I: separação de responsabilidades, Ports & Adapters, DIP e padrões aplicados com propósito; T20 permanece mapa de cobertura, sem alegação de execução específica |
| Transição se necessária — Soluções p.3 | transição e implantação local→AWS | NA para legado | Não foi fornecido legado a migrar; evolução local→AWS está documentada |
| Custos e licenças — Soluções p.3 | estimativa-custos | D: memória de cálculo e fontes | Estimativa arquitetural; atualizar preços/cotação antes de provisionar |
| Observabilidade — Soluções p.3 | segurança/observabilidade/parâmetros | D | I + E T14 local: logs estruturados, OpenTelemetry, `/metrics`, traces OTLP/Jaeger; stack AWS não homologada |
| Segurança de integração — Soluções p.3 | ADR-010/011/segurança | D | I + E local para JWT/RabbitMQ e fronteiras locais; SQS/IAM/TLS/KMS de produção pertencem à H |

A suíte T01–T20 permanece como mapa de cobertura, não como alegação de execução integral de todos os cenários. T06/T09/T10/T13/T14/T15/T18 têm evidência operacional local limitada em [rastreabilidade](../08-testes/production-grade-local-traceability.md); T07 completo não foi executado. Testes que dependem de SQS, IAM, KMS, Cognito ou infraestrutura AWS real pertencem à homologação futura; nenhuma infraestrutura AWS foi provisionada ou testada neste estudo.

O gate automatizado local corrente de 23/10/2026 é **90/0/0**, com build **0 warnings / 0 errors**. Resultados anteriores (80/84/89) são histórico de evolução e não representam o estado final da branch de validação.