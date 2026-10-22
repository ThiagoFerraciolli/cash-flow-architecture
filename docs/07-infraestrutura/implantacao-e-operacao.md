# Implantação, promoção e operação

## Escopo de implantação

A arquitetura-alvo prevê C# e testes, Compose local, migrations, mensageria, recuperação, OpenAPI, imagens imutáveis, carga e uma topologia AWS com infraestrutura como código. O que é obrigatório na entrega local, o que depende de homologação AWS e o que permanece como evolução está definido em [escopo de implementação](../01-requisitos/escopo-implementacao.md). Terraform é a ferramenta escolhida para uma futura infraestrutura como código; estado remoto deve ser criptografado, bloqueado e restrito, sem segredos versionados. Um pipeline AWS futuro deve usar OIDC, sem access keys estáticas.

## Promoção

1. Build e testes unitários/contrato/análise de dependências; gerar imagens e artefatos quando essa etapa estiver implementada.
2. Integração local com PostgreSQL e RabbitMQ reais; executar falhas, concorrência, recuperação e carga correspondentes às capacidades implementadas; associar resultados ao commit.
3. Homologação AWS, quando disponível, deve ser isolada e usar dados sintéticos; validar infraestrutura, permissões e custos antes de qualquer promoção produtiva.
4. Criar bancos, filas, clientes OAuth e permissões do ambiente alvo; migrations devem executar como tarefa controlada e falha deve bloquear deployment.
5. Subir consumidores compatíveis antes de produtores incompatíveis; validar readiness e smoke tests.
6. A suíte SQS real, incluindo IAM negativo, KMS, visibilidade e redrive, é evidência de homologação AWS e não bloqueia a entrega executável local.
7. Ensaios de capacidade, restore, rebuild e failover devem registrar o ambiente medido. Só declarar capacidade demonstrada quando houver evidência executada correspondente.
8. Promoção produtiva deve reutilizar o mesmo artefato imutável validado, sem rebuild.

## Reversão

Rollback de imagem é permitido somente se schema/contrato continuar compatível. Migrations seguem expandir→migrar→contrair; remoção de coluna/contrato ocorre em release posterior, com backup e verificação de dependentes. Rollback de imagem não reverte dados financeiros. Problema em projeção exige procedimento de recuperação; não executar truncamento ou reativação ingênua da geração antiga.

Circuit breaker do ECS protege falha de inicialização; alarme de erro sustentado, ausência de tasks prontas ou divergência deve interromper promoção futura. Preservar imagens e versão anterior de configuração, com trilha auditável. Para segredo comprometido, rotacionar em vez de restaurar segredo inseguro.

## Dados e brokers

O ambiente local contém dados sintéticos e não é fonte de migração produtiva. Produção inicia vazia. Uma demonstração que precise preservar dados deve exportar snapshot de Lançamentos e identidades de idempotência, importar em ambiente isolado e reconstruir Consolidação antes da liberação; não copiar mensagens entre brokers. Operações de restore e troca física de fila pertencem ao procedimento de recuperação e devem ser ensaiadas antes de uso produtivo.

## Critérios para liberação produtiva

Requisitos funcionais demonstrados; capacidade e taxa de falha medidas; indisponibilidade de toda Consolidação sem interromper lançamentos; zero dupla contabilização; identidade validada no ambiente correspondente; mecanismos de recuperação ensaiados; backup ativo; alarmes com destino configurado; orçamento e residência de dados confirmados; artefatos, plano de reversão e responsáveis operacionais registrados.

Esses critérios descrevem a arquitetura operacional de produção. Não são todos gates da entrega local: capacidades classificadas como homologação ou evolução só podem ser consideradas demonstradas quando o respectivo ambiente e evidências existirem.
