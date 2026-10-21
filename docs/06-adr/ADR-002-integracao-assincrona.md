# ADR-002 — Integração assíncrona entre Lançamentos e Consolidação

- **Status:** PROPOSTO — aguardando validação
- **Drivers:** RNF-001, RNF-005, RNF-007, ASR-001, ASR-003, ASR-005
- **Dependência:** ADR-001

## Contexto

O registro de lançamentos deve permanecer operacional durante indisponibilidade da consolidação. Portanto, uma chamada síncrona obrigatória do Transactions Service para o Consolidation Service não pode estar no caminho crítico de escrita.

A integração precisa reter informação suficiente para recuperação posterior e permitir reprocessamento controlado.

## Alternativas avaliadas

### A. HTTP síncrono com retry/circuit breaker

Simples, porém a confirmação do lançamento continua acoplada à disponibilidade/latência da consolidação ou exige uma segunda estratégia de persistência para pendências. Não atende sozinho ao driver principal.

### B. Polling da base de lançamentos pela consolidação

Evita broker, mas cria acoplamento de dados entre contexts, aumenta risco de consultas concorrentes e torna ownership/fronteiras menos claros. Pode funcionar em escala pequena, mas é menos apropriado para independência e evolução dos serviços.

### C. Fila tradicional

Entrega assíncrona, desacopla temporalmente e é suficiente para muitos cenários. É alternativa válida, especialmente quando o requisito é processamento de comandos/trabalho e não retenção/replay de um histórico de eventos.

### D. Log de eventos distribuído (Kafka)

Oferece retenção configurável, replay, consumer offsets, particionamento e possibilidade de novos consumidores. Em contrapartida, possui custo e complexidade operacional maiores do que uma fila simples.

## Decisão proposta

Adotar **integração assíncrona orientada a eventos** entre os contexts e utilizar **Kafka** como tecnologia de referência para o desafio.

A escolha de Kafka é justificada pela combinação de:
- recuperação de backlog após indisponibilidade;
- retenção e replay úteis para reconstrução/recuperação da projeção;
- controle explícito da posição do consumidor;
- possibilidade de demonstrar lag operacional;
- escalabilidade por partições caso o volume cresça.

Para o volume explicitamente conhecido (50 req/s no consolidado), Kafka não é necessário apenas por throughput. A escolha é principalmente por semântica de retenção/replay e demonstração de recuperação. Em um ambiente corporativo cujo padrão seja uma fila gerenciada e sem necessidade de replay, uma tecnologia mais simples pode ser preferível.

## Semântica

- entrega assumida: **at-least-once**;
- não será prometido exactly-once ponta a ponta;
- consumidores precisam ser idempotentes;
- ordenação global não será assumida;
- qualquer necessidade de ordenação deve ser definida por chave de negócio após esclarecimento de conta/caixa/tenant; não será inventada uma partition key sem esse requisito.

## Consequências

- o consolidado pode ficar temporariamente defasado;
- precisamos monitorar backlog/consumer lag;
- schema e compatibilidade dos eventos precisam ser governados;
- falhas persistentes precisam de política operacional;
- Kafka não resolve a janela entre commit no banco de lançamentos e publicação do evento; isso é tratado em ADR separado.