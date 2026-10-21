# Architecturally Significant Requirements (ASR)

## Objetivo

ASRs são requisitos cuja satisfação influencia de forma significativa a estrutura, integração, dados, segurança, operação, desempenho, disponibilidade ou custo da solução.

Este documento registra os drivers arquiteturais e suas consequências. Ele não escolhe tecnologias ou padrões antes da análise de alternativas; essas decisões serão formalizadas em ADRs.

## ASR-001 — Independência entre lançamento e consolidação

**Origem:** requisito não funcional explícito do desafio / RNF-001.

A indisponibilidade da capacidade de consolidação não pode impedir o registro de novos lançamentos.

**Impactos arquiteturais:** segregação de responsabilidades e falhas; ausência de dependência síncrona da consolidação no caminho crítico de escrita; capacidade de interromper, recuperar e escalar a consolidação sem interromper o registro de lançamentos.

## ASR-002 — Durabilidade após confirmação do lançamento

**Origem:** RNF-004 e PRE-001.

Após confirmar sucesso ao consumidor, a solução deve preservar o lançamento e a informação necessária para que ele seja posteriormente refletido no consolidado, inclusive diante de falhas intermediárias.

**Impactos arquiteturais:** persistência durável; tratamento explícito da consistência entre gravação e disponibilização para processamento; recuperação de falhas; análise de atomicidade e dos riscos de dual write. O mecanismo concreto será decidido em ADR.

## ASR-003 — Desacoplamento temporal e convergência do consolidado

**Origem:** RNF-001, RNF-005 e PRE-003.

Para preservar a independência entre as capacidades, o consolidado pode permanecer temporariamente defasado, mas deve convergir após a recuperação do processamento.

**Impactos arquiteturais:** comunicação desacoplada no tempo, processamento recuperável, indicação de freshness/última atualização e monitoramento do atraso de processamento. A tecnologia de integração será definida em ADR.

## ASR-004 — Idempotência e proteção contra dupla contabilização

**Origem:** RNF-006 e PRE-004.

Tentativas repetidas de uma mesma operação ou reprocessamentos não podem criar lançamentos duplicados nem aplicar o mesmo efeito financeiro mais de uma vez ao consolidado.

**Impactos arquiteturais:** identificação estável das operações/mensagens, persistência do controle necessário à deduplicação, constraints adequadas e atomicidade entre alteração da projeção e registro do processamento quando aplicável. A estratégia concreta será definida no design e em ADR quando arquiteturalmente significativa.

## ASR-005 — Recuperação após indisponibilidade da consolidação

**Origem:** RNF-007.

Após a recuperação da consolidação, o trabalho acumulado deve ser processado até que a projeção volte a refletir os lançamentos registrados, sem perda permanente ou dupla contabilização.

**Impactos arquiteturais:** retenção suficiente da informação pendente, controle da posição/estado de processamento, retries controlados, tratamento de falhas persistentes e observabilidade de backlog e atraso. O mecanismo de mensageria e recuperação será definido em ADR.

## ASR-006 — Pico de 50 requisições por segundo no consolidado

**Origem:** requisito não funcional explícito do desafio / RNF-002 e RNF-003.

A arquitetura deve considerar o pico informado de 50 requisições por segundo para o serviço de consolidado, com no máximo 5% de perda de requisições conforme enunciado.

**Impactos arquiteturais:** dimensionamento orientado a métricas, capacidade de escalabilidade horizontal quando aplicável, ausência de estado de sessão dependente da instância, persistência e consultas eficientes e teste de carga que reproduza o cenário. O teste local será evidência do comportamento do MVP, não certificação de capacidade produtiva.

## ASR-007 — Segurança de consumo, integração e dados

**Origem:** objetivo do desafio, RNF-009 e diferencial de segurança da avaliação de Solutions Architect.

A solução deve proteger APIs, integrações, dados e segredos contra acesso indevido e ataques, considerando autenticação, autorização, criptografia e mecanismos de proteção.

**Impactos arquiteturais:** definição de trust boundaries; autenticação e autorização; proteção do tráfego; gestão de segredos e chaves; least privilege; proteção de borda conforme o ambiente; auditoria; e critérios específicos para comunicação serviço-a-serviço. Tecnologias e serviços concretos serão definidos na arquitetura-alvo e ADRs.

## ASR-008 — Escalabilidade, disponibilidade e isolamento operacional

**Origem:** objetivo do desafio, RNF-001, RNF-008 e RNF-014.

As capacidades de lançamento e consolidação devem permitir evolução, implantação e dimensionamento coerentes com seus perfis de carga e disponibilidade, preservando o isolamento de falhas exigido pelo desafio.

**Impactos arquiteturais:** componentes com responsabilidades bem definidas; execução sem estado de sessão local quando necessário; health checks; balanceamento e redundância na arquitetura-alvo; estratégia de implantação e scaling independentes quando essa decomposição for confirmada pelo ADR de estilo arquitetural.

## ASR-009 — Observabilidade e rastreabilidade ponta a ponta

**Origem:** RNF-010, RNF-011 e diferencial de monitoramento/observabilidade da avaliação de Solutions Architect.

A operação deve permitir acompanhar uma solicitação desde o registro do lançamento até sua reflexão no consolidado e identificar falhas ou degradação.

**Impactos arquiteturais:** logs estruturados e correlacionáveis, métricas de taxa/erro/latência, itens pendentes, retries, backlog/atraso de processamento, falhas persistentes, freshness da consolidação, health checks, tracing quando aplicável e alertas baseados em metas operacionais.

## ASR-010 — Recuperação de desastre e continuidade dependem de metas de negócio

**Origem:** lacunas QRNF-001, QRNF-002, QRNF-003 e QRNF-008.

SLA/SLO, RTO, RPO e requisitos de disaster recovery não foram informados pelo desafio e não devem ser inventados como compromissos produtivos.

**Impactos arquiteturais:** a arquitetura-alvo deverá explicitar os mecanismos possíveis de alta disponibilidade, backup, restore, replicação e recuperação, indicando quais decisões dependem da definição dessas metas e como custo/complexidade variam conforme os objetivos estabelecidos.

## ASRs pendentes de metas quantitativas

Ainda precisam de definição com stakeholders:
- SLA/SLO de disponibilidade;
- RTO;
- RPO;
- latência P95/P99;
- volume e crescimento de lançamentos;
- retenção e arquivamento;
- disaster recovery regional;
- requisitos regulatórios e de localização dos dados;
- janela máxima de defasagem do consolidado.

Essas lacunas permanecem registradas em `docs/01-requisitos/questoes-em-aberto.md`.