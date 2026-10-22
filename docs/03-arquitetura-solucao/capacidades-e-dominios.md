# Capacidades de negócio e domínios funcionais

**Estado: DOCUMENTAÇÃO APROVADA.** O comerciante registra débitos/créditos e consulta o saldo do movimento diário. O valor de negócio é manter os fatos financeiros disponíveis e uma visão derivada recuperável.

| Capacidade | Responsabilidade | Resultado |
|---|---|---|
| Controle de Lançamentos | Registrar, validar, identificar e consultar fatos imutáveis | Fonte financeira durável e auditável |
| Consolidação Diária | Projetar totais por data e disponibilizar relatório | Saldo diário, contagem e indicação de consistência eventual |
| Segurança de acesso | Identidade técnica, permissões e proteção | Consumo controlado das APIs e integrações |
| Operação e recuperação | Diagnóstico, reconciliação, restore e rebuild | Continuidade e evidências de integridade |

Segurança e operação são capacidades transversais, não novos domínios financeiros.

## Contexto de Lançamentos

Modelo centrado em FinancialTransaction. Valida comando, define BusinessDate e identidade, persiste fato e trabalho de integração, oferece consultas. Não depende do saldo projetado para aceitar débito e não chama Consolidação ao confirmar. API e Worker compartilham contexto e banco, com permissões distintas.

## Contexto de Consolidação

Modelo centrado em DailyConsolidation e geração da projeção. Processa fatos imutáveis, controla efeito único, disponibiliza leitura eventual e permite reconstrução. Não altera fatos de origem nem decide aceitação de lançamento. API e Worker compartilham contexto e banco.

## Fluxo de valor

Comerciante/cliente autenticado → registro confirmado → publicação confiável → projeção atualizada → consulta do saldo diário. A confirmação encerra o caminho crítico de escrita; a consulta informa que pode estar defasada. Recuperação usa fatos da origem para restabelecer a visão derivada.

## Limites

Comerciante único, caixa lógico único, BRL, sem estorno/edição/multi-tenant ou saldo transportado. Essas escolhas estão aprovadas em premissas. Separação física está no ADR-001; integração no ADR-002; segurança não cria cadastro financeiro adicional.
