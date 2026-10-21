# Capacidades de Negócio e Domínios Funcionais

## 1. Objetivo

Este documento transforma os dois requisitos de negócio do desafio em capacidades e responsabilidades arquiteturais, sem introduzir funcionalidades financeiras que não foram solicitadas.

## 2. Contexto de negócio

O comerciante precisa:
1. controlar lançamentos de débito e crédito do fluxo de caixa diário;
2. consultar o saldo diário consolidado.

A solução deve preservar a capacidade de registrar lançamentos mesmo quando a consolidação estiver indisponível.

## 3. Mapa de capacidades

| Capacidade | Valor entregue | Responsabilidades | Criticidade |
|---|---|---|---|
| Controle de Lançamentos | Registrar e consultar os fatos financeiros do fluxo de caixa | registrar débito/crédito, validar dados mínimos, consultar lançamentos, preservar registros confirmados | Alta — fonte primária dos dados financeiros |
| Consolidação Diária | Disponibilizar visão diária derivada | processar lançamentos, calcular totais, saldo e quantidade, consultar consolidado | Alta para leitura, mas sua indisponibilidade não pode bloquear lançamentos |
| Segurança de Acesso | Proteger consumo das capacidades | autenticação/autorização conforme ambiente, proteção de credenciais e segredos | Transversal |
| Observabilidade Operacional | Permitir diagnóstico e operação | logs, métricas, correlação, health, backlog/atraso e alertas | Transversal |

## 4. Domínios funcionais

### 4.1 Domínio de Lançamentos

Responsável pelos fatos financeiros registrados.

**Responsabilidades:**
- aceitar lançamentos válidos de débito e crédito;
- atribuir identidade e instante de criação;
- preservar lançamentos confirmados;
- disponibilizar consultas do MVP;
- disponibilizar de forma confiável a informação necessária para atualização do consolidado.

**Não é responsabilidade deste domínio:**
- calcular o consolidado diário como fonte autoritativa;
- depender da disponibilidade da consolidação para confirmar um lançamento;
- implementar regras de saldo disponível, estorno, fechamento de caixa ou multi-tenant sem requisito validado.

### 4.2 Domínio de Consolidação

Responsável por uma visão derivada dos lançamentos.

**Responsabilidades:**
- receber/processar informação dos lançamentos;
- manter a projeção diária;
- calcular total de créditos, total de débitos, saldo e quantidade refletida;
- disponibilizar consulta do consolidado;
- recuperar trabalho pendente após indisponibilidade sem dupla contabilização.

**Não é responsabilidade deste domínio:**
- ser fonte primária dos lançamentos;
- participar sincronamente da confirmação de um novo lançamento;
- decidir se um débito pode ou não ocorrer com base em saldo disponível — essa regra não existe no desafio.

## 5. Bounded contexts propostos

### `Transactions Context`

Modelo centrado em `FinancialTransaction` e nas regras necessárias para registrar o fato financeiro.

### `Daily Consolidation Context`

Modelo centrado em `DailyConsolidation`, uma projeção derivada dos fatos produzidos pelo contexto de lançamentos.

A separação entre contexts é proposta porque existem responsabilidades, modelos e requisitos de disponibilidade diferentes. A decisão sobre a separação física em deployables independentes é tratada no ADR de estilo arquitetural.

## 6. Fluxo de valor principal

`Comerciante → registrar lançamento → lançamento confirmado/persistido → informação disponibilizada para processamento → consolidado atualizado → comerciante consulta saldo diário`

O caminho crítico de registro termina com a confirmação segura do lançamento e não depende da disponibilidade do processamento do consolidado.

## 7. Capacidades transversais

Segurança, observabilidade, configuração, resiliência e auditoria são capacidades técnicas transversais. Elas não serão transformadas artificialmente em domínios de negócio.

## 8. Fora do escopo

Permanecem fora do MVP até validação: comerciante/multi-tenant, conta/caixa, abertura/fechamento, estorno, saldo disponível, multi-moeda, relatórios adicionais e exportações.