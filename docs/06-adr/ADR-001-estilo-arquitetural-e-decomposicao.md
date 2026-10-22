# ADR-001 — Separação das capacidades de Lançamentos e Consolidação

- **Status:** ACEITO PARA A ARQUITETURA DO DESAFIO
- **Drivers:** RNF-001, RNF-013, ASR-001, ASR-008

## Contexto

O desafio exige que Lançamentos continue disponível quando a Consolidação diária estiver indisponível. Lançamentos representa fatos financeiros primários; Consolidação é projeção derivada. Os perfis de carga e falha também são distintos.

## Alternativas avaliadas

### Monólito/processo único
Menor complexidade, porém compartilha domínio de falha e escala, enfraquecendo o RNF explícito de independência.

### Monólito modular com um deployment
Boa separação lógica, mas ainda compartilha ciclo de vida/processo e não demonstra isolamento operacional suficiente para o case.

### Dois serviços independentes
Isolamento operacional e escala independente, ao custo de integração distribuída, consistência eventual e observabilidade ponta a ponta.

## Decisão

Adotar **2 serviços de negócio**: `Transactions` e `Consolidation`.

Cada serviço possui **2 processos executáveis** para separar caminho HTTP de processamento assíncrono:
- Transactions API + Transactions Worker/Outbox Publisher;
- Consolidation API + Consolidation Worker.

Logo, a solução possui **2 serviços / 4 processos**, e não quatro microsserviços. API e Worker do mesmo serviço compartilham bounded context e ownership de dados, mas podem escalar e ser implantados independentemente.

## Consequências

- Consolidação não participa do caminho síncrono do lançamento;
- integração é assíncrona e eventualmente consistente;
- ownership de persistência é por serviço;
- APIs e workers possuem health/scale independentes;
- o ambiente deve permitir parar toda a Consolidação e manter Lançamentos operando;
- não são criados serviços adicionais sem driver.

## Validação posterior

`ACEITO` significa que a decisão documental foi escolhida para o desafio. A garantia comportamental só muda para **VALIDADA POR EVIDÊNCIA** quando testes executados demonstrarem independência, recuperação e convergência.