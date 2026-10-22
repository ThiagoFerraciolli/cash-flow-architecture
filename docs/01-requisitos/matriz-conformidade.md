# Matriz de conformidade com os PDFs

Legenda: **D** = cobertura documental; **I** = implementação; **E** = execução/evidência; **H** = homologação externa; **NA** = não aplicável. D não significa atendimento executável. O corte de implementação está em [escopo-implementacao.md](escopo-implementacao.md).

| Critério e localização na fonte | Artefato principal | Cobertura documental | Entrega |
|---|---|---|---|
| Controle de lançamentos — ambos p.2 | RF/SDD/contratos HTTP | D | I + E T01–T03 |
| Relatório diário — ambos p.2 | RF/contratos/visão de dados | D | I + E T01/T05 |
| Capacidades/domínios — Soluções p.2 | capacidades-e-dominios | D | Documentado |
| RF/RNF refinados — Soluções p.2 | requisitos/ASR/parâmetros | D | Testar atributos implementados |
| Desenho completo — Soluções p.2; Software p.2 | DAS/C4/views/AWS/local/operação | D | Implementação incremental conforme corte de escopo |
| Justificativas — Soluções p.2 | ADR-001 a ADR-011 | D | Documentado |
| Linguagem dominada — Soluções p.2; C# — Software p.2 | SDD | D | I: aplicação C# |
| Testes — ambos p.2 | estratégia-testes | D | E conforme capacidade implementada; cenários AWS reais são H |
| README e execução local — ambos p.2 | README/execucao-local | D | I + E local |
| Repositório público — ambos p.2 | GitHub ThiagoFerraciolli/cash-flow-architecture | Confirmado público em 22/10/2026 via API | Reconfirmar acesso na entrega |
| Documentação no repositório — ambos p.2 | README e docs | D | Versionada junto à implementação |
| Independência — Soluções p.3; Software p.2 | ADR-001/002/004; arquitetura AWS/local | D | E T06/T07 no ambiente local |
| 50 req/s e perda ≤ 5% — Soluções p.3; Software p.2 | RNF/estratégia-testes | D | E T15 no ambiente declarado |
| Escalabilidade/resiliência — ambos p.1 | AWS/parâmetros/recuperação | D | E local para mecanismos implementados; H para infraestrutura AWS |
| Segurança — Soluções p.1–2; Software p.1 | ADR-011/segurança/contratos/AWS | D | I + E local; Cognito/IAM/KMS em H |
| Padrões/SOLID — Software p.2 | SDD/componentes/ADRs | D | I + E T20 |
| Transição se necessária — Soluções p.3 | transição e implantação local→AWS | NA para legado | Sem requisito adicional de migração |
| Custos e licenças — Soluções p.3 | estimativa-custos | D: memória de cálculo e fontes | Atualizar cotação antes de provisionar |
| Observabilidade — Soluções p.3 | segurança/observabilidade/parâmetros | D | I + E para observabilidade implementada |
| Segurança de integração — Soluções p.3 | ADR-010/011/segurança | D | E local; suíte SQS/IAM real em H |

A suíte T01–T20 permanece como mapa de cobertura. Testes que dependem de SQS, IAM, KMS, Cognito ou infraestrutura AWS real são evidências de homologação e não bloqueiam a execução local nem devem ser simulados como certificação da AWS.
