# Estimativa de Custos da Arquitetura-alvo

- **Classificação:** DIFERENCIAL — estimativa de infraestrutura e licenciamento
- **Status:** ESTIMATIVA ARQUITETURAL PARA COMPARAÇÃO
- **Moeda:** USD/mês
- **Região de referência:** us-east-1 (Norte da Virgínia)
- **Período de cálculo:** aproximadamente 730 horas/mês

## 1. Objetivo

Apresentar uma estimativa econômica compreensível da arquitetura-alvo, incluindo **serviço, configuração assumida, quantidade, custo estimado, subtotal e total mensal**.

O desafio não fornece volume diário, tamanho médio de evento, retenção, SLA, RTO, RPO, região produtiva ou crescimento. Portanto, os números abaixo são **hipóteses de dimensionamento para comparação arquitetural**, e não orçamento definitivo de produção.

O requisito de 50 req/s no consolidado será validado por teste. Ele não é extrapolado artificialmente para 24 horas por dia no cálculo de armazenamento e tráfego.

## 2. Premissas de cálculo

Para permitir um total, são definidos dois cenários.

### Cenário A — referência enxuta

Destinado a demonstração/homologação ou produção de baixo volume, sem compromisso de alta disponibilidade:

- quatro tarefas Fargate ativas: API de Lançamentos, API de Consolidação, Publicador da Outbox e Consumidor de Consolidação;
- cada tarefa inicia com 0,25 vCPU e 0,5 GB de memória;
- dois bancos PostgreSQL pequenos, um por contexto;
- 20 GB iniciais por banco;
- um Application Load Balancer;
- Kafka gerenciado dimensionado no menor cenário razoável para a estimativa;
- 30 GB/mês de logs como hipótese de cálculo;
- pequeno conjunto de segredos/chaves/imagens;
- sem tráfego significativo de saída para Internet na estimativa base.

### Cenário B — produção de referência

Destinado a mostrar o impacto econômico da redundância, sem afirmar que ela é requisito do desafio:

- duas tarefas para cada API;
- duas tarefas para cada processamento assíncrono;
- bancos com configuração de maior disponibilidade;
- mensageria/eventos gerenciada com redundância compatível com produção;
- maior retenção e volume de observabilidade;
- dimensionamento final condicionado a SLA/RTO/RPO, volume e perfil medidos.

## 3. Tabela de custos — Cenário A

> Valores são aproximações para decisão arquitetural. Preços variam com região, modalidade, tráfego e atualizações do provedor. Antes de uma contratação real, a estimativa deve ser recalculada no AWS Pricing Calculator.

| Serviço | Configuração/premissa | Qtd. | Estimativa unitária/mês | Subtotal/mês |
|---|---|---:|---:|---:|
| AWS Fargate | 0,25 vCPU + 0,5 GB, 24x7 | 4 tarefas | ~US$ 9,01 | **US$ 36,04** |
| RDS PostgreSQL | instância pequena, Single-AZ, referência arquitetural | 2 | ~US$ 25,00 | **US$ 50,00** |
| Armazenamento RDS | 20 GB por banco | 40 GB | ~US$ 0,115/GB | **US$ 4,60** |
| Amazon MSK | Kafka gerenciado; reserva arquitetural para cenário mínimo | 1 | ~US$ 150,00 | **US$ 150,00** |
| Application Load Balancer | 1 ALB + utilização baixa | 1 | ~US$ 22,00 | **US$ 22,00** |
| CloudWatch | hipótese de 30 GB/mês de logs; 5 GB iniciais gratuitos conforme faixa publicada | 30 GB | ~US$ 0,50/GB faturável | **US$ 12,50** |
| Secrets Manager | até 10 segredos como hipótese | 10 | ~US$ 0,40 | **US$ 4,00** |
| KMS | pequena quantidade de chaves/chamadas | 2 chaves | ~US$ 1,00 | **US$ 2,00** |
| ECR | poucas imagens; armazenamento baixo | — | reserva | **US$ 2,00** |
| Transferência/itens variáveis | reserva para baixo volume; não representa egress significativo | — | reserva | **US$ 10,00** |
| **TOTAL ESTIMADO — CENÁRIO A** | | | | **~US$ 293,14/mês** |

### Interpretação

O total de aproximadamente **US$ 293/mês** serve para mostrar a ordem de grandeza do desenho utilizando Kafka gerenciado. O principal componente de custo no cenário enxuto é justamente a infraestrutura de Kafka/MSK, e não o processamento das APIs.

Isso reforça o ADR-007: a escolha de Kafka precisa ser justificada por retenção, reprocessamento e evolução arquitetural, e não pelos 50 req/s.

## 4. Sensibilidade — substituição de Kafka por SQS

Se a validação com o negócio concluir que não existe necessidade de log de eventos, retenção para reprocessamento ou múltiplos consumidores, uma fila gerenciada como Amazon SQS pode substituir Kafka.

Para baixa/média utilização, o custo de SQS tende a ser de poucos dólares por mês e pode ficar dentro da franquia aplicável dependendo do volume. Para comparação arquitetural, adotamos **US$ 5/mês** como reserva de consumo, sem transformá-la em cotação.

| Composição | Total aproximado |
|---|---:|
| Cenário A com Amazon MSK | **US$ 293/mês** |
| Cenário A substituindo MSK por reserva de US$ 5 para SQS | **US$ 148/mês** |
| Economia aproximada na hipótese | **US$ 145/mês (~49%)** |

Essa comparação **não altera automaticamente a decisão arquitetural atual**. Ela torna explícito o custo da capacidade adicional oferecida por Kafka.

## 5. Tabela de custos — Cenário B

Como o desafio não informa SLA, RTO, RPO nem volume produtivo, este cenário é deliberadamente uma **faixa de planejamento**, não uma cotação.

| Grupo | Premissa de produção de referência | Faixa mensal adotada |
|---|---|---:|
| Fargate | 8 tarefas mínimas, permitindo redundância dos quatro componentes | ~US$ 72–150 |
| RDS PostgreSQL | dois bancos com maior disponibilidade, armazenamento e backup | ~US$ 150–400 |
| MSK/Kafka | serviço gerenciado com capacidade/redundância produtiva | ~US$ 300–700 |
| ALB/rede | balanceamento e utilização maior | ~US$ 25–75 |
| Observabilidade | logs, métricas e rastreamento com maior volume/retenção | ~US$ 25–150 |
| Segredos/KMS/ECR | segurança e imagens | ~US$ 10–30 |
| Transferência e reservas variáveis | depende do tráfego real | ~US$ 20–100 |
| **TOTAL DE PLANEJAMENTO — CENÁRIO B** | | **~US$ 602–1.605/mês** |

A faixa é ampla de propósito: sem os RNFs produtivos faltantes, apresentar um valor pontual seria falsa precisão.

## 6. Licenciamento

| Tecnologia | Licença de software | Observação |
|---|---:|---|
| .NET | US$ 0 | não há licença comercial por instância para runtime/SDK utilizado |
| PostgreSQL | US$ 0 | o custo considerado é do serviço gerenciado/infraestrutura |
| Apache Kafka | US$ 0 | o custo do MSK é do serviço gerenciado, não de licença do Kafka |
| OpenTelemetry | US$ 0 | telemetria gera custos de ingestão, processamento e armazenamento no destino |

**Licença zero não significa custo operacional zero.** Operação, infraestrutura, armazenamento, suporte, rede e observabilidade continuam compondo o custo total.

## 7. Itens não incluídos no total

Não foram adicionados valores artificiais para:

- suporte empresarial AWS;
- recuperação entre regiões;
- NAT Gateway, caso a topologia final exija;
- tráfego elevado de saída;
- WAF/API Gateway, caso sejam escolhidos após definição de exposição/segurança corporativa;
- custos organizacionais como CI/CD corporativo, SIEM ou ferramentas já contratadas;
- impostos e câmbio.

Esses componentes deverão ser incluídos quando as respectivas decisões forem conhecidas.

## 8. Informações necessárias para orçamento produtivo

Para transformar a estimativa em orçamento, validar:

1. região de produção;
2. lançamentos por dia e pico de escrita;
3. tamanho médio de payload/evento;
4. retenção dos dados e eventos;
5. SLA/SLO;
6. RTO e RPO;
7. necessidade de Multi-AZ e recuperação entre regiões;
8. crescimento esperado;
9. volume/retenção de logs e traces;
10. tráfego de saída;
11. necessidade real de Kafka versus SQS.

## 9. Fontes e atualização dos valores

A composição utiliza a estrutura de preços publicada pela AWS como referência. Os valores devem ser atualizados no momento da entrega/contratação porque preços podem mudar.

Fontes oficiais de referência:

- AWS Fargate Pricing — https://aws.amazon.com/fargate/pricing/
- Amazon ECS Pricing — https://aws.amazon.com/ecs/pricing/
- Amazon RDS Pricing — https://aws.amazon.com/rds/pricing/
- Amazon MSK Pricing — https://aws.amazon.com/msk/pricing/
- Elastic Load Balancing Pricing — https://aws.amazon.com/elasticloadbalancing/pricing/
- Amazon CloudWatch Pricing — https://aws.amazon.com/cloudwatch/pricing/
- AWS Secrets Manager Pricing — https://aws.amazon.com/secrets-manager/pricing/
- AWS KMS Pricing — https://aws.amazon.com/kms/pricing/
- Amazon ECR Pricing — https://aws.amazon.com/ecr/pricing/
- Amazon SQS Pricing — https://aws.amazon.com/sqs/pricing/

## 10. Conclusão arquitetural

O custo não invalida a arquitetura, mas evidencia uma decisão importante: para este volume, **Kafka é escolhido pelas capacidades de retenção/reprocessamento, não por desempenho**. Se essas capacidades não forem necessárias no ambiente real, SQS é uma alternativa economicamente mais simples que deve ser reavaliada antes da produção.