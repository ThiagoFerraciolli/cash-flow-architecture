# Estimativa de Custos da Arquitetura-alvo

- **Classificação:** DIFERENCIAL — infraestrutura e licenciamento
- **Status:** EM REVISÃO FINAL — estimativa, não cotação
- **Moeda:** USD/mês
- **Região de referência:** us-east-1
- **Período:** ~730 h/mês

## 1. Princípio

A estimativa serve para comparar decisões. O desafio não informa volume mensal, payload, retenção, SLA, RTO/RPO, região produtiva ou tráfego; portanto qualquer total depende de premissas. Preços devem ser recalculados no AWS Pricing Calculator na data da validação/contratação.

A arquitetura-alvo atual usa **SQS Standard + DLQ**. MSK aparece apenas como análise de sensibilidade.

## 2. Cenário A — referência enxuta

Premissas: 4 tasks Fargate 24x7 (0,25 vCPU/0,5 GB), 2 PostgreSQL pequenos Single-AZ, 20 GB por banco, 1 ALB, SQS de baixo/médio volume, 30 GB/mês de logs, pequeno conjunto de secrets/keys/images. Não representa produção HA.

| Serviço | Premissa | Subtotal aproximado/mês |
|---|---|---:|
| Fargate | 4 tasks pequenas 24x7 | US$ 36 |
| RDS PostgreSQL | 2 instâncias pequenas Single-AZ | US$ 50 |
| RDS storage | 40 GB | US$ 5 |
| SQS Standard + DLQ | reserva de consumo; volume não informado | US$ 5 |
| ALB | baixa utilização | US$ 22 |
| CloudWatch | hipótese de 30 GB/mês de logs | US$ 13 |
| Secrets Manager | até 10 secrets | US$ 4 |
| KMS | pequeno conjunto de chaves/chamadas | US$ 2 |
| ECR | baixo armazenamento | US$ 2 |
| variáveis/rede | reserva sem egress relevante | US$ 10 |
| **TOTAL DE REFERÊNCIA A** | | **~US$ 149/mês** |

O total é ordem de grandeza, não orçamento. O custo de requests SQS precisa ser recalculado quando volume e batching forem conhecidos.

## 3. Cenário B — produção de referência

Sem SLO/RTO/RPO não é correto fixar topologia HA. Para planejamento, considerar:

- 2+ tasks por processo quando redundância for requerida;
- RDS Multi-AZ somente se o objetivo de disponibilidade justificar;
- autoscaling das APIs e workers;
- maior ingestão/retenção de observabilidade;
- WAF, NAT Gateway ou VPC endpoints conforme topologia final;
- backup/PITR conforme RPO/RTO.

Por causa dessas variáveis, este documento **não apresenta um total de produção fictício**. O diferencial arquitetural é mostrar os cost drivers e como medi-los, não fabricar precisão.

## 4. Sensibilidade — SQS versus MSK

SQS possui baixo custo fixo e cobrança orientada a uso. MSK adiciona baseline de cluster/brokers e operação, mas entrega capacidades que SQS não oferece como event log e replay histórico arbitrário.

Para o desafio atual, essas capacidades não são requeridas; portanto SQS reduz custo e complexidade sem perder nenhum requisito conhecido. Se event streaming/replay histórico se tornar requisito, o ADR-007 deve ser reaberto e o custo de MSK recalculado com configuração real.

## 5. Custos que não podem ficar invisíveis

O total de referência não inclui automaticamente:

- NAT Gateway; pode ser relevante se tasks privadas acessarem endpoints públicos;
- VPC endpoints/PrivateLink; podem substituir parte do tráfego via NAT e possuem custo próprio;
- WAF/API Gateway;
- egress significativo;
- suporte AWS;
- DR cross-region;
- SIEM/observabilidade corporativa;
- CI/CD corporativo;
- impostos/câmbio.

Na implantação real deve-se escolher explicitamente entre NAT e endpoints privados e incorporar o impacto ao cálculo.

## 6. Licenciamento

| Tecnologia | Licença de software | Observação |
|---|---:|---|
| .NET | US$ 0 | runtime/SDK sem licença por instância |
| PostgreSQL | US$ 0 | RDS cobra serviço/infraestrutura |
| Amazon SQS | n/a | serviço gerenciado por uso |
| OpenTelemetry | US$ 0 | destino/ingestão/armazenamento geram custo |

## 7. Dados necessários para orçamento produtivo

Região; transações/dia e pico; requests de consulta; tamanho de mensagens; batching; retenção de dados/logs; SLA/SLO; RTO/RPO; Multi-AZ; crescimento; egress; WAF/API Gateway; topologia NAT/VPC endpoints; política de backup; necessidade futura de Kafka.

## 8. Fontes oficiais

- AWS Fargate Pricing
- Amazon RDS Pricing
- Amazon SQS Pricing
- Elastic Load Balancing Pricing
- Amazon CloudWatch Pricing
- AWS Secrets Manager Pricing
- AWS KMS Pricing
- Amazon ECR Pricing
- AWS VPC Pricing
- Amazon MSK Pricing (somente alternativa)

Os links oficiais devem ser consultados novamente no momento da validação financeira, pois preços mudam.

## 9. Conclusão

A escolha SQS é coerente simultaneamente com requisitos, simplicidade e custo. O principal risco de custo que permanece sem topologia definitiva é rede/observabilidade, não a fila. A arquitetura evita justificar tecnologia mais cara por um throughput de 50 req/s que, sozinho, não exige Kafka.