# Evidência histórica — Fase 5 de Consolidação

> **ARQUIVO HISTÓRICO.** Este documento registra uma execução intermediária de 22/10/2026 e **não representa o estado final da entrega**. Para avaliação use [validação local final](validacao-local-final.md) e [rastreabilidade de testes da entrega](production-grade-local-traceability.md). O gate automatizado corrente é **90 passed, 0 failed, 0 skipped**, com build **0 warnings / 0 errors**; o ambiente final inclui Keycloak, Jaeger, Recovery e os demais endurecimentos posteriores.

Este arquivo é mantido apenas como trilha histórica porque contém evidência de uma etapa anterior da implementação. Números, branches, topologia e limitações abaixo não devem substituir a documentação final.

## Registro original resumido

- Data: 2026-10-22.
- Base histórica: `e63996bc45989f2881cadc49d07bc04ba6f5a5fc`, branch `feat/consolidation-consumer`.
- Gate daquela etapa: **75 passed, 0 failed, 0 skipped**.
- Ambiente: Docker Compose, dois PostgreSQL e RabbitMQ; identidade ainda usava emissor de teste externo ao Compose.
- Cobertura principal: projeção diária, idempotência, conflitos, retry/DLQ, outage do banco de Consolidação, concorrência e persistência local.
- Um ensaio manual confirmou CREDIT `100.00`, DEBIT `20.00`, CREDIT `50.00` → créditos `150.00`, débitos `20.00`, contagem `3`, saldo `130.00`.
- A etapa também confirmou que indisponibilidade do PostgreSQL de Consolidação não derrubava Transactions e que mensagem inválida podia chegar à DLQ.
- Não mediu performance, RTO/RPO, AWS/SQS ou rebuild operacional.

## Por que este registro não é evidência final

Depois desta fase foram adicionados/alterados identidade local com Keycloak, observabilidade distribuída, Recovery com lease/fencing, ensaios T06/T09/T10/T13/T14/T15/T18, autorização de leitura de Transactions e testes adicionais. Portanto, qualquer avaliação do desafio deve usar os documentos finais citados no início deste arquivo.

O Git preserva os detalhes completos da implementação intermediária; a documentação final evita manter duas versões concorrentes da verdade operacional.