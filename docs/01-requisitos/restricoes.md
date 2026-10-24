# Restrições e limites de entrega

## Origem nos PDFs

Ambos os desafios pedem controle de lançamentos, consolidado diário, independência entre capacidades, pico de 50 req/s e perda máxima de 5% para o Consolidado. A entrega deve incluir desenho, testes, README com funcionamento/execução local, repositório público e documentação versionada. O desafio de Software exige C#; o de Soluções exige ainda capacidades/domínios, refinamento de requisitos e justificativas arquiteturais. Nem toda premissa arquitetural precisa ser codificada, mas implementação e testes permanecem itens obrigatórios.

Diferenciais do desafio de Soluções incluem custos/licenças, observabilidade, segurança de integração e Arquitetura de Transição **se necessária, considerando migração de legado**. O case não fornece um legado real. Para demonstrar a competência solicitada sem inventar uma situação existente, a entrega inclui uma [arquitetura de transição hipotética](../03-arquitetura-solucao/arquitetura-transicao-hipotetica.md) com premissas explicitamente hipotéticas. Ela é independente da implantação local→AWS.

## Estado da entrega

A entrega possui um corte local implementado e executável com C#/.NET, PostgreSQL, RabbitMQ, Keycloak e Jaeger, além da ferramenta administrativa de Recovery. O golden path final registrado em 23/10/2026 possui **90 testes aprovados, 0 falhas e 0 skips**, com build **0 warnings / 0 errors**. Ensaios operacionais executados e seus limites estão em [docs/08-testes](../08-testes/validacao-local-final.md).

A arquitetura de produção AWS está documentada, mas não foi provisionada nem homologada nesta entrega. SQS, Cognito, ECS/Fargate, RDS Multi-AZ, IAM/KMS/WAF e demais componentes AWS são **arquitetura-alvo**, não evidência executada. Não existe adaptador SQS no código atual.

Exemplos em Markdown são contratos/especificações e não constituem evidência executável por si só. O corte entre implementação, demonstração e evolução está em [escopo-implementacao.md](escopo-implementacao.md).

## Classificação usada na documentação

- **IMPLEMENTADO:** existe no código/configuração entregue.
- **DEMONSTRADO:** existe evidência de execução no ambiente declarado.
- **ARQUITETURA-ALVO:** decisão de produção projetada, ainda não implementada/homologada nesta entrega.
- **EVOLUÇÃO:** melhoria possível ou condição para adoção futura.

Nenhuma afirmação de desempenho, disponibilidade, segurança cloud ou recuperação produtiva deve ser inferida apenas do desenho. O repositório é público; isso atende ao requisito de publicação, mas não substitui validação técnica.

## Restrições técnicas adotadas

C#/.NET; PostgreSQL por contexto; RabbitMQ no ambiente local; SQS Standard como alvo AWS; dois serviços de negócio e quatro processos permanentes; domínio sem dependência direta de SDK de broker; idempotência e recuperação transacionais; configuração e segredos externos; dados sintéticos na demonstração. Valores adicionais de latência, RTO/RPO, retenção e disponibilidade são escolhas/metas arquiteturais, não requisitos inventados do cliente.

OpenAPI/Swagger não é parte da implementação entregue e não é requisito obrigatório dos PDFs. A ausência desse artefato não deve ser mascarada como trabalho concluído; pode ser evolução futura se houver necessidade de publicação formal do contrato.

## Gestão de mudança

Mudança de escopo deve atualizar requisito, ADR, contrato e teste afetado. Decisões da arquitetura-alvo podem permanecer documentadas sem implementação correspondente desde que sua classificação seja explícita e não sejam apresentadas como evidência da entrega local.
