# ADR-001 — Dois serviços de negócio e quatro processos

- **Direcionadores:** RNF-001/013, ASR-001/008.

## Alternativas

Monólito de processo único reduz operação, mas compartilha interrupção e implantação entre registro e consolidação. Monólito modular melhora organização interna, porém um deployment único preserva esse acoplamento. Dois serviços independentes permitem parar Consolidação inteira e manter Lançamentos, com custo de integração distribuída.

## Decisão e consequências

Serviço Lançamentos: API e Worker publicador da Outbox. Serviço Consolidação: API e Worker consumidor. São quatro processos implantáveis, dois modelos de domínio e dois bancos; não quatro microsserviços de negócio. API e Worker compartilham responsabilidade pelo contexto, com credenciais e permissões distintas.

Separar Workers permite escalar e reiniciar integração sem interromper HTTP. A referência AWS mantém réplicas por processo; local usa uma instância de cada processo. Falha de banco/Worker/API de Consolidação não deve bloquear commit em Lançamentos. Infraestrutura compartilhada ainda pode falhar; não se promete independência de região, rede ou identidade.

## Evidência e limite

A decomposição está implementada no ambiente local. O ensaio T06 demonstrou, de forma limitada, a propriedade central do RNF-001: `consolidation-api`, `consolidation-worker` e `postgres-consolidation` foram interrompidos enquanto Transactions permaneceu live/ready e aceitou dois lançamentos determinísticos (CREDIT 100 e DEBIT 20); após a restauração, a projeção convergiu para 100/20/80 com contagem 2.

Esse ensaio não corresponde ao cenário normativo completo de 1.000 fatos a 10/s. T07 permanece parcialmente coberto por testes de falha/circuito, sem execução operacional integral do cenário normativo. A evidência detalhada está em `docs/08-testes/validacao-local-final.md` e `production-grade-local-traceability.md`.
