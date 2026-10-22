# Questões de evolução e condições para adoção produtiva

## Questões funcionais e tratamento atual

| IDs | Tema | Tratamento aprovado |
|---|---|---|
| QRF-001 | Débito superior ao saldo | Permitido; não há validação de saldo disponível |
| QRF-002/008 | Estorno/correção | Fora do escopo; fatos imutáveis |
| QRF-003 | Retroativos/futuros | Retroativos aceitos, futuro até tolerância de relógio |
| QRF-004/005 | Fuso/fechamento | America/Sao_Paulo; sem fechamento |
| QRF-006 | Valores | Faixa e escala definidas nos parâmetros |
| QRF-007 | Perfis | Permissões técnicas por cliente; sem RBAC de negócio |
| QRF-009/012 | Tenant/conta/caixa | Comerciante único e caixa lógico único |
| QRF-010 | Períodos | Consulta diária e intervalo paginado |
| QRF-011 | Moeda | BRL; multi-moeda exige revisão |

## Condições de adoção em cliente real

| IDs | Tema | Referência já escolhida / informação futura |
|---|---|---|
| QRNF-001/002/003 | SLA/RTO/RPO | Metas internas adotadas; contrato de serviço futuro pode exigir outra topologia |
| QRNF-004/005/006 | Latência, volume e crescimento | Metas/massas de teste fixadas; medir perfil real antes do sizing definitivo |
| QRNF-007 | Retenção legal | Fatos/identidades sem limpeza; definir política legal antes de arquivar ou excluir |
| QRNF-008 | DR regional | Sem automático cross-region; contratar objetivo específico se necessário |
| QRNF-009/010 | Regulação/residência | Us-east-1 para referência e dados sintéticos; confirmar permissibilidade antes de dados reais |
| QRNF-011/014/016 | Identidade, chaves e integração | Cognito, IAM/SigV4, KMS/TLS definidos; integrar padrão corporativo por nova decisão |
| QRNF-012 | Defasagem | Meta normal e recuperação de backlog já definidas |
| QRNF-013/015 | Interpretação do pico/perda | GET por data e respostas válidas; confirmar interpretação com avaliador quando houver oportunidade, sem bloquear ensaio |

Não há dependência de resposta externa para implementar o escopo documentado. Não transformar uma nova informação de negócio em alteração silenciosa de contrato, identidade financeira ou regra de data.
