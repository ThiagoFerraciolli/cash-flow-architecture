# Requisitos Não Funcionais

## 1. Objetivo e classificação

Este documento separa o que foi **explicitamente informado pelo desafio** do que foi **refinado para orientar a solução**. Metas não fornecidas, como SLA/SLO, RTO, RPO e latência P95/P99, não são inventadas.

Classificação utilizada:
- **EXPLÍCITO**: requisito informado pelo desafio;
- **REFINADO**: requisito derivado dos objetivos técnicos, do requisito explícito ou de riscos necessários para tornar a solução verificável;
- **PENDENTE**: atributo relevante cuja meta depende de validação com stakeholders.

## 2. RNFs explícitos do desafio

### RNF-001 — Independência entre lançamento e consolidação — EXPLÍCITO
O serviço de controle de lançamentos não deve ficar indisponível se o sistema de consolidado diário cair.

**Critério de aceite do MVP:** durante a indisponibilidade da consolidação, novas solicitações válidas de lançamento continuam podendo ser aceitas e persistidas pelo componente responsável por lançamentos.

### RNF-002 — Pico do consolidado — EXPLÍCITO
Em dias de pico, o serviço de consolidado diário recebe 50 requisições por segundo.

**Critério de aceite do MVP:** executar teste de carga reproduzível a 50 req/s sobre a operação do consolidado definida no plano de teste e registrar taxa de sucesso/erro e latência observada. Como o enunciado não informa qual operação recebe essa carga, essa definição deverá ser explicitada como premissa do teste e permanece como questão aberta `QRNF-015`. O resultado local demonstra o comportamento do MVP e não constitui certificação de capacidade produtiva.

### RNF-003 — Perda máxima de requisições do consolidado — EXPLÍCITO
No cenário de pico, o desafio admite no máximo 5% de perda de requisições do serviço de consolidado diário.

**Critério de aceite do MVP:** no teste de 50 req/s, a taxa de requisições sem resposta válida por erro/perda atribuível à aplicação deve ser menor ou igual a 5%.

**Observação:** o enunciado não define a semântica de “perda”. A solução não interpreta esse limite como autorização para perda permanente de lançamentos financeiros já confirmados; essa interpretação permanece registrada como premissa e questão em aberto `QRNF-013`.

## 3. RNFs refinados para a solução

### RNF-004 — Durabilidade após confirmação — REFINADO
Após confirmar a criação de um lançamento, uma falha intermediária não deve provocar perda permanente do lançamento nem impedir definitivamente que ele seja refletido no consolidado.

O mecanismo para satisfazer esse requisito será decidido por ADR após análise de alternativas. O RPO formal de produção permanece pendente.

### RNF-005 — Convergência do consolidado — REFINADO
O consolidado pode apresentar defasagem temporária para preservar a independência exigida pelo RNF-001, mas deve convergir após a recuperação do processamento.

A janela máxima aceitável de defasagem não foi informada e permanece pendente.

### RNF-006 — Idempotência — REFINADO
Tentativas repetidas da mesma operação e reprocessamentos não devem causar duplicidade de lançamento nem dupla contabilização no consolidado.

### RNF-007 — Recuperação do processamento — REFINADO
Após a recuperação da capacidade de consolidação, o trabalho pendente deve voltar a ser processado até a convergência, com tratamento controlado de falhas persistentes.

### RNF-008 — Escalabilidade — REFINADO A PARTIR DO OBJETIVO DO DESAFIO
A arquitetura deve permitir aumento de capacidade sem exigir alteração das regras de negócio e deve avaliar dimensionamento horizontal, balanceamento de carga e cache conforme necessidade demonstrada.

Cache não será introduzido apenas por sofisticação; sua adoção depende de benefício e consistência compatíveis com o caso.

### RNF-009 — Segurança — REFINADO A PARTIR DO OBJETIVO E DO DIFERENCIAL
A solução deve proteger APIs, integrações, dados e segredos. Deve considerar autenticação, autorização, criptografia em trânsito e em repouso no ambiente-alvo, gestão de segredos, least privilege, proteção contra ataques e auditoria.

Os critérios de segurança de consumo e integração entre serviços deverão ser detalhados na arquitetura-alvo, atendendo ao diferencial explícito do desafio de Solutions Architect.

### RNF-010 — Observabilidade — REFINADO A PARTIR DO DIFERENCIAL
A solução deve permitir monitoramento e diagnóstico do fluxo ponta a ponta.

Devem ser observáveis, conforme aplicável à tecnologia escolhida:
- taxa de requisições, erros e latência;
- estado de saúde dos componentes;
- trabalho pendente e atraso de processamento;
- retries e falhas persistentes;
- freshness/última atualização do consolidado;
- correlação entre lançamento e processamento;
- utilização/saturação dos recursos relevantes.

### RNF-011 — Auditabilidade — REFINADO
Operações financeiras devem manter informação suficiente para identificar o lançamento, instante de criação e identidade responsável quando disponível, sem expor dados sensíveis indevidamente em logs.

### RNF-012 — Configuração e segredos — REFINADO
Configurações dependentes de ambiente devem ser externalizadas e segredos reais não devem ser versionados nem incorporados às imagens da aplicação.

### RNF-013 — Isolamento de falhas e operação — REFINADO
A arquitetura deve permitir demonstrar que a indisponibilidade da consolidação não interrompe o registro de lançamentos. A decomposição física/deployment utilizada para isso será definida pelo ADR de estilo arquitetural e pela arquitetura-alvo.

### RNF-014 — Baixo acoplamento às tecnologias de infraestrutura — REFINADO
Regras de domínio e casos de uso devem permanecer desacoplados de detalhes específicos de persistência, mensageria e cloud sempre que essa abstração trouxer benefício de testabilidade, manutenção ou portabilidade.

Isso não implica equivalência semântica entre diferentes produtos ou serviços de infraestrutura.

## 4. Atributos relevantes sem meta definida — PENDENTE

Precisam de definição antes de compromissos produtivos:
- SLA/SLO de disponibilidade;
- RTO por capacidade;
- RPO por categoria de dado;
- latência P95/P99;
- pico de escrita de lançamentos;
- volume diário/anual e crescimento;
- retenção e arquivamento;
- janela máxima de defasagem do consolidado;
- estratégia de disaster recovery e requisito multi-região;
- requisitos regulatórios/compliance e LGPD específicos;
- localização geográfica dos dados;
- requisitos corporativos de identidade e autorização.

As perguntas correspondentes estão registradas em `questoes-em-aberto.md`.