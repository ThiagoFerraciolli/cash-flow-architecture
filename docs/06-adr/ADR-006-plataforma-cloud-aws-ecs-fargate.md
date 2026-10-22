# ADR-006 — AWS como referência e ECS/Fargate para workloads

- **Status:** ACEITO PARA A ARQUITETURA DO DESAFIO
- **Drivers:** RNF-008, RNF-010, RNF-012, RNF-013, ASR-008, ASR-009

## Contexto

A entrega precisa demonstrar arquitetura-alvo, segurança, observabilidade, escalabilidade e custos. O desafio não exige cloud específica nem Kubernetes.

## Alternativas

**EKS/Kubernetes:** flexível, mas adiciona plataforma/cluster sem requisito que o justifique.  
**ECS/Fargate:** containers, escala/deploy independente e operação reduzida.  
**Functions:** válidas para outros perfis, porém mudariam desnecessariamente o modelo de APIs/workers do desafio.

## Decisão

Usar **AWS como referência documental** e **ECS/Fargate** para os quatro processos: Transactions API, Transactions Worker, Consolidation API e Consolidation Worker.

RDS PostgreSQL atende persistência; SQS Standard + DLQ atende integração conforme ADR-007; ALB atende entrada HTTP; ECR, Secrets Manager/KMS, CloudWatch e OpenTelemetry completam as capacidades de plataforma.

## Consequências

- 2 serviços de negócio continuam existindo, apesar de 4 tasks/processos;
- escala é independente por processo;
- não há cluster Kubernetes para operar;
- IAM Task Roles permitem menor privilégio;
- custos são orientados a recursos/uso e precisam incluir rede/observabilidade;
- eventual padrão corporativo Kubernetes permite remapeamento sem alterar domínio/contratos.

Não são criados SLA, RTO, RPO, Multi-AZ ou cross-region como requisitos. Esses itens dependem de objetivos produtivos ausentes.