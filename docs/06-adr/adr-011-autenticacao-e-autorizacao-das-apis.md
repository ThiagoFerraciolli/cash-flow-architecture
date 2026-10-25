# ADR-011 — Autenticação e autorização das APIs

- **Direcionadores:** RNF-009; clientes técnicos, execução local independente de AWS e implantação gerenciada na AWS.

## Decisão

Adotar OAuth 2.0 client credentials com tokens JWT de acesso. Keycloak é o provedor implementado no ambiente local; Amazon Cognito User Pool é a escolha da arquitetura-alvo AWS. As APIs validam o token e aplicam permissões no próprio processo. ALB, WAF e rede protegem a borda na arquitetura AWS, mas não substituem autenticação ou autorização da aplicação.

Permissões lógicas da aplicação: `transactions.write`, `transactions.read` e `consolidations.read`. No Keycloak local, scopes/mappers emitem as permissões usadas pelas APIs. Na arquitetura Cognito, os valores externos serão scopes do resource server `cashflow`: `cashflow/transactions.write`, `cashflow/transactions.read` e `cashflow/consolidations.read`. A aplicação aceita a representação local `permissions` e a representação por `scope`, mantendo as regras de negócio independentes do provedor.

## Validação e identidade

No corte local, as APIs exigem token assinado com RS256, issuer e audience configurados, expiração, cliente permitido e permissão da rota. A tolerância de relógio configurada é 60 segundos; o realm local emite access tokens com duração de 15 minutos. A validação usa o middleware JWT Bearer/JWKS do provedor e falha fechada quando o token não pode ser validado.

Keycloak exige audience `cashflow-api` e `client_id` emitido pelo mapper aprovado. A identidade usada para auditoria/idempotência de escrita é `issuer + client_id` obtida de claims já validados. Endpoints de negócio exigem autenticação e a permissão correspondente; liveness/readiness e métricas pertencem à superfície operacional local e não devem ser expostos publicamente em produção.

Cognito ainda não foi implementado ou homologado. Validação de `token_use`, convenção exata de scopes/`aud`, rotação de JWKS e demais diferenças do Cognito são requisitos de homologação da arquitetura-alvo, não comportamento comprovado pela execução local.

## Credenciais e operação

Segredos de clientes não ficam versionados no repositório: o Compose usa `.env` ignorado pelo Git e o bootstrap local recebe valores de desenvolvimento. Na arquitetura AWS, segredos devem ficar em Secrets Manager com acesso mínimo e rotação controlada.

O corte atual não implementa uma trilha específica e estruturada de auditoria para cada 401/403 contendo issuer, clientId e permissão requerida. Esse é um requisito operacional de produção a ser implementado com sanitização e sem registrar tokens. Logs gerais e correlação existentes não devem ser apresentados como se já constituíssem essa auditoria de segurança.

## Alternativas e consequências

Chave compartilhada estática foi rejeitada por rotação, granularidade e auditoria insuficientes. Autenticação exclusiva no ALB foi rejeitada porque os clientes são técnicos e as APIs devem validar identidade e permissões. Usar Keycloak também na AWS aumentaria a responsabilidade operacional; usar Cognito local exigiria conta e conectividade. Dois provedores trazem diferenças de claims, aceitas mediante uma fronteira explícita de autorização e testes no ambiente correspondente antes de produção.

T13 demonstra somente o comportamento local registrado: 401 sem token em escrita, 403 para cliente sem permissão de escrita, 201 para escrita autorizada e 200 para leitura autorizada de Consolidação. Não demonstra Cognito, IAM, WAF, TLS produtivo ou a auditoria de 401/403 descrita como evolução.

Referências complementares: [visão de segurança](../04-visoes/seguranca-resiliencia-observabilidade.md), [contratos HTTP](../05-arquitetura-software/contratos-http.md), [execução local](../07-infraestrutura/execucao-local.md) e [arquitetura AWS](../07-infraestrutura/arquitetura-aws.md).
