# Restrições

## Restrições do desafio

- A solução deve atender ao problema de lançamentos de débito/crédito e consolidação diária.
- A indisponibilidade da consolidação não pode causar indisponibilidade do controle de lançamentos.
- O cenário de pico informado para o consolidado é de 50 requisições por segundo, com tolerância máxima de perda indicada pelo desafio de 5%.
- Para a perspectiva de Software Architect, a implementação deve utilizar C#.
- Devem existir testes.
- O repositório deve ser público e conter a documentação do projeto.
- O README deve permitir compreender e executar a solução localmente.

## Restrições definidas para a entrega

- O ambiente local será reproduzível com Docker Compose.
- PostgreSQL e Kafka utilizarão volumes persistentes no ambiente local.
- Configurações serão externalizadas e um `.env.example` será fornecido.
- Segredos reais não serão versionados.
- Transaction Service e Consolidation Service serão processos/deployments independentes.
- O projeto evitará dependência direta do domínio em tecnologias de infraestrutura.
- AWS será a referência detalhada da arquitetura produtiva; Azure e GCP serão documentados como alternativas por capacidade.
- Funcionalidades não exigidas pelo desafio somente serão implementadas quando necessárias para satisfazer requisito, ASR, risco ou critério de avaliação.

## Restrições de processo

- Alterações do projeto, após o bootstrap inicial do repositório, devem ocorrer por branch e Pull Request.
- Pull Requests não devem ser integrados à `main` sem revisão e aprovação explícita do responsável pelo projeto.