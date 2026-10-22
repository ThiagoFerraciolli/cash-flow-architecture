# Implantação, promoção e operação

## Entregáveis da próxima etapa

C# e testes; Compose local e seed de identidade; migrations; dois adaptadores de mensagens; ferramenta de recuperação; OpenAPI; imagens imutáveis; scripts de carga; Terraform para rede, ECS, RDS, SQS/DLQ, Cognito, IAM, KMS, ALB/WAF, segredos e alarmes. Terraform é a ferramenta escolhida para infraestrutura como código; estado remoto criptografado, bloqueio e acesso restrito, sem segredos em arquivos versionados. Pipeline GitHub Actions com OIDC para AWS, sem access keys estáticas.

## Promoção

1. Build e testes unitários/contrato/análise de dependências; gerar imagens, SBOM e digest.
2. Integração local com PostgreSQL e RabbitMQ reais; executar falhas, concorrência, recuperação e carga; publicar resultados associados ao commit.
3. Provisionar homologação AWS isolada com dados sintéticos; validar plano Terraform, permissões e custos. Infraestrutura de produção usa conta/estado/segredos próprios.
4. Criar bancos, filas, clientes OAuth e permissões; executar migrations como task eventual com papel exclusivo. Falha bloqueia deployment.
5. Subir consumidores compatíveis com versão atual e nova antes de produtores novos; iniciar APIs/Workers, validar readiness e smoke tests.
6. Executar suíte SQS real, incluindo IAM negativo, KMS, visibilidade, redrive, indisponibilidade e expiração simulada em fila isolada. Nunca reduzir retenção da fila de produção para teste.
7. Ensaiar 50 req/s, restore da fonte com bloqueio de escrita e geração física nova de fila, rebuild da projeção, troca de geração e failover. Comparar metas e registrar desvios. Release só pode declarar capacidade demonstrada no ambiente medido.
8. Promover o mesmo digest, sem rebuild, para produção; observar erros, atraso e reconciliação. Alterações de infraestrutura e migrations compatíveis precedem promoção de aplicação.

## Reversão

Rollback de imagem é permitido somente se schema/contrato continuar compatível. Migrations seguem expandir→migrar→contrair; remoção de coluna/contrato ocorre em release posterior, com backup e verificação de dependentes. Rollback de imagem não reverte dados financeiros. Problema em projeção exige procedimento de recuperação; não executar truncamento ou reativação ingênua da geração antiga.

Circuit breaker do ECS protege falha de inicialização; alarme de erro sustentado, ausência de tasks prontas ou divergência dispara interrupção da promoção. Preservar imagens e versão anterior de configuração, com trilha auditável. Para segredo comprometido, rotacionar em vez de restaurar segredo inseguro.

## Dados e brokers

O ambiente local contém dados sintéticos e não é fonte de migração produtiva. Produção inicia vazia. Uma demonstração que precise preservar dados deve exportar snapshot de Lançamentos e identidades de idempotência, importar em ambiente isolado e reconstruir Consolidação antes da liberação; não copiar mensagens entre brokers. Em restore de Lançamentos, a geração antiga de fila permanece isolada e o destino novo é configurado antes da reabertura conforme o procedimento de recuperação. Essas operações são excepcionais e devem ser ensaiadas.

## Critérios para liberação produtiva

Requisitos funcionais demonstrados; 50 req/s e taxa de falha medida; indisponibilidade de toda Consolidação sem interromper lançamentos; zero dupla contabilização; Keycloak e Cognito validados conforme ADR-011; redrive dentro da mesma geração, rebuild e restore com troca de fila ensaiados; backup ativo; alarmes com destino configurado; orçamento e residência de dados confirmados; digests, plano de reversão e responsáveis operacionais registrados.

Esses critérios são controles da futura implantação, não aprovação pendente do texto desta branch. A documentação está aprovada e a implementação permanece fora do escopo atual.
