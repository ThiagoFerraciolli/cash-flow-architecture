# Auditoria transversal — resposta ao feedback de Senior Solutions Architect

> **Escopo desta rodada:** revisão documental por leitura de arquivos selecionados da branch `docs/feedback-solutions-architect`, confronto de requisitos, arquitetura, ADR, custos, runbooks e evidências locais. **Não** houve build, execução de testes, implantação AWS, renderização visual de Mermaid em browser ou varredura automatizada exaustiva de todos os links. As correções desta rodada não equivalem a homologação.

## Matriz feedback → correção → evidência → status

| Crítica do avaliador | Correção rastreável | Verificação efetuada | Status / pendência |
|---|---|---|---|
| Capacidades de negócio confundidas com serviços | [Capacidades e domínios](../03-arquitetura-solucao/capacidades-e-dominios.md): cadeia de valor, L1/L2/L3, subdomínios e ownership; [DAS](../03-arquitetura-solucao/das.md) | Conferida distinção entre capacidade, contexto e tecnologia | **Documentado;** validação com negócio real não aplicável ao case hipotético |
| Ecossistema corporativo insuficiente | [C4](../04-visoes/c4.md): contexto complementar e integrações hipotéticas; [capacidades](../03-arquitetura-solucao/capacidades-e-dominios.md): stakeholders e perguntas | Conferido uso explícito de hipótese, sem afirmar ERP/PDV real | **Documentado;** interfaces empresariais não implementadas |
| ALB/NAT/protocolos pouco visíveis | [AWS](../07-infraestrutura/arquitetura-aws.md), [integração](../04-visoes/visao-integracao.md) e [segurança](../04-visoes/seguranca-resiliencia-observabilidade.md) | Corrigidos identificadores visuais e direção de polling SQS no [C4](../04-visoes/c4.md) | **Correção textual feita;** renderização Mermaid e inspeção visual ainda pendentes |
| Dois RDS sem trade-off econômico | [ADR-003](../06-adr/adr-003-persistencia-postgresql.md) e [estimativa](../07-infraestrutura/estimativa-custos.md) | Conferida comparação A/B, risco de contenção e caráter hipotético dos valores | **Análise documental feita;** sizing e preços AWS não homologados |
| Migração histórica e cutover genéricos | [Transição](../03-arquitetura-solucao/arquitetura-transicao-hipotetica.md): G0–G7, snapshot, deltas, identidade, reconciliação, fencing e rollback | Conferida separação entre migração, rebuild e restore nos [runbooks](../07-infraestrutura/recuperacao-e-reconciliacao.md) | **Plano hipotético documentado;** nenhum legado ou ensaio real |
| Ênfase em implementação em vez de solução | [DAS](../03-arquitetura-solucao/das.md) e [capacidades](../03-arquitetura-solucao/capacidades-e-dominios.md) iniciam pelo resultado de negócio | Leitura dos títulos e seções de abertura; verificada rastreabilidade até componentes | **Melhoria documentada;** revisão editorial de ponta a ponta pendente |

## Inconsistências efetivamente identificadas e corrigidas

1. **Mermaid de transição:** identificador `M` era usado tanto como nó quanto como `subgraph` no baseline hipotético. O diagrama passou a apontar para o módulo dentro do agrupamento; **a renderização não foi testada**.
2. **C4 de contêineres:** fluxo de consumo agora diferencia entrega AMQP do RabbitMQ local e `ReceiveMessage` iniciado pelo Worker no SQS AWS alvo. As setas são **alternativas por ambiente**, não comunicação simultânea.
3. **Inventário AWS:** nomes de agrupamentos corrigidos para corresponder aos IDs do diagrama (`PUBLIC`, `PRIVATEA`, `PRIVATEB`, `DATABASE`).

## Verificação de links desta rodada

Foram inspecionados 14 arquivos da revisão (incluindo as duas matrizes, DAS, capacidades, C4, integração, AWS, custos, transição, runbooks e esta auditoria) em dois lotes. O verificador extraiu links Markdown relativos e normalizou caminhos `..`/`.` contra a árvore Git: **93 + 27 = 120 links, 0 destinos inexistentes**. Links externos, âncoras e renderização não foram testados; não é auditoria de todos os arquivos do repositório.

## Controle de evidências: o que é demonstrado e o que não é

| Alegação | Evidência documental consultada | Formulação permitida |
|---|---|---|
| 90 testes, 0 falhas, 0 skips | [Matriz de rastreabilidade](../01-requisitos/matriz-rastreabilidade.md) e [validação local](../08-testes/validacao-local-final.md) | Resultado de execução **local registrada anteriormente**, não reexecutada nesta auditoria |
| Independência de Lançamentos | [T06](../08-testes/validacao-local-final.md) | Dois POSTs aceitos durante parada de API/Worker/banco de Consolidação; não 1.000 fatos a 10/s |
| 50 req/s no Consolidado | [T15](../08-testes/validacao-local-final.md) | 6.000 GETs/120 s, 0 falhas na janela local; não 10 minutos, AWS ou SLA produtivo |
| Mensageria e identidade AWS | [ADR-010](../06-adr/adr-010-mensageria-local-rabbitmq-e-aws-sqs.md), [AWS](../07-infraestrutura/arquitetura-aws.md) | SQS/Cognito são **arquitetura-alvo**; adaptador SQS não implementado |
| Migração de legado | [Transição](../03-arquitetura-solucao/arquitetura-transicao-hipotetica.md) | Plano arquitetural **hipotético**; sem execução nem legado real |
| Custo RDS compartilhado | [Estimativa](../07-infraestrutura/estimativa-custos.md) | Simulação com tarifas **hipotéticas**, dependente de dimensionamento e validação |

## Gates e status antes de considerar o feedback encerrado

- **V1 — Mermaid:** renderizar e inspecionar visualmente todos os diagramas alterados, inclusive a leitura de fluxos alternativos RabbitMQ/SQS, setas de rotas NAT e limites de VPC.
- **V2 — Links: VERIFICAÇÃO PARCIAL APROVADA.** Verificados **120 links relativos Markdown em 14 documentos alterados/revisados** contra a árvore Git da branch: **0 destinos ausentes**. O teste verifica existência de arquivo, não âncoras, conteúdo de URLs externas ou todos os demais documentos do repositório. A varredura integral continua pendente.
- **V3 — Matriz: DOCUMENTAÇÃO ATUALIZADA.** [Matriz de conformidade](../01-requisitos/matriz-conformidade.md) e [matriz de rastreabilidade](../01-requisitos/matriz-rastreabilidade.md) agora vinculam capacidades, ecossistema, rede, RDS e migração às evidências e pendências. Revisão de links e auditoria final permanecem gates separados; nenhuma nova execução de testes foi alegada.
- **V4 — Consistência técnica:** revisar toda ocorrência de RDS separado/compartilhado, AWS implementada/alvo, AMQP/SQS e migração/rebuild/restore; classificar conflitos remanescentes.
- **V5 — Evidência executável:** rodar build/testes em checkout da branch e registrar SHA, ambiente, comandos e resultados; não reutilizar 90 testes anteriores como se fossem execução atual.
- **V6 — Avaliação editorial:** leitura final como avaliador de Soluções Sênior: objetivo, capacidades, contexto, opções, trade-offs, riscos, transição, custos e só depois implementação.

**Critério de fechamento:** nenhum item crítico sem correção ou justificativa, links e diagramas verificados, afirmações de implementação comprovadas, decisões condicionais claramente identificadas e nenhuma mudança em código, PR ou merge sem aprovação.
