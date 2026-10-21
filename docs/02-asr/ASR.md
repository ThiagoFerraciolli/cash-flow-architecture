# Architecturally Significant Requirements (ASR)

## Objetivo

ASRs são requisitos cuja satisfação influencia de forma significativa a estrutura, tecnologias, integração, dados, segurança, operação ou custo da solução.

## ASR-001 — Independência entre lançamento e consolidação

**Origem:** RNF-001.

A indisponibilidade do serviço de consolidação não pode impedir o registro de novos lançamentos.

**Impactos arquiteturais:** separação de responsabilidades, deployments independentes, integração assíncrona, persistência independente e eliminação de dependência síncrona da consolidação no caminho crítico de escrita.

## ASR-002 — Durabilidade do lançamento confirmado

**Origem:** RNF-004 e PRE-001.

Um lançamento confirmado pela API não deve ser perdido por falha entre a persistência do lançamento e a publicação do evento.

**Impactos arquiteturais:** transação ACID local, Transactional Outbox, persistência durável e estratégia produtiva de backup/recuperação condicionada à definição de RPO.

## ASR-003 — Consistência eventual do consolidado

**Origem:** RNF-005 e PRE-003.

O consolidado é uma projeção derivada e pode permanecer temporariamente defasado em relação aos lançamentos.

**Impactos arquiteturais:** processamento assíncrono, freshness explícita, monitoramento de lag e capacidade de recuperação/reprocessamento.

## ASR-004 — Processamento idempotente

**Origem:** RNF-006.

Retries e reentrega de eventos não podem gerar lançamento duplicado nem dupla contabilização no consolidado.

**Impactos arquiteturais:** Idempotency-Key na entrada quando aplicável, identificador único de evento, controle de eventos processados e constraints transacionais.

## ASR-005 — Recuperação após indisponibilidade da consolidação

**Origem:** RNF-007.

Após a recuperação do serviço de consolidação, o backlog deve ser processado e a projeção deve convergir para os lançamentos registrados.

**Impactos arquiteturais:** retenção de eventos no broker, consumer offsets, retries controlados, idempotência e observabilidade de lag.

## ASR-006 — Capacidade de 50 requisições por segundo no consolidado

**Origem:** RNF-002.

A arquitetura deve considerar o pico explicitamente informado no desafio.

**Impactos arquiteturais:** serviços stateless, escalabilidade horizontal, banco e índices adequados, load balancing/autoscaling na arquitetura-alvo e teste de carga como evidência local.

## ASR-007 — Segurança das APIs e dados

**Origem:** RNF-009.

Operações protegidas exigem autenticação e a arquitetura produtiva deve considerar proteção de borda, identidade, segredos, criptografia e least privilege.

**Impactos arquiteturais:** autenticação local para demonstração, IdP OAuth2/OIDC na arquitetura-alvo, WAF/API Gateway, TLS, secret management, KMS/IAM e redes privadas.

## ASR-008 — Implantação e escalabilidade independentes

**Origem:** RNF-008 e RNF-014.

Transaction Service e Consolidation Service devem possuir ciclo de vida independente.

**Impactos arquiteturais:** microserviços separados, containers separados, bancos de responsabilidade própria, health checks e scaling independente.

## ASR-009 — Observabilidade e rastreabilidade ponta a ponta

**Origem:** RNF-010 e RNF-011.

A operação deve permitir identificar o caminho de um lançamento desde a API até sua aplicação no consolidado.

**Impactos arquiteturais:** logs estruturados, correlation/trace IDs, métricas de Outbox, Kafka consumer lag, estado do processamento, health checks e alertas.

## ASRs pendentes de definição

Os seguintes temas são arquiteturalmente significativos, porém não possuem meta suficiente no desafio:
- SLA/SLO;
- RTO;
- RPO;
- latência P95/P99;
- retenção;
- disaster recovery regional;
- requisitos regulatórios e de localização dos dados.

Eles permanecem em `docs/01-requisitos/questoes-em-aberto.md` até definição com stakeholders.