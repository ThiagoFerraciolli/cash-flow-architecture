# ADR-006 — AWS com ECS/Fargate

- **Direcionadores:** RNF-008/009/010/012/013, demonstração de caminho produtivo.

## Alternativas e decisão

EKS oferece plataforma Kubernetes, mas adiciona operação sem requisito que a justifique. Functions exigiriam adaptar execução/empacotamento e modelo de consumo. ECS/Fargate preserva containers das APIs/Workers e fornece escala e implantação independentes; adotado como referência AWS.

A arquitetura-alvo prevê quatro ECS Services, dois RDS PostgreSQL Multi-AZ, SQS/DLQ, ALB/WAF, Cognito, IAM, KMS, Secrets Manager, ECR e observabilidade, distribuídos em duas zonas. Quantidades e dimensionamento de referência estão na arquitetura AWS; preços e hipóteses na estimativa de custos. Terraform e pipeline com OIDC são decisões para uma futura implementação da infraestrutura e do processo de promoção; não existem como infraestrutura executável nesta entrega.

## Consequências

Custo mínimo superior ao ambiente local, aceito para redundância e isolamento. Não há Kubernetes a operar. Multi-AZ não resolve falha regional ou corrupção lógica. Metas internas são objetivos de homologação e não representam SLA contratual nem capacidade demonstrada. Nenhuma infraestrutura AWS foi provisionada ou validada nesta entrega; ECS/Fargate, RDS Multi-AZ, SQS, Cognito, IAM e KMS permanecem arquitetura-alvo.
