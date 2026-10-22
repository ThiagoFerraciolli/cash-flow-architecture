# ADR-002 — Integração assíncrona entre Lançamentos e Consolidação

- **Status:** ACEITO PARA A ARQUITETURA DO DESAFIO
- **Drivers:** RNF-001, RNF-005, RNF-007, ASR-001, ASR-003, ASR-005
- **Dependência:** ADR-001

## Contexto

O registro de lançamentos deve permanecer operacional durante indisponibilidade da consolidação. Portanto, a confirmação de um lançamento não pode depender de chamada síncrona para o serviço de Consolidação.

A integração precisa desacoplar temporalmente os serviços, preservar trabalho pendente durante indisponibilidades, permitir retentativa e oferecer tratamento explícito para mensagens que não possam ser processadas.

O desafio informa 50 req/s no serviço de consolidação, mas não exige retenção histórica de eventos, replay arbitrário, múltiplos grupos de consumidores, ordenação global ou plataforma de event streaming.

## Alternativas avaliadas

### A. HTTP síncrono

Rejeitado. Mantém acoplamento de disponibilidade/latência entre os serviços e enfraquece diretamente o RNF-001.

### B. Consulta direta/polling da base de Lançamentos

Rejeitado. Evita broker, mas viola ownership de dados, aumenta acoplamento entre contextos e dificulta evolução independente.

### C. Fila gerenciada — Amazon SQS na arquitetura AWS

Atende os drivers atuais com menor complexidade operacional: entrega assíncrona, armazenamento durável durante indisponibilidade do consumidor, retentativa, visibility timeout, DLQ nativa e escalabilidade suficiente para a carga conhecida.

### D. Log distribuído — Apache Kafka/Amazon MSK

Também atende ao desacoplamento e adiciona retenção configurável, offsets, múltiplos grupos de consumidores e replay. Essas capacidades têm valor quando existem requisitos de event streaming, fan-out independente ou replay histórico, mas nenhum deles é exigido pelo desafio atual. O custo e a complexidade operacional são maiores.

## Decisão

Adotar **integração assíncrona orientada a eventos usando fila**, com **Amazon SQS Standard** como tecnologia da arquitetura-alvo AWS e um broker compatível para execução local.

A escolha decorre dos requisitos conhecidos, e não de preferência tecnológica. SQS satisfaz independência entre os serviços, recuperação de backlog, retentativa e isolamento de falhas sem introduzir capacidades não requeridas apenas para justificar Kafka.

O padrão Transactional Outbox permanece necessário: SQS não elimina a janela de dual write entre PostgreSQL e o broker.

## Semântica

- entrega: **at-least-once**;
- não há promessa de exactly-once ponta a ponta;
- consumidor obrigatoriamente idempotente;
- SQS Standard não garante ordenação global, e o domínio atual não exige ordenação para somas de débito/crédito;
- mensagens que excederem a política de recebimentos são encaminhadas à DLQ por redrive policy;
- a confirmação do lançamento depende apenas do commit local de lançamento + Outbox, nunca da disponibilidade de SQS ou da Consolidação.

## Consequências

- o consolidado pode ficar temporariamente defasado e converge após o processamento das pendências;
- backlog, idade da mensagem mais antiga, número de recebimentos e mensagens na DLQ precisam ser observados;
- o contrato de eventos precisa ser versionado;
- deve existir procedimento de redrive/reprocessamento da DLQ e reconciliação entre lançamentos e consolidado;
- a aplicação acessará mensageria por portas/adaptadores para evitar acoplamento do domínio à SQS;
- Kafka/MSK permanece alternativa de evolução se requisitos de replay histórico, múltiplos consumidores independentes ou event streaming forem confirmados no futuro.

## Evidência esperada

Os testes devem demonstrar que a Consolidação e/ou a fila podem ficar indisponíveis sem impedir a persistência de novos lançamentos; após recuperação, as mensagens pendentes são publicadas/processadas e o consolidado converge sem dupla contabilização.