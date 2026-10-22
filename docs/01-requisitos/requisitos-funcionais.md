# Requisitos funcionais

## Requisitos de negócio

- **RN-001:** controlar lançamentos de débito e crédito do fluxo de caixa diário.
- **RN-002:** disponibilizar relatório com saldo diário consolidado.

## Requisitos refinados

| ID | Requisito | Critério de aceite e origem |
|---|---|---|
| RF-001 | Registrar lançamento | Débito/crédito positivo com identidade, instante e data financeira; confirmar após persistência; RN-001 |
| RF-002 | Consultar lançamento por identidade | Retornar fato existente ou 404; RN-001 |
| RF-003 | Listar lançamentos | Filtros de data/tipo e paginação limitada; refinamento de RN-001 |
| RF-004 | Consolidar fatos por dia | Somar créditos/débitos e contar fatos sem duplicação; RN-002 |
| RF-005 | Consultar saldo diário | Retornar totais, saldo, contagem e estado de projeção; RN-002 |
| RF-006 | Consultar período | Uma linha por data, intervalo limitado e paginação; refinamento de RN-002 |

## Regras aprovadas

Comerciante único, moeda BRL e saldo do movimento do dia (créditos menos débitos), sem saldo inicial transportado. Valor positivo e duas casas; lançamento imutável; datas retroativas aceitas; futuro limitado à tolerância de relógio. BusinessDate calculado em Lançamentos com America/Sao_Paulo e persistido; sem fechamento. Limites exatos na [tabela normativa](../07-infraestrutura/parametros-operacionais.md).

A identidade do cliente técnico autenticado é persistida para auditoria. Não há cadastro de usuário ou comerciante como domínio. Ausência de projeção retorna zeros com completude desconhecida; a resposta não afirma ausência de lançamentos na origem. Relatório é oferecido por API JSON; interface gráfica/PDF de relatório não é exigida no escopo adotado.

## Contratos e exclusões

[Contratos HTTP](../05-arquitetura-software/contratos-http.md) especificam entradas, saídas, erros e permissões. Fora do escopo: edição, exclusão, estorno, multi-moeda, multi-tenant, bloqueio por saldo disponível, abertura/fechamento de caixa e exportação. Essas capacidades exigem nova decisão, não estão implicitamente disponíveis.
