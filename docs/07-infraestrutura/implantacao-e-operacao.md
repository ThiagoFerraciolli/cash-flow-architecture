# Implantação, promoção e operação

## Estado da entrega e arquitetura-alvo

A entrega local contém C#/.NET, testes, Docker Compose, migrations, RabbitMQ, recuperação administrativa, imagens de aplicação e ensaio de carga local. OpenAPI não foi implementado e não é requisito obrigatório do desafio; permanece evolução possível do contrato HTTP documentado. A topologia AWS e a infraestrutura como código são arquitetura-alvo, não artefatos executáveis desta entrega. Terraform é a ferramenta escolhida para uma futura implementação de IaC; o estado deverá ser criptografado, bloqueado e restrito, sem segredos versionados. Um pipeline AWS futuro deverá usar OIDC, sem access keys estáticas.

## Promoção

1. Restaurar dependências em modo locked, compilar e executar a suíte automatizada; gerar imagens imutáveis dos quatro processos de aplicação.
2. Validar localmente com PostgreSQL e RabbitMQ reais as capacidades correspondentes ao corte implementado e associar os resultados ao commit.
3. Em homologação AWS futura, usar dados sintéticos e validar infraestrutura, permissões, custos e os componentes específicos de cloud antes de qualquer promoção produtiva.
4. Criar bancos, filas, clientes OAuth e permissões do ambiente-alvo; migrations devem executar como tarefa controlada e falha deve bloquear deployment.
5. Subir consumidores compatíveis antes de produtores incompatíveis; validar readiness e smoke tests.
6. A futura suíte SQS real deve incluir IAM negativo, KMS, visibilidade, retry e redrive. Ela é evidência de homologação AWS e não foi executada nesta entrega.
7. Ensaios de capacidade, restore, rebuild e failover devem registrar o ambiente medido. Só declarar capacidade demonstrada quando houver evidência executada correspondente.
8. Promoção produtiva deve reutilizar o mesmo artefato imutável validado, sem rebuild.

## Reversão

Rollback de imagem é permitido somente se schema/contrato continuar compatível. Migrations seguem expandir→migrar→contrair; remoção de coluna/contrato ocorre em release posterior, com backup e verificação de dependentes. Rollback de imagem não reverte dados financeiros. Problema em projeção exige procedimento de recuperação; não executar truncamento ou reativação ingênua da geração antiga.

Na arquitetura AWS, mecanismos nativos de deployment e health checks devem interromper uma promoção quando tasks não ficam saudáveis. Alarmes de erro sustentado, ausência de tasks prontas ou divergência também devem bloquear promoção. Esses mecanismos não foram executados em AWS neste estudo de caso. Preservar imagens e versão anterior de configuração, com trilha auditável. Para segredo comprometido, rotacionar em vez de restaurar segredo inseguro.

## Dados e brokers

O ambiente local contém dados sintéticos e não é fonte de migração produtiva. Produção inicia vazia. Uma demonstração que precise preservar dados deve exportar snapshot de Lançamentos e identidades de idempotência, importar em ambiente isolado e reconstruir Consolidação antes da liberação; não copiar mensagens entre brokers. Operações de restore e troca física de fila pertencem ao procedimento de recuperação e devem ser ensaiadas antes de uso produtivo.

## Critérios para liberação produtiva

Requisitos funcionais demonstrados; capacidade e taxa de falha medidas no ambiente-alvo; indisponibilidade de toda Consolidação sem interromper lançamentos; zero dupla contabilização; identidade validada no ambiente correspondente; mecanismos de recuperação ensaiados; backup ativo; alarmes com destino configurado; orçamento e residência de dados confirmados; artefatos, plano de reversão e responsáveis operacionais registrados.

Esses critérios descrevem a arquitetura operacional de produção. Não são todos gates da entrega local: capacidades classificadas como arquitetura-alvo, homologação ou evolução só podem ser consideradas demonstradas quando o respectivo ambiente, implementação e evidências existirem.
