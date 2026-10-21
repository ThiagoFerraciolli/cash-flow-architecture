# ADR-004 — Publicação confiável com Transactional Outbox

- **Status:** PROPOSTO — aguardando validação
- **Drivers:** RNF-004, ASR-002
- **Dependências:** ADR-002, ADR-003

## Contexto

Após confirmar um lançamento, precisamos preservar tanto o fato financeiro quanto a possibilidade de ele ser refletido no consolidado.

Uma implementação ingênua cria dual write:
1. grava lançamento no PostgreSQL;
2. publica evento no Kafka.

Se a aplicação falhar entre as duas operações, o lançamento pode existir sem evento. Inverter a ordem cria o problema oposto: evento publicado para uma transação que depois não foi persistida.

## Alternativas avaliadas

### A. Gravar no banco e publicar diretamente no broker

Simples, mas possui janela de inconsistência entre dois recursos transacionais independentes.

### B. Transação distribuída/2PC

Pode coordenar recursos compatíveis, porém aumenta acoplamento, complexidade e custo operacional; não é uma escolha adequada como padrão para este desafio.

### C. Transactional Outbox

Persistir lançamento e registro de evento na mesma transação local e publicar posteriormente por processo separado. Elimina a janela de dual write entre o dado de negócio e a intenção durável de publicação, ao custo de processamento adicional e necessidade de limpeza/monitoramento.

### D. CDC sobre log do banco

Reduz código de polling e pode ser robusto, mas adiciona plataforma de CDC e complexidade operacional desnecessária para o MVP. Continua sendo evolução possível para maior escala.

## Decisão proposta

Adotar **Transactional Outbox** no Transactions Service.

Na mesma transação PostgreSQL:

```text
BEGIN
  INSERT financial_transaction
  INSERT outbox_event
COMMIT
```

Um publisher independente lê eventos pendentes, publica no broker e registra o estado de publicação.

## Garantias e limites

- a API só confirma sucesso após commit local;
- lançamento e intenção de publicação são atômicos no banco local;
- publicação pode ocorrer mais de uma vez se houver falha entre envio e atualização do estado;
- por isso o consumidor continua precisando ser idempotente;
- Outbox não substitui backup/DR nem define RPO produtivo;
- Outbox não cria exactly-once ponta a ponta.

## Evidência esperada

Teste de integração deve demonstrar que um lançamento confirmado permanece recuperável para publicação mesmo quando o broker ou a consolidação estiver indisponível.