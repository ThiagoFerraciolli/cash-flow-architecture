# Resolução da revisão arquitetural

**Estado: DOCUMENTAÇÃO APROVADA.** Registro de fechamento da revisão na branch documental. Data: 22/10/2026. Todas as ações abaixo são documentais; implementação e evidências continuam reservadas à próxima etapa por instrução do responsável.

| Ponto da revisão | Resolução adotada | Evidência documental |
|---|---|---|
| Entrega ainda sem C#/testes | Separação explícita de conformidade documental e executável; plano e critérios completos | Matriz de conformidade; README; estratégia de testes |
| Execução local indefinida | Compose especificado com RabbitMQ, dois PostgreSQL e Keycloak; portas, bootstrap e comandos futuros | execucao-local; ADR-010 |
| Limpeza permite dupla contabilização | Identidade financeira por geração sem expiração ativa; idempotência HTTP retida com fato | ADR-005; visão de dados |
| Rebuild sem algoritmo seguro | Pausa persistente antes do snapshot, geração nova com totais/identidades, validação e troca atômica | recuperação; ADR-009; fluxos |
| Reconciliação com cortes diferentes | Snapshot da origem após pausa; comparação de conjuntos e integridade interna; atraso separado de corrupção | recuperação; T10 |
| Zero confundido com ausência de movimento | API declara consistência eventual e completude desconhecida, inclusive com zeros | contratos-http; RF-005 |
| Expiração de transporte | Retenções/alertas definidos; eventId persistido no fato; recuperação sem depender da Outbox/fila | parâmetros; ADR-008/009; T12 |
| Decisões técnicas adiadas | Valores, limites, metas internas e topologia adotados como escolhas aprovadas | parâmetros; premissas; AWS |
| Segurança genérica | JWT nas APIs, Keycloak/Cognito, permissões, IAM/KMS/TLS, limites e auditoria | segurança; contratos |
| SKIP LOCKED incompleto | Reserva persistente com prazo/token, transação curta e atualização condicionada | ADR-004; T04 |
| Carga pode emitir menos de 50/s | Executor de chegada constante, dropped_iterations=0, contagem esperada e validação semântica | estratégia de testes T15 |
| Custos pouco reproduzíveis | Quantidades, fórmulas, distinção tarifa/hipótese, NAT, IPv4, identidade e contingência | estimativa-custos |
| Duplicações e status | Fontes normativas únicas, remoção de histórico de propostas e aprovação documental uniforme | README; ADRs e views |
| Cardinalidade da Outbox | Relação opcional após limpeza, obrigatória no commit de criação | visão de dados |
| Transição de legado sem escopo | Marcada não aplicável; plano real local→AWS definido | transição; implantação |
| Repositório público sem verificação | Visibilidade public confirmada por metadados GitHub em 22/10/2026 | matriz de conformidade |

## Critérios de encerramento documental

Decisões coerentes entre requisitos, ADRs, contratos, dados, diagramas, operação e testes; links internos válidos; nenhuma alegação de execução inexistente; nenhuma criação de código, scripts executáveis, Compose ou IaC. Os arquivos originais dos PDFs foram usados como critérios; não foram tratados como autorização para iniciar desenvolvimento.

A etapa executável deverá cumprir os critérios dos PDFs antes de declarar a entrega final concluída. A aprovação do texto não substitui esses resultados nem cria pendência de nova aprovação documental.

## Verificações realizadas nesta revisão

Revisados 38 arquivos Markdown (31 existentes e 7 novos). Verificados 69 links internos, parseados 3 exemplos JSON e validada a sintaxe dos 2 blocos de comandos com bash -n, sem executá-los. Inspeção estrutural dos 14 diagramas Mermaid e conferência transversal dos contratos, parâmetros e fontes normativas. git diff --check sem erros. Nenhum arquivo de implementação foi criado.

Não houve renderização automática Mermaid, teste de aplicação, execução dos comandos demonstrativos ou provisionamento cloud. As verificações acima validam a documentação e seus exemplos, não o comportamento de software ainda inexistente.
