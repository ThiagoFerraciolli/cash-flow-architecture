# Requisitos Não Funcionais

## 1. Objetivo

Este documento registra requisitos não funcionais fornecidos pelo desafio e refinamentos arquiteturais necessários para orientar a solução. Metas não fornecidas, como RTO, RPO, SLA e latência P95/P99, não são inventadas: permanecem registradas como questões em aberto.

## 2. Requisitos do desafio e refinamentos

### RNF-001 — Independência entre lançamento e consolidação
A indisponibilidade do serviço de consolidação não deve tornar indisponível o serviço responsável pelo registro de lançamentos.

A solução deve permitir que novos lançamentos continuem sendo aceitos e persistidos enquanto a consolidação estiver indisponível.

### RNF-002 — Carga do consolidado
O serviço de consolidação deve ser projetado considerando o pico informado de 50 requisições por segundo.

O teste local de carga deve demonstrar o comportamento da aplicação nesse cenário, sem ser apresentado como garantia de capacidade do ambiente produtivo.

### RNF-003 — Tolerância de perda informada pelo desafio
O desafio informa tolerância máxima de 5% de perda de requisições no cenário de pico do consolidado.

Para esta solução, essa tolerância não será interpretada como autorização para perda permanente de lançamentos financeiros já confirmados. A interpretação deve ser validada com o negócio antes da produção.

### RNF-004 — Durabilidade de lançamento confirmado
Após o sistema confirmar a criação de um lançamento, a solução deve impedir que uma falha entre a persistência do lançamento e a disponibilização da informação necessária à consolidação resulte em perda permanente do processamento correspondente.

O mecanismo arquitetural para satisfazer esse requisito será avaliado e formalizado em ADR, considerando alternativas e trade-offs. A garantia formal de RPO produtivo depende de requisitos de recuperação e infraestrutura ainda não informados.

### RNF-005 — Consistência eventual
A projeção de consolidado diário pode apresentar atraso temporário em relação aos lançamentos registrados. O modelo entre lançamento e consolidação é de consistência eventual.

A fonte primária é o conjunto de lançamentos; o consolidado é uma projeção derivada.

### RNF-006 — Idempotência
A solução deve evitar duplicidade causada por retries do cliente e por reentrega do mecanismo de integração.

Devem existir mecanismos de idempotência tanto na entrada de operações críticas quanto no processamento assíncrono.

### RNF-007 — Recuperação de backlog
Após a recuperação do serviço de consolidação, itens pendentes de processamento devem voltar a ser processados sem dupla contabilização.

### RNF-008 — Escalabilidade
Os serviços devem ser desenhados para permitir escalabilidade horizontal, evitando estado de sessão dependente da instância da aplicação.

A arquitetura-alvo deve considerar balanceamento de carga e autoscaling de acordo com métricas e limites posteriormente definidos.

### RNF-009 — Segurança
As APIs protegidas devem exigir autenticação. Segredos não devem ser armazenados no código-fonte.

O ambiente produtivo deve considerar, conforme arquitetura-alvo e requisitos corporativos: Identity Provider com OAuth2/OIDC, TLS, WAF, API Gateway, gestão de segredos, criptografia, IAM/least privilege, rede privada e auditoria.

### RNF-010 — Observabilidade
A solução deve permitir observação do fluxo ponta a ponta por logs estruturados, métricas e correlação/tracing.

Devem ser observáveis, no mínimo:
- taxa de requisições;
- taxa de erros;
- latência;
- itens pendentes de publicação/processamento;
- falhas e retries de integração;
- atraso do processamento assíncrono;
- falhas de consumo/processamento;
- itens em tratamento de erro/DLQ, quando aplicável;
- freshness da consolidação;
- health dos serviços.

As métricas específicas da tecnologia selecionada, como Outbox pending ou Kafka consumer lag, serão detalhadas após as respectivas decisões arquiteturais.

### RNF-011 — Auditabilidade
Operações financeiras devem possuir informação suficiente para identificar o lançamento, data/hora e usuário responsável, respeitando princípios de minimização e proteção de dados sensíveis nos logs.

### RNF-012 — Persistência do ambiente local
Dados persistentes utilizados pelo ambiente Docker local devem sobreviver à recriação dos containers por meio de volumes apropriados.

A persistência local não substitui estratégia produtiva de backup, alta disponibilidade ou disaster recovery. Os componentes específicos serão detalhados após as decisões arquiteturais correspondentes.

### RNF-013 — Configuração externa
Configurações de ambiente devem ser externalizadas. O projeto deve disponibilizar `.env.example` versionado e manter `.env` fora do controle de versão.

Em produção, segredos devem utilizar serviço apropriado de gestão de segredos e identidade, e não arquivos `.env` embarcados nas imagens.

### RNF-014 — Implantação independente
O componente responsável pelos lançamentos e o componente responsável pela consolidação devem poder ser construídos, executados, interrompidos e implantados independentemente.

Isso deve permitir demonstrar localmente a indisponibilidade da consolidação sem interromper o registro de lançamentos.

### RNF-015 — Portabilidade e baixo acoplamento de infraestrutura
Regras de domínio e casos de uso não devem depender diretamente de implementações proprietárias de banco, mensageria ou cloud. Dependências externas devem permanecer nas bordas da aplicação, respeitando abstrações justificadas.

Isso não implica que tecnologias diferentes, especialmente mecanismos de mensageria, possuam semântica equivalente.

## 3. Metas não definidas pelo desafio

Os seguintes atributos de qualidade precisam de definição antes de uma arquitetura produtiva ser considerada completamente dimensionada:
- SLA/SLO de disponibilidade;
- RTO por capacidade;
- RPO por categoria de dado;
- latência P95/P99;
- volume máximo de lançamentos;
- crescimento esperado;
- retenção e arquivamento;
- estratégia de disaster recovery;
- requisitos regulatórios/compliance;
- requisitos de localização geográfica dos dados.

Essas lacunas são detalhadas em `questoes-em-aberto.md`.
