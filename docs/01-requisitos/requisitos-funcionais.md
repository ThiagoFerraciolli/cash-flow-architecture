# Requisitos Funcionais

## 1. Objetivo

Este documento registra os requisitos funcionais identificados para o MVP de controle de fluxo de caixa. O escopo foi mantido intencionalmente aderente ao desafio: registro de lançamentos de débito e crédito e disponibilização de consolidado diário.

Requisitos não informados pelo desafio não são tratados como fatos. Quando necessários para implementação, são registrados como premissas; quando dependem de decisão de negócio, são mantidos como questões em aberto.

## 2. Escopo funcional do MVP

### RF-001 — Autenticar usuário
O sistema deve permitir que um usuário se autentique para consumir as operações protegidas da aplicação.

**Observação:** autenticação foi adicionada como requisito de segurança da solução. Autorização baseada em papéis e permissões não faz parte do MVP.

### RF-002 — Registrar lançamento financeiro
O sistema deve permitir o registro de um lançamento financeiro.

Cada lançamento deve possuir, no mínimo:
- identificador único;
- tipo da operação: `DEBIT` ou `CREDIT`;
- valor;
- descrição opcional;
- data/hora de ocorrência;
- data/hora de criação;
- usuário responsável pela criação.

### RF-003 — Registrar crédito
O sistema deve permitir registrar um lançamento do tipo crédito.

### RF-004 — Registrar débito
O sistema deve permitir registrar um lançamento do tipo débito.

### RF-005 — Consultar lançamento por identificador
O sistema deve permitir consultar um lançamento específico pelo seu identificador.

### RF-006 — Consultar lançamentos
O sistema deve permitir consultar lançamentos registrados, com paginação e filtros compatíveis com o MVP, incluindo período e tipo da operação.

### RF-007 — Processar consolidação diária
O sistema deve processar os eventos de lançamentos e criar ou atualizar o consolidado correspondente ao dia financeiro do lançamento.

O processamento do consolidado não é iniciado manualmente pelo usuário por meio de uma operação de cadastro pública; ele é consequência do processamento assíncrono dos eventos financeiros.

### RF-008 — Consultar consolidado diário
O sistema deve permitir consultar o consolidado de uma determinada data.

O consolidado deve apresentar, no mínimo:
- total de créditos;
- total de débitos;
- saldo diário;
- quantidade de lançamentos processados;
- data/hora da última consolidação;
- indicador de estado/freshness quando aplicável.

### RF-009 — Consultar consolidados por período
O sistema deve permitir consultar consolidados diários em um intervalo de datas, com paginação quando aplicável.

### RF-010 — Registrar evento para publicação
Ao confirmar um lançamento financeiro, o sistema deve registrar de forma atômica a intenção de publicação do respectivo evento para processamento assíncrono.

Esse registro compõe o mecanismo Transactional Outbox e deve ocorrer na mesma transação local do lançamento financeiro.

### RF-011 — Publicar eventos pendentes
O sistema deve identificar eventos ainda não publicados e encaminhá-los ao mecanismo de mensageria, mantendo informação suficiente para rastrear o estado da publicação e suas falhas.

### RF-012 — Registrar evento processado
O serviço de consolidação deve manter controle dos eventos já aplicados ao consolidado para impedir dupla contabilização em caso de reentrega da mensagem.

O registro do evento processado e a alteração do consolidado devem ocorrer atomicamente na mesma transação local.

## 3. Regras mínimas conhecidas

- O valor de um lançamento deve ser maior que zero.
- O tipo deve ser `DEBIT` ou `CREDIT`.
- Um lançamento confirmado é tratado como registro financeiro imutável no MVP.
- O saldo diário é calculado como `total de créditos - total de débitos`.
- O consolidado é uma projeção derivada; os lançamentos financeiros constituem a fonte primária dos dados.

## 4. Fora do escopo do MVP

Não serão implementados inicialmente:
- cadastro de comerciante/multi-tenant;
- cadastro de caixa;
- abertura e fechamento de caixa;
- estorno;
- edição ou exclusão de lançamento financeiro;
- RBAC completo;
- relatórios financeiros adicionais;
- exportações.

Esses itens podem ser avaliados como evoluções funcionais após definição de requisitos de negócio.