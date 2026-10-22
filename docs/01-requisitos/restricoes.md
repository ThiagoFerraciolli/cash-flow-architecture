# Restrições e limites de entrega

## Origem nos PDFs

Ambos pedem controle de lançamentos, consolidado diário, independência entre capacidades, 50 req/s e perda máxima de 5%. A entrega final deve incluir desenho, testes, README com funcionamento e execução local, repositório público e documentação versionada. Software exige C#; Soluções permite linguagem dominada e exige capacidades/domínios, refinamento e justificativas. A permissão para representar aspectos arquiteturais sem codificá-los não elimina os obrigatórios de aplicação e testes.

Diferenciais de Soluções: custos/licenças, observabilidade, segurança de integração e transição se houver legado. Não há legado conhecido; transição de legado é não aplicável.

## Estado da entrega

A documentação arquitetural está concluída para o escopo definido e a implementação ocorre incrementalmente. Exemplos em Markdown são contratos ou especificações e não constituem evidência executável por si só. O corte entre implementação, homologação e evolução está em [escopo-implementacao.md](escopo-implementacao.md).

Estados são separados: DOCUMENTADO, IMPLEMENTADO e DEMONSTRADO. Não marcar desempenho, disponibilidade ou execução local como demonstrados apenas pela existência do desenho. Repositório público foi confirmado em 22/10/2026 via metadados GitHub.

## Restrições técnicas aprovadas

C#/.NET; PostgreSQL por contexto; RabbitMQ local; SQS AWS; dois serviços/quatro processos; domínio sem dependência de SDK; idempotência e recuperação transacionais; identidade e segurança conforme o escopo de implementação; dados sintéticos na demonstração. Configurações e segredos externos. Valores e metas de referência são escolhas da arquitetura, não requisitos inventados do cliente.

## Gestão de mudança

Mudança de escopo deve atualizar requisito, ADR, contrato e teste afetado. Decisões da arquitetura-alvo podem permanecer documentadas mesmo quando sua implementação estiver classificada como homologação ou evolução; nesses casos, a documentação deve deixar explícito que não existe evidência executável correspondente.
