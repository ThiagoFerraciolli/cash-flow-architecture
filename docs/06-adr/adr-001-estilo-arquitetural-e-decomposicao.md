# ADR-001 — Dois serviços de negócio e quatro processos

- **Estado:** APROVADO
- **Direcionadores:** RNF-001/013, ASR-001/008.

## Alternativas

Monólito de processo único reduz operação, mas compartilha interrupção e implantação entre registro e consolidação. Monólito modular melhora organização interna, porém um deployment único preserva esse acoplamento. Dois serviços independentes permitem parar Consolidação inteira e manter Lançamentos, com custo de integração distribuída.

## Decisão e consequências

Serviço Lançamentos: API e Worker publicador da Outbox. Serviço Consolidação: API e Worker consumidor. São quatro processos implantáveis, dois modelos de domínio e dois bancos; não quatro microsserviços de negócio. API e Worker compartilham responsabilidade pelo contexto, com credenciais e permissões distintas.

Separar Workers permite escalar e reiniciar integração sem interromper HTTP. A referência AWS mantém réplicas por processo; local usa uma instância de cada processo. Falha de banco/Worker/API de Consolidação não deve bloquear commit em Lançamentos. Infraestrutura compartilhada ainda pode falhar; não se promete independência de região, rede ou identidade.

Validação futura: T06 e T07 da estratégia de testes. A decisão está aprovada; o comportamento ainda será implementado e medido.
