# ADR-006 — AWS com ECS/Fargate

- **Direcionadores:** RNF-008/009/010/012/013, demonstração de caminho produtivo.

## Alternativas e decisão

EKS oferece plataforma Kubernetes, mas adiciona operação sem requisito que a justifique. Functions exigiriam adaptar execução/empacotamento e modelo de consumo. ECS/Fargate preserva containers das APIs/Workers e fornece escala e implantação independentes; adotado como referência AWS.

Quatro ECS Services, dois RDS PostgreSQL Multi-AZ, SQS/DLQ, ALB/WAF, Cognito, IAM, KMS, Secrets Manager, ECR e observabilidade. Duas zonas e NAT por zona. Quantidades e dimensionamento normativos na arquitetura AWS; preços e hipóteses na estimativa de custos. Terraform e pipeline com OIDC estão definidos no plano de implantação.

## Consequências

Custo mínimo superior ao ambiente local, aceito para redundância e isolamento. Não há Kubernetes a operar. Multi-AZ não resolve falha regional ou corrupção lógica. Metas internas são adotadas e serão medidas; não representam SLA contratual. A decisão não autoriza provisionamento nesta branch documental.
