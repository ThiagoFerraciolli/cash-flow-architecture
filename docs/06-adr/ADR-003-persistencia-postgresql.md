# ADR-003 — PostgreSQL como persistência transacional de referência

- **Status:** PROPOSTO — aguardando validação
- **Drivers:** ASR-002, ASR-004, requisitos de integridade e auditabilidade

## Contexto

Lançamentos financeiros possuem estrutura conhecida, exigem integridade e precisam participar de operações transacionais. O consolidado também possui modelo estruturado e precisa aplicar atualização da projeção e controle de processamento de forma consistente.

## Alternativas avaliadas

### A. Banco relacional — PostgreSQL

Oferece transações ACID, constraints, índices, maturidade operacional, suporte amplo em cloud e bom equilíbrio entre consistência, custo e simplicidade.

### B. Banco documental

Flexibilidade de schema é útil para domínios altamente variáveis, mas não existe driver no desafio que compense abandonar o modelo relacional para os dados financeiros estruturados.

### C. Banco distribuído/NoSQL orientado a escala

Pode oferecer alta escala e disponibilidade, porém o volume informado não exige essa complexidade e o modelo de consistência/transações precisa ser avaliado cuidadosamente para dados financeiros.

## Decisão proposta

Usar **PostgreSQL** como tecnologia de persistência de referência.

Cada serviço será proprietário de seus dados. Isso não obriga dois servidores físicos no ambiente local: a separação lógica pode ser feita por databases/instâncias conforme o ambiente, desde que não exista acesso direto de um serviço às tabelas privadas do outro.

## Ownership proposto

### Transactions
- `financial_transactions`;
- estruturas técnicas necessárias à publicação confiável, caso ADR específico aprove essa estratégia.

### Daily Consolidation
- `daily_consolidations`;
- estruturas técnicas necessárias à idempotência/controle de processamento.

## Consequências

- migrations são versionadas por serviço;
- constraints de banco complementam invariantes da aplicação;
- serviços não fazem joins entre bancos/contexts;
- backup, PITR, HA e replicação da arquitetura-alvo dependem das metas de RPO/RTO ainda pendentes;
- no ambiente local, persistência deve sobreviver à recriação de containers quando volumes forem utilizados.

## Portabilidade

A decisão é por PostgreSQL e SQL padrão sempre que razoável, não por um serviço cloud específico. A arquitetura-alvo poderá usar PostgreSQL gerenciado, preservando a análise de recursos proprietários caso tragam benefício.