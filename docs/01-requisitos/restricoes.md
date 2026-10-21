# Restrições

## 1. Restrições impostas pelo desafio

- A solução deve atender ao controle de lançamentos de débito e crédito e ao consolidado diário.
- O serviço de controle de lançamentos não deve ficar indisponível se o sistema de consolidado diário cair.
- Em dias de pico, o serviço de consolidado diário recebe 50 requisições por segundo, com no máximo 5% de perda de requisições.
- Na avaliação de Solutions Architect, são obrigatórios: mapeamento de domínios funcionais e capacidades de negócio, refinamento de requisitos funcionais e não funcionais, arquitetura-alvo completa, justificativa das escolhas de ferramentas/tecnologias e tipo de arquitetura, testes, README, repositório público e documentação no repositório.
- Na avaliação de Software Architect, são obrigatórios: desenho da solução, implementação em C#, testes, README, repositório público e documentação no repositório. Boas práticas como Design Patterns, padrões arquiteturais e SOLID são explicitamente valorizadas.
- O desafio permite que aspectos arquiteturais relevantes sejam demonstrados por decisões e representações arquiteturais, sem exigir que todas as premissas estejam codificadas.

## 2. Diferenciais explicitamente solicitados para Solutions Architect

Devem ser considerados na entrega:
- arquitetura de transição, somente se houver cenário de legado/migração que a justifique;
- estimativa de custos de infraestrutura e licenças;
- monitoramento e observabilidade;
- critérios de segurança para consumo e integração de serviços.

## 3. Restrições e critérios da estratégia de entrega

Os itens abaixo orientam a execução do desafio, mas não são apresentados como requisitos de negócio:

- O ambiente executável local deverá ser simples e reproduzível, preferencialmente containerizado.
- Configurações devem ser externalizadas e segredos reais não podem ser versionados.
- A implementação que atender à avaliação de Software Architect será feita em C#.
- O domínio e os casos de uso devem evitar acoplamento desnecessário a tecnologias de infraestrutura.
- Tecnologias de persistência, integração, cloud, padrões e estilo arquitetural serão formalmente escolhidos em ADRs após análise dos requisitos e ASRs.
- Funcionalidades não exigidas pelo desafio somente serão implementadas quando contribuírem para requisito, ASR, risco ou critério de avaliação.
- A documentação deve distinguir claramente: `IMPLEMENTADO`, `VALIDADO`, `ARQUITETURA-ALVO` e `EVOLUÇÃO`.

## 4. Restrições de processo do repositório

- Após o bootstrap inicial autorizado, alterações devem ocorrer por branch e Pull Request.
- Pull Requests não devem ser integrados à `main` sem revisão e aprovação explícita do responsável pelo projeto.
- Antes da abertura de novos Pull Requests, o conjunto de alterações deve ser revisado integralmente contra o desafio e contra a rastreabilidade existente.