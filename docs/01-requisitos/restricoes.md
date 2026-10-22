# Restrições e limites de entrega

## Origem nos PDFs

Ambos pedem controle de lançamentos, consolidado diário, independência entre capacidades, 50 req/s e perda máxima de 5%. A entrega final deve incluir desenho, testes, README com funcionamento e execução local, repositório público e documentação versionada. Software exige C#; Soluções permite linguagem dominada e exige capacidades/domínios, refinamento e justificativas. A permissão para representar aspectos arquiteturais sem codificá-los não elimina os obrigatórios de aplicação e testes.

Diferenciais de Soluções: custos/licenças, observabilidade, segurança de integração e transição se houver legado. Não há legado conhecido; transição de legado é não aplicável.

## Escopo desta branch

Somente documentação em português. Todas as decisões de texto estão aprovadas. Não iniciar C#, Docker Compose executável, migrations, scripts de teste, Terraform ou provisionamento até finalizar esta etapa. Exemplos em Markdown são contratos/documentação, não implementação.

Estados são separados: DOCUMENTAÇÃO APROVADA, IMPLEMENTAÇÃO PENDENTE e EVIDÊNCIA PENDENTE. Não marcar desempenho, disponibilidade ou execução local como demonstrados por existir um desenho. Repositório público foi confirmado em 22/10/2026 via metadados GitHub.

## Restrições técnicas aprovadas

C#/.NET; PostgreSQL por contexto; RabbitMQ local; SQS AWS; dois serviços/quatro processos; domínio sem dependência de SDK; idempotência e recuperação transacionais; identidade e segurança obrigatórias; dados sintéticos na demonstração. Configurações e segredos externos. Valores e metas de referência são escolhas da arquitetura, não requisitos inventados do cliente.

## Gestão de mudança

Trabalhar na branch atual. Aprovação documental autoriza concluir os textos, não implica abrir PR, fazer merge ou provisionar. Mudança de escopo posterior deve atualizar requisito, ADR, contrato e teste afetado. A etapa seguinte implementará o desenho aprovado e acrescentará evidências verificáveis.
