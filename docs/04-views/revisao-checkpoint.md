# Revisão transversal — registro de checkpoint

- **Status:** SUPERADO PELA REVISÃO FINAL EM ANDAMENTO

Este arquivo é mantido somente como registro histórico. Ele **não representa aprovação atual** e não autoriza abertura de PR.

A revisão anterior continha conclusões posteriormente reavaliadas: Kafka era tecnologia de referência; havia linguagem de “aprovado” enquanto ADRs/views permaneciam propostos; SDD/C4 Component/testes ainda não estavam fechados; e não havia propagação integral de 2 serviços/4 processos, concorrência, DLQ/redrive, reconciliação e `BusinessDate`.

## Estado que substitui o checkpoint

- SQS Standard + DLQ é a mensageria da arquitetura AWS; Kafka/MSK é evolução condicional;
- 2 serviços de negócio: Lançamentos e Consolidação;
- 4 processos: Transactions API, Transactions Worker, Consolidation API e Consolidation Worker;
- `financial_transactions` é fonte de verdade deste escopo;
- `BusinessDate` é calculado em Lançamentos e propagado no evento;
- Outbox + entrega at-least-once + consumidor idempotente;
- recuperação inclui retry/backoff, DLQ/redrive, reconciliação e rebuild controlado;
- documentação só será considerada pronta após segunda revisão integral e validação humana.

Nenhuma afirmação de aprovação ou “sem blocker para PR” da versão histórica deve ser usada como evidência de conclusão.