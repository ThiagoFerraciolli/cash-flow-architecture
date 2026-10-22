# Estimativa de custos de infraestrutura e licenciamento

**Estado: DOCUMENTAÇÃO APROVADA; ESTIMATIVA DE PLANEJAMENTO.** Referência em 22/10/2026, USD/mês, us-east-1, 730 horas, sem créditos/free tier/descontos. Não é cotação nem custo medido. O cenário corresponde à topologia de produção documentada, incluindo conectividade e identidade.

## Memória de cálculo

Preços marcados **R** derivam de exemplos públicos consultados; **H** são hipóteses unitárias de planejamento, ainda sujeitas ao Pricing Calculator. Essa distinção evita atribuir à AWS uma tarifa não confirmada. Reservas incluem pequenos consumos acessórios, mas não substituem medição.

| Item | Quantidade × valor unitário adotado | Tipo | USD/mês |
|---|---|---|---:|
| Fargate Linux x86 | 8 × 730 × (0,5 vCPU × 0,0404784/h + 1 GiB × 0,004446/h) | R | 144,16 |
| RDS PostgreSQL | 2 db.t4g.small Multi-AZ × 730 × 0,08/h, incluindo standby na hipótese | H | 116,80 |
| Storage RDS | 2 × 100 GiB gp3 × 0,25/GiB-mês, replicação incluída na hipótese | H | 50,00 |
| NAT Gateway | 2 × 730 × 0,045/h | H | 65,70 |
| Dados processados NAT | 100 GB × 0,045/GB | H | 4,50 |
| IPv4 público | 4 endereços iniciais (ALB e NAT) × 730 × 0,005/h | H | 14,60 |
| ALB | 730 × (0,0225/h + 1 LCU × 0,008/h) | H | 22,27 |
| SQS + DLQ | Reserva: 10 milhões de ações de até 64 KiB × 0,40/milhão + margem 1 | H | 5,00 |
| Observabilidade | 30 GB logs × 0,50 + 10 GB armazenados × 0,03 + reserva 15 para métricas/traces/alarmes | H | 30,30 |
| Secrets Manager | 10 segredos × 0,40 | H | 4,00 |
| KMS | Reserva para chaves/chamadas | H | 2,00 |
| ECR | 20 GB × 0,10 | H | 2,00 |
| WAF | Reserva para ACL, regras e baixo volume | H | 15,00 |
| Cognito M2M | 3 clientes × 4 tokens/h × 730 × 0,00225, arredondado para reserva | R | 20,00 |
| Backups excedentes, S3, DNS e tráfego adicional | Reserva inicial | H | 10,00 |
| **Base de planejamento** | Soma dos itens arredondados | | **506,33** |
| **Contingência de 25%** | Autoscaling, variação de uso e tarifas hipotéticas | | **126,58** |
| **Envelope inicial** | Base + contingência | | **632,91** |

As 50 consultas/s são pico, não média mensal. A estimativa assume 1 milhão de consultas e 1 milhão de lançamentos/mês, payload até 16 KiB, tokens reutilizados e logs de 30 GB. Reserva SQS inclui polling, envio, remoção e retries; medir ações reais, batching e tamanhos. ALB/egress/logs podem mudar significativamente se o pico for sustentado todo o mês. Instâncias burstable podem gerar créditos adicionais. Armazenamento cresce porque fatos e identidades ativas não expiram.

O preço de NAT é hipótese para us-east-1: a página oficial consultada apresenta exemplo regional, portanto deve ser reconfirmado no Calculator. A tarifa RDS e o armazenamento Multi-AZ também precisam de cotação com classe, engine e deployment exatos; não tratar esses valores hipotéticos como oferta vigente.

## Sensibilidade e exclusões

Mais uma task de 0,5 vCPU/1 GiB por 730 h adiciona aproximadamente USD 18,02 de compute, além de telemetria/rede. Dobrar todas as tasks adiciona cerca de USD 144,16. Implantação rolling usa capacidade temporária. Endpoints de interface podem substituir parte do NAT, mas precisam de orçamento por serviço/zona; não somar as duas topologias sem necessidade.

Excluídos: impostos/câmbio, suporte AWS pago, trabalho de engenharia/operação, CI/CD pago, domínio próprio, DR cross-region, SIEM corporativo e tráfego acima das hipóteses. Backup 35 dias pode superar a franquia conforme alterações; a reserva de USD 10 precisa ser recalculada. Homologação simultânea é ambiente adicional, não incluído no total produtivo. Não há MSK/RabbitMQ hospedado na AWS nesta topologia.

## Licenciamento

.NET, PostgreSQL, RabbitMQ, Keycloak e OpenTelemetry não exigem licença por instância para este desenho; seus termos devem ser preservados na distribuição. Docker Desktop pode exigir assinatura conforme organização, enquanto Docker Engine/Compose em Linux é opção local. Terraform e ferramentas de terceiros devem passar por revisão dos termos na implementação; não presumir que hospedagem/serviço gerenciado ou suporte seja gratuito. O custo local depende da máquina e da opção de distribuição Docker.

## Fontes e atualização

Fontes consultadas em 22/10/2026: [Fargate](https://aws.amazon.com/fargate/pricing/), [RDS PostgreSQL](https://aws.amazon.com/rds/postgresql/pricing/), [VPC/NAT/IPv4](https://aws.amazon.com/vpc/pricing/), [ALB](https://aws.amazon.com/elasticloadbalancing/pricing/), [SQS](https://aws.amazon.com/sqs/pricing/), [CloudWatch](https://aws.amazon.com/cloudwatch/pricing/), [Cognito](https://aws.amazon.com/cognito/pricing/), [Secrets Manager](https://aws.amazon.com/secrets-manager/pricing/), [KMS](https://aws.amazon.com/kms/pricing/), [ECR](https://aws.amazon.com/ecr/pricing/), [WAF](https://aws.amazon.com/waf/pricing/). Revisão de licenças: [Docker Desktop](https://docs.docker.com/subscription/desktop-license/), [Terraform](https://github.com/hashicorp/terraform/blob/main/LICENSE).

Antes de provisionar, exportar orçamento do [AWS Pricing Calculator](https://calculator.aws/) com todas as hipóteses, confirmar tarifas H, configurar AWS Budgets em 80% e 100% do envelope aprovado e comparar consumo real após uma semana. A atualização financeira é parte da implantação futura; a memória de cálculo documental está definida.
