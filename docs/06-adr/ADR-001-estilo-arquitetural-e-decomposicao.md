# ADR-001 — Separação das capacidades de Lançamentos e Consolidação

- **Status:** PROPOSTO — aguardando validação
- **Drivers:** RNF-001, RNF-013, ASR-001, ASR-008

## Contexto

O desafio exige que o serviço de controle de lançamentos continue disponível quando o sistema de consolidado diário estiver indisponível. As duas capacidades também apresentam perfis distintos: lançamentos representam fatos financeiros primários; consolidação é uma visão derivada. O enunciado informa ainda um cenário de 50 req/s para o serviço de consolidação, sem especificar qual operação recebe essa carga.

Precisamos decidir a unidade de execução/deployment sem assumir que “microsserviços” são automaticamente a melhor resposta.

## Alternativas avaliadas

### A. Monólito único, mesmo processo e deployment

**Vantagens:** menor complexidade operacional, desenvolvimento e execução simples.

**Desvantagens:** aumenta o risco de acoplamento de falha e deployment entre lançamento e consolidação; dificulta demonstrar e garantir o RNF-001; scaling é conjunto mesmo com perfis de carga diferentes.

### B. Monólito modular com um deployment

**Vantagens:** boa separação lógica, baixo custo operacional, fronteiras internas explícitas.

**Desvantagens:** apesar do isolamento lógico, lançamento e consolidação continuam compartilhando ciclo de vida do processo/deployment. Uma falha de processo ou manutenção pode afetar as duas capacidades, enfraquecendo a demonstração do RNF-001.

### C. Dois serviços/deployables independentes, alinhados aos bounded contexts

**Vantagens:** isolamento operacional; falha e scaling independentes; aderência direta ao RNF-001; permite evolução separada sem exigir decomposição excessiva.

**Desvantagens:** introduz integração distribuída, consistência eventual, observabilidade ponta a ponta e maior complexidade de testes/operação.

## Decisão proposta

Adotar **dois serviços independentes**, um para `Transactions` e outro para `Daily Consolidation`, cada um alinhado a uma capacidade/bounded context.

Esta solução usa arquitetura de serviços/microsserviços de granularidade grossa: apenas duas unidades justificadas pelo requisito de isolamento. Não serão criados serviços adicionais para autenticação, relatórios, eventos ou outras responsabilidades sem driver arquitetural.

A decisão não depende do cenário hipotético de transição. O cenário de transição apenas demonstra como uma solução monolítica hipotética poderia evoluir para a arquitetura-alvo; a justificativa primária para os dois serviços continua sendo o RNF explícito de independência entre Lançamentos e Consolidação.

## Consequências

- consolidação não participa do caminho síncrono de confirmação do lançamento;
- comunicação entre os contexts precisa tolerar indisponibilidade temporária;
- consistência entre lançamento e consolidado será eventual;
- cada serviço terá health e ciclo de deployment próprios;
- persistência terá ownership definido por serviço;
- integração, idempotência e observabilidade tornam-se decisões arquiteturais relevantes;
- o ambiente local deve permitir interromper a consolidação mantendo lançamentos operacionais.

## Critério de validação

O ADR será considerado validado quando o ambiente executável demonstrar que o serviço de consolidação pode ser interrompido enquanto novos lançamentos continuam sendo confirmados e persistidos.