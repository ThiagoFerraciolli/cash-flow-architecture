# ADR-011 — Autenticação e autorização das APIs

- **Estado:** APROVADO
- **Direcionadores:** RNF-009; clientes técnicos, execução local independente de AWS e implantação gerenciada na AWS.

## Decisão

Adotar OAuth 2.0 client credentials com tokens JWT de acesso. Keycloak é o provedor local; Amazon Cognito User Pool é o provedor na AWS. As APIs validam o token e aplicam permissões no próprio processo. ALB, WAF e rede protegem a borda, mas não substituem autenticação ou autorização da aplicação.

Permissões lógicas da aplicação: `transactions.write`, `transactions.read` e `consolidations.read`. No Cognito, os valores externos são scopes do resource server `cashflow`: `cashflow/transactions.write`, `cashflow/transactions.read` e `cashflow/consolidations.read`. No Keycloak, mappers do realm local emitem as permissões equivalentes. Um adaptador explícito converte claims externos para permissões lógicas; regras de negócio não dependem do formato de um provedor.

## Validação e identidade

Aceitar somente access token assinado com algoritmo permitido RS256. Validar emissor exato, assinatura por JWKS, expiração, `not-before`, cliente permitido e permissões requeridas. Tolerância máxima de relógio: 60 segundos. Tokens duram 15 minutos. Chave desconhecida provoca atualização controlada do cache JWKS; se a validação confiável continuar impossível, a requisição falha fechada.

Keycloak exige audience `cashflow-api` e `client_id` emitido pelo mapper aprovado. Cognito exige `token_use=access`, `client_id` permitido e scopes do resource server; `aud` é validado somente quando emitido pela configuração adotada. ID token, token de outro issuer, assinatura não permitida e claim ausente são rejeitados.

A identidade auditável e o escopo da idempotência são `issuer + client_id` normalizados a partir de claims validados. Alterar mappers, pool, issuer, clientes ou convenção de claims exige teste de compatibilidade e atualização desta decisão. Endpoints públicos de negócio exigem token; liveness não revela dependências e pode permanecer sem autenticação dentro da rede controlada; readiness e métricas não são expostas à internet.

## Credenciais e operação

Segredos de clientes não ficam no repositório, imagem ou logs. Localmente são valores sintéticos carregados pelo bootstrap do Keycloak. Na AWS ficam no Secrets Manager e são acessados apenas pelo cliente autorizado. Rotação mantém uma janela curta de coexistência quando necessária e é validada antes de revogar o segredo anterior. Tentativas 401/403, issuer, clientId e permissão requerida geram auditoria sanitizada sem registrar token.

## Alternativas e consequências

Chave compartilhada estática foi rejeitada por rotação, granularidade e auditoria insuficientes. Autenticação exclusiva no ALB foi rejeitada porque os clientes são técnicos e as APIs devem validar issuer, cliente e permissões. Usar Keycloak também na AWS aumentaria a operação; usar Cognito local exigiria conta e conectividade. Dois provedores trazem diferenças de claims, aceitas mediante adaptadores e testes obrigatórios nos dois ambientes.

Referências normativas complementares: [visão de segurança](../04-visoes/seguranca-resiliencia-observabilidade.md), [contratos HTTP](../05-arquitetura-software/contratos-http.md), [execução local](../07-infraestrutura/execucao-local.md) e [arquitetura AWS](../07-infraestrutura/arquitetura-aws.md).
