# ADR-003 — PostgreSQL por contexto

- **Direcionadores:** ASR-002/004, integridade monetária e isolamento de falhas.

## Alternativas e decisão

PostgreSQL oferece transações, constraints, índices, UPSERT e bloqueios adequados aos fatos financeiros. Banco documental/NoSQL não tem requisito conhecido que compense complexidade adicional. Adotar PostgreSQL 17, instâncias separadas em ambiente local e RDS separados em AWS; schemas, credenciais e migrations por contexto.

Lançamentos possui fatos, idempotência e Outbox. Consolidação possui projeções, registro de identidades, controle de geração/manutenção e histórico operacional. O modelo normativo é a visão de dados. Não há joins entre bancos no fluxo de negócio; ferramenta operacional tem acesso excepcional auditado.

## Consequências

Decimal/numeric com precisão explícita, constraints e transações locais. Migrations são job separado, não inicialização concorrente das APIs. Produção usa Multi-AZ, backups/PITR e ensaio de restore conforme arquitetura AWS; local usa volumes persistentes e não oferece HA. Testes usam PostgreSQL real para concorrência, rollback e precisão.
