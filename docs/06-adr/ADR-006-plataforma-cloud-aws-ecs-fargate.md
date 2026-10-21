# ADR-006 — AWS como referência e ECS/Fargate para workloads

- **Status:** PROPOSTO

## Contexto

A solução precisa apresentar arquitetura-alvo, segurança, observabilidade, escalabilidade e estimativa de infraestrutura. O desafio não exige uma cloud específica nem Kubernetes.

## Alternativas

### Kubernetes/EKS
Prós: ecossistema amplo, portabilidade e controles avançados.
Contras: custo e complexidade operacional sem requisito que os justifique neste case.

### ECS/Fargate
Prós: containers, scale-out, deployments independentes, integração AWS e ausência de gestão de nós.
Contras: maior acoplamento operacional à AWS e menos portabilidade da camada de orquestração.

### Serverless functions
Prós: escala por demanda e operação reduzida.
Contras: muda o modelo de execução dos workers/APIs e não traz benefício necessário para demonstrar o desenho pedido.

## Decisão proposta

Usar **AWS como cloud de referência documental** e **ECS/Fargate** para executar APIs e workers containerizados.

A escolha não significa que a solução depende conceitualmente da AWS: contratos HTTP/eventos, PostgreSQL, Kafka e OpenTelemetry preservam portabilidade nas principais fronteiras.

## Consequências

- cada serviço escala e é implantado independentemente;
- não há cluster Kubernetes para operar;
- sizing é definido por task CPU/memória;
- custos podem ser estimados diretamente por recursos solicitados;
- eventual exigência corporativa por Kubernetes permitiria remapear containers para EKS sem alterar domínios/contratos.

## Relação com requisitos

Atende principalmente RNF-008, RNF-010, RNF-012, RNF-013 e ASR-008/009. Não cria SLA/RTO/RPO não fornecidos.