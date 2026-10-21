# Requisitos Funcionais

## 1. Objetivo e origem

O desafio fornece dois requisitos de negócio de alto nível:
1. serviço que faça o controle de lançamentos;
2. serviço do consolidado diário.

Os requisitos abaixo refinam essas duas capacidades sem apresentar regras não informadas como fatos. Regras desconhecidas permanecem em `questoes-em-aberto.md` e hipóteses necessárias ao MVP em `premissas.md`.

Decisões como banco de dados, broker, Transactional Outbox, padrões de processamento ou cloud não são requisitos funcionais e serão avaliadas posteriormente na arquitetura e nos ADRs.

## 2. Requisitos de negócio explícitos

### RN-001 — Controlar lançamentos
A solução deve oferecer a capacidade de controlar lançamentos de débito e crédito do fluxo de caixa diário.

### RN-002 — Disponibilizar consolidado diário
A solução deve oferecer a capacidade de disponibilizar o saldo diário consolidado a partir dos lançamentos financeiros.

## 3. Refinamento funcional do MVP

### RF-001 — Registrar lançamento financeiro
**Origem:** RN-001.

O sistema deve permitir registrar um lançamento financeiro de débito ou crédito.

Dados mínimos adotados para o MVP:
- identificador único;
- tipo: `DEBIT` ou `CREDIT`;
- valor;
- descrição opcional;
- data/hora de ocorrência;
- data/hora de criação.

A identificação do usuário responsável poderá ser registrada quando a autenticação estiver habilitada, conforme decisão de segurança da solução.

### RF-002 — Consultar lançamento por identificador
**Origem:** RN-001.

O sistema deve permitir consultar um lançamento registrado pelo seu identificador.

### RF-003 — Consultar lançamentos
**Origem:** RN-001.

O sistema deve permitir consultar lançamentos registrados. Para tornar a operação utilizável e testável no MVP, a consulta deverá suportar paginação e filtros básicos por período e tipo.

### RF-004 — Processar consolidação diária
**Origem:** RN-002.

O sistema deve calcular e persistir/manter a informação necessária ao consolidado diário a partir dos lançamentos registrados.

O mecanismo de integração e processamento não é definido neste requisito. Ele será derivado dos RNFs/ASRs e decidido arquiteturalmente.

### RF-005 — Consultar consolidado diário
**Origem:** RN-002.

O sistema deve permitir consultar o consolidado de uma determinada data.

O resultado do MVP deve disponibilizar:
- total de créditos;
- total de débitos;
- saldo diário (`créditos - débitos`);
- quantidade de lançamentos refletidos;
- data/hora da última atualização do consolidado.

### RF-006 — Consultar consolidados por período
**Origem:** refinamento de RN-002 para operação e demonstração do MVP.

O sistema deve permitir consultar consolidados diários em um intervalo de datas, com paginação quando aplicável.

## 4. Regras adotadas para tornar o MVP executável

Estas regras não foram explicitamente fornecidas pelo desafio e devem ser lidas junto às premissas e questões em aberto:
- o valor do lançamento deve ser maior que zero;
- o tipo deve ser `DEBIT` ou `CREDIT`;
- lançamentos confirmados são tratados como imutáveis no MVP;
- o saldo diário é calculado como total de créditos menos total de débitos;
- o consolidado é informação derivada dos lançamentos.

## 5. Segurança funcional

O desafio exige que segurança seja considerada, mas não fornece cadastro de usuários, perfis ou Identity Provider como requisito de negócio. Portanto, autenticação/autorização não serão artificialmente tratadas como capacidade financeira do domínio.

A proteção das APIs e das integrações será definida na visão de segurança. Se for necessário um endpoint de autenticação para tornar o ambiente local demonstrável, ele será classificado como mecanismo de suporte do MVP, não como requisito de negócio original.

## 6. Fora do escopo funcional inicial

Não serão adicionados sem requisito ou justificativa posterior:
- cadastro de comerciante/multi-tenant;
- cadastro, abertura ou fechamento de caixa;
- estorno/correção de lançamento;
- edição ou exclusão física de lançamento;
- RBAC de negócio;
- multi-moeda;
- relatórios adicionais e exportações.

Esses itens poderão aparecer como evoluções futuras ou ser incorporados caso uma questão em aberto seja validada como requisito.