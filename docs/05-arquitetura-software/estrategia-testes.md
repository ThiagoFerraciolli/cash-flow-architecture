# Estratégia de testes e critérios de aceite

A estratégia cobre os PDFs e os riscos arquiteturais. Ferramentas previstas: xUnit, WebApplicationFactory, Testcontainers, Docker e k6. Não usar provider in-memory como evidência de transações PostgreSQL.

## Casos rastreáveis

| ID | Camada/cenário | Aceite |
|---|---|---|
| T01 | Domínio e HTTP | Crédito 100 e débito 20 resultam em saldo 80/contagem 2; validar valores, precisão, datas, paginação e erros |
| T02 | Idempotência HTTP | Mesma chave/cliente/comando cria um fato; comando divergente dá 409; clientes diferentes têm escopos distintos; retry tardio permanece seguro |
| T03 | Transação de criação | Falha em qualquer inserção reverte fato, chave e Outbox; timeout não deixa reserva órfã |
| T04 | Publicação concorrente | Dois publicadores, lease vencida e token antigo; sem perda e sem atualização por proprietário antigo |
| T05 | Consumo concorrente | Eventos distintos na mesma data somam exatamente; duplicata e versões do mesmo fato não duplicam; conflito de conteúdo gera DLQ |
| T06 | Independência | Parar API, Worker e banco de Consolidação; registrar 1.000 fatos válidos a 10/s com sucesso e contagem exata; retomar e convergir |
| T07 | Broker indisponível | Novos fatos confirmam; Outbox cresce; circuit breaker pausa envio sem suspender itens válidos; retorno do broker entrega tudo; payload inválido suspende somente o próprio item |
| T08 | Crash windows | Cair após envio antes de published_at e após commit antes de ack/delete; recuperar sem perda/duplicação |
| T09 | Retry e DLQ | Falha sistêmica pausa consumo sem gastar tentativas de aplicação; transitória isolada recupera; permanente isola; redrive preserva fato; destino de retry indisponível não perde mensagem |
| T10 | Reconciliação | Snapshot consistente diferencia atraso e corrupção; não acusa fato posterior ao snapshot como divergência |
| T11 | Rebuild | Consumo pausado antes do snapshot, escrita continua; mensagens antes/depois do corte; crash em cada etapa; geração trocada atomicamente e somas exatas |
| T12 | Retenção e identidade | Simular mensagem expirada e Outbox publicada removida; reconstruir pelo fato/eventId persistido; deduplicação ativa nunca é limpa por prazo |
| T13 | Segurança | Keycloak e Cognito: 401/403, access token, emissor/assinatura/cliente/scopes/audience aplicável, rotação JWKS/segredo, limite de corpo e taxa; nenhum segredo/payload sensível em logs |
| T14 | Observabilidade | Correlação e sinais permitem diagnosticar os cenários anteriores; manutenção não vira falsa falha financeira |
| T15 | Carga de consulta | 50 req/s, perda/falha ≤ 5%, resposta válida e metas internas de latência |
| T16 | Recuperação de backlog | 10.000 fatos acumulados convergem em até 10 min com escrita contínua 10/s; verificar conjuntos e valores |
| T17 | Restore e disponibilidade AWS | Restore/PITR de Lançamentos bloqueia escrita, isola fila antiga, cria destino novo, reconstrói a projeção e impede evento antigo; medir RPO/RTO, falha de zona, readiness e reconexão |
| T18 | Inicialização limpa | Terceiro executa guia local sem AWS; migrations e configuração determinísticas |
| T19 | Contratos | Evento v1, campos opcionais, versão desconhecida, decimal textual, mudança de geração na paginação |
| T20 | Isolamento de camadas | Domain/Application sem SDK de broker/framework de persistência; adaptadores separados |

T01 inclui centavos, limite individual, overflow de agregado, timezone na virada do dia, data retroativa e futuro além da tolerância. T02 inclui requisições concorrentes e releitura após conflito. T11 valida também que rollback para geração antiga após retomada é bloqueado. T17 inclui mensagem anterior e posterior ao ponto recuperado, Outbox pendente, retry do cliente com a mesma chave e proibição de redrive entre gerações. API com projeção vazia retorna zeros e completeness UNKNOWN; banco indisponível retorna 503, nunca zeros.

## Carga reproduzível

A operação assumida para as 50 req/s é GET /consolidations/{date}; o PDF não esclarece se a carga se refere a consulta ou processamento. Ensaio de escrita/backlog é complementar e tem critério separado.

Massa: 100.000 lançamentos sintéticos em 30 datas, consolidados reconciliados antes do início. Mistura de créditos/débitos com somas conhecidas. Máquina de referência local: pelo menos 4 CPUs/8 GB disponíveis; registrar modelo/OS/Docker e alocação real. Gerador de carga separado dos recursos limitados da aplicação sempre que possível; registrar quando compartilhar máquina.

Aquecimento 2 min, medição 10 min. k6 `constant-arrival-rate`, 50 iterações/s com exatamente um GET por iteração; distribuir entre as datas pré-carregadas. Token obtido previamente e renovado fora do tempo medido de GET; pré-alocar VUs suficientes para timeout de 5 s (300 como referência), validar capacidade do gerador. Proibir sleeps que reduzam a taxa. Registrar taxa por segundo e total esperado de 30.000 requisições na janela.

Definir `failure_rate = (timeouts + erros de transporte + status inesperado + corpo semanticamente inválido) / requests_sent`. Cada requisição conta no máximo uma falha. Sucesso é 200 com schema e valores esperados para a massa estática; um 200 incorreto falha. 429/5xx contam; falhas não são excluídas sem causa demonstrada e relatório. Não atribuir automaticamente erro do ambiente à aplicação, mas não usar essa distinção para aprovar um ensaio inválido.

Aceite: carga emitida sustentada em 50/s, `dropped_iterations = 0`, total esperado emitido, failure_rate ≤ 0,05, P95 ≤ 500 ms e P99 ≤ 1 s (estas duas são metas internas). Gerador incapaz de emitir invalida o ensaio; não reduz o denominador para obter aprovação. Registrar tentadas, enviadas, respostas, timeout, percentis, CPU/memória, conexões, fila e Outbox. Ensaio adicional com escrita concorrente verifica integridade eventual separadamente da massa estática.

## Matriz por transporte

RabbitMQ: T01–T16 e T18–T20 localmente, incluindo confirms, mandatory sem rota, ack perdido, falha no repasse retry/DLQ, volume persistente e reinício. SQS real em homologação: repetir invariantes T01–T16/T19/T20 e executar T17; incluir receipt handle, timeout/renovação de visibilidade, IAM/KMS negativos, redrive e expiração em fila descartável isolada. Teste local não certifica AWS.

## Evidências e aprovação de execução

Na implementação criar `docs/08-testes/` com relatório por caso: commit, data, ambiente, configuração, versões/digests, comando, massa/seed, duração, resultado esperado/obtido, métricas brutas e logs sanitizados. Um caso é aprovado somente por evidência reproduzível; falhas ficam abertas com impacto e correção. A matriz documental permanece distinta do resultado de execução.

Referência: [executor de taxa constante do k6](https://grafana.com/docs/k6/latest/using-k6/scenarios/executors/constant-arrival-rate/).
