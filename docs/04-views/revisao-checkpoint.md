# Revisão do checkpoint — Views, Cloud e Custos

## Escopo revisado

Este checkpoint foi revisado contra a fundação já aprovada (`RF/RNF`, premissas, questões abertas, ASRs, DAS e ADRs 001–005) e contra a matriz de conformidade dos desafios de Solutions Architect e Software Architect.

## Solutions Architect

- [x] C4 Context e Container;
- [x] visão de dados e ownership;
- [x] visão de integração e contratos;
- [x] segurança para consumo/integração;
- [x] resiliência e recuperação;
- [x] observabilidade;
- [x] diagramas dinâmicos dos fluxos críticos;
- [x] arquitetura-alvo AWS;
- [x] justificativa ECS/Fargate versus EKS;
- [x] estimativa preliminar de infraestrutura/licenciamento;
- [x] trade-off Kafka/MSK versus SQS;
- [x] nenhuma meta de SLA/RTO/RPO inventada;
- [x] 50 req/s mantido como requisito explícito sem inventar operação.

## Software Architect

Este checkpoint não encerra o desafio de Software Architect. Permanecem para o próximo bloco:
- SDD;
- C4 Component;
- estrutura interna .NET/C#;
- SOLID/patterns aplicados;
- contratos concretos;
- implementação e testes.

As views atuais preservam fronteiras adequadas para essa implementação e não impõem dependência de domínio à AWS.

## Consistência

- Transactions continua independente da disponibilidade de Consolidation;
- confirmação de lançamento depende apenas da transação local de lançamento + outbox;
- processamento é at-least-once + idempotente, sem promessa de exactly-once;
- bancos possuem ownership separado;
- não existe transação distribuída;
- não foi inventada partition key;
- não há requisito atual de ordenação global para soma de débitos/créditos;
- Kafka é defendido por replay/retention/recovery, não por 50 req/s;
- SQS é registrado como alternativa de menor TCO;
- custos são apresentados como estimativa, não orçamento definitivo;
- cenário de transição hipotético do checkpoint anterior permanece separado dos requisitos reais.

## Pendências deliberadas para etapas seguintes

- README final e execução local;
- C# e testes;
- evidência do cenário de consolidação indisponível;
- teste de carga 50 req/s / <=5% conforme premissa documentada;
- C4 Component/SDD;
- contratos finais e migrations;
- resultados medidos de performance;
- evolução futura.

## Resultado

**Sem blocker arquitetural identificado para abertura do PR deste checkpoint.**

A aprovação deste PR não significa conclusão do desafio; significa aprovação das views de solução, arquitetura cloud e abordagem de custo que orientarão o design/implementação subsequentes.