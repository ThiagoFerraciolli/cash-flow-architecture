# ADR-003 — PostgreSQL por contexto

- **Direcionadores:** ASR-002/004, integridade monetária e isolamento de falhas.

## Alternativas e decisão

PostgreSQL oferece transações, constraints, índices, UPSERT e bloqueios adequados aos fatos financeiros. Banco documental/NoSQL não tem requisito conhecido que compense complexidade adicional. Adotar PostgreSQL 17, instâncias separadas em ambiente local e, **como topologia AWS inicial ainda não homologada**, RDS separados; schemas, credenciais e migrations por contexto. **A separação de bounded contexts não exige, por si só, instâncias físicas separadas.** A decisão produtiva fica condicionada à análise econômica e aos testes de isolamento descritos abaixo.

Lançamentos possui fatos, idempotência e Outbox. Consolidação possui projeções, registro de identidades, controle de geração/manutenção e histórico operacional. O modelo normativo é a visão de dados. Não há joins entre bancos no fluxo de negócio; ferramenta operacional tem acesso excepcional auditado.

## Revisão crítica após feedback — alternativa compartilhada

O desafio pede continuidade do registro quando a Consolidação está indisponível. A implementação local usa dois PostgreSQL e valida uma falha limitada da Consolidação; isso **não prova** que duas instâncias RDS sejam a única forma de cumprir o requisito, nem prova que um RDS compartilhado seja seguro.

| Alternativa | Benefício | Risco principal | Condição de escolha |
|---|---|---|---|
| **A — dois RDS Multi-AZ** | Isolamento físico de CPU, memória, I/O, conexões e failover por contexto | Custo fixo maior e operação duplicada | Isolamento de falha/saturação exigido ou alternativa B reprovada |
| **B — um RDS Multi-AZ, dois bancos/schemas e roles** | Menor custo inicial e separação lógica dos dados | Banco/failover, recursos e PITR compartilhados | Aceite de risco compartilhado + testes de carga, contenção e restore aprovados |

**Decisão registrada:** preservar A no desenho de referência atual **sem apresentá-la como opção economicamente ótima**. Para adoção produtiva, usar B como **baseline de custo a avaliar**, e selecionar A apenas se houver evidência de que a independência requerida inclui isolamento físico de falhas, contenção de recursos ou recuperação independente. O trade-off precisa ser validado com responsáveis de negócio e operação. Não houve execução de testes de B ou provisionamento AWS.

**Critérios obrigatórios:** carga simultânea incluindo 50 GET/s e escrita; saturação deliberada da Consolidação; indisponibilidade/failover; conexão e locking; restauração seletiva e reconciliação; separação de credenciais e migrations; comparação de TCO com sizing real. Sem esses resultados, a decisão de produção está **pendente**, não aprovada.

A [estimativa de custos](../07-infraestrutura/estimativa-custos.md) apresenta memória diferencial de A e B, economia hipotética e limites. A [topologia AWS](../07-infraestrutura/arquitetura-aws.md) ainda representa A e deverá ser atualizada se B for formalmente selecionada.

## Consequências

Decimal/numeric com precisão explícita, constraints e transações locais. Migrations são job separado, não inicialização concorrente das APIs. Produção usa Multi-AZ, backups/PITR e ensaio de restore conforme arquitetura AWS; local usa volumes persistentes e não oferece HA. Testes usam PostgreSQL real para concorrência, rollback e precisão.
