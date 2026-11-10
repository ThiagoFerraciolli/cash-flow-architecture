# Estimativa de custos de infraestrutura e licenciamento

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

## Comparação econômica e de isolamento — um RDS versus dois RDS

> **Análise de alternativa, não mudança de infraestrutura:** a estimativa-base acima representa a decisão inicial de **dois RDS Multi-AZ independentes**. O desafio exige independência de Lançamentos quando a Consolidação falha, mas **não prescreve dois servidores de banco**. É necessário separar isolamento lógico, isolamento de recursos e isolamento de falhas. A avaliação abaixo usa **exatamente as hipóteses H da tabela-base**, não cotações verificadas da AWS.

### Alternativas

| Critério | A — 2 RDS Multi-AZ independentes (referência inicial) | B — 1 RDS Multi-AZ com 2 bancos lógicos ou schemas e roles distintos |
|---|---|---|
| Separação de domínio | Forte, por instância e credenciais | Forte no modelo e permissões; física compartilhada |
| CPU, memória, I/O, conexões | Isolados por instância | Compartilhados; Consolidação pode saturar Lançamentos |
| Falha/restart/failover de RDS | Afeta o contexto da instância | Afeta simultaneamente os dois contextos |
| Deploy de aplicação | Independente | Independente, desde que migrations sejam segregadas |
| Backup/PITR | Restore por instância/contexto | PITR restaura a instância inteira; recuperação seletiva exige export/import e reconciliação |
| Segurança | Blast radius menor; permissões distintas | Roles/schemas separados, mas superfície operacional comum |
| Custo fixo | Maior | Menor antes de redimensionamento |
| Risco de evolução | Mais infraestrutura e operação | Exige governança de recursos e capacidade conjunta |
| Adequação ao RNF de independência | Melhor isolamento contra falha de banco de Consolidação | Só atende se a falha simulada não afetar recursos compartilhados; **não garante isolamento contra falha/saturação do RDS comum** |

**Atenção:** duas instâncias não tornam toda a solução independente; VPC, região, IAM, rede e outros serviços ainda podem ser compartilhados. Da mesma forma, bancos lógicos separados em uma única instância não equivalem a isolamento físico.

### Memória de cálculo diferencial (USD/mês; hipóteses, não cotação)

| Parcela | A — 2 RDS | B — 1 RDS no mesmo tamanho inicial | Diferença B − A |
|---|---:|---:|---:|
| Compute RDS Multi-AZ: 730 h × USD 0,08 por instância | 116,80 | 58,40 | −58,40 |
| Storage RDS: 200 GiB totais × USD 0,25/GiB-mês | 50,00 | 50,00 | 0,00 |
| Demais itens do orçamento-base, mantidos iguais | 339,53 | 339,53 | 0,00 |
| **Base mensal** | **506,33** | **447,93** | **−58,40** |
| Contingência 25% | 126,58 | 111,98 | −14,60 |
| **Envelope mensal** | **632,91** | **559,91** | **−73,00** |

**Economia indicativa:** USD 58,40/mês de base (**11,5%**) ou USD 73,00/mês com contingência (**11,5%**), assumindo que a instância compartilhada tenha **o mesmo tamanho de uma das instâncias anteriores** e suporte a carga conjunta. Isso **não está demonstrado**. Se o RDS compartilhado precisar de classe maior, IOPS, armazenamento, pooling ou controles adicionais, a economia diminui ou pode desaparecer. Não interpretar a simulação como cotação AWS ou recomendação incondicional.

A hipótese de armazenamento mantém **200 GiB totais** para não inventar economia por reduzir dados; a configuração real de um RDS compartilhado poderia ser diferente. Preços H de compute, Multi-AZ, storage e backup exigem revisão no AWS Pricing Calculator, assim como limites de conexão e características da classe escolhida. Os valores da estimativa-base não constituem preços confirmados para novembro de 2026.

### Testes e critérios de decisão antes de escolher B

1. **Capacidade conjunta:** executar simultaneamente ingestão de lançamentos, publicação/consumo, rebuild e 50 GET/s do consolidado, medindo CPU, memória, IOPS, latência p95/p99, conexões, locks e tempo de resposta da escrita.
2. **Falha de aplicação:** parar API e Worker de Consolidação sem impedir confirmação de Lançamentos; isto valida isolamento de processos, não do banco.
3. **Contenção:** induzir consulta pesada, excesso de conexões, locks e I/O de Consolidação e demonstrar que o SLO de Lançamentos permanece atendido. Se não, usar limites efetivos ou preferir A.
4. **Falha do banco:** reinício/failover do RDS compartilhado afetará ambos os contextos. Registrar aceitação explícita desse domínio de falha pelo negócio; não alegar independência total.
5. **Restore seletivo:** ensaiar recuperação de apenas um contexto, com RPO/RTO, identidade financeira, deduplicação, outbox, replay e reconciliação. PITR da instância inteira pode retroceder ambos os contextos.
6. **Segurança e mudança:** roles sem acesso cruzado, migrations independentes, limites de conexão e políticas de operação testados.
7. **TCO real:** recalcular classe compartilhada, storage, I/O, backup, rede, monitoramento e operação para 1/3/12 meses, comparando cenários normal, pico e recuperação.

**Recomendação condicional:** a alternativa B deve ser o **baseline econômico a validar** quando a organização aceita o risco de falha compartilhada e os testes de contenção/restore comprovam os SLOs. A alternativa A deve prevalecer quando o isolamento de recursos e falhas entre os contextos é requisito efetivo, ou quando os testes da alternativa B falham. **Nenhuma delas está homologada em AWS neste case.** O ADR-003 registra essa decisão condicional; a topologia desenhada continua mostrando A como referência inicial até a revisão formal.

## Sensibilidade e exclusões

Mais uma task de 0,5 vCPU/1 GiB por 730 h adiciona aproximadamente USD 18,02 de compute, além de telemetria/rede. Dobrar todas as tasks adiciona cerca de USD 144,16. Implantação rolling usa capacidade temporária. Endpoints de interface podem substituir parte do NAT, mas precisam de orçamento por serviço/zona; não somar as duas topologias sem necessidade.

Excluídos: impostos/câmbio, suporte AWS pago, trabalho de engenharia/operação, CI/CD pago, domínio próprio, DR cross-region, SIEM corporativo e tráfego acima das hipóteses. Backup 35 dias pode superar a franquia conforme alterações; a reserva de USD 10 precisa ser recalculada. Homologação simultânea é ambiente adicional, não incluído no total produtivo. Não há MSK/RabbitMQ hospedado na AWS nesta topologia.

## Licenciamento

.NET, PostgreSQL, RabbitMQ, Keycloak e OpenTelemetry não exigem licença por instância para este desenho; seus termos devem ser preservados na distribuição. Docker Desktop pode exigir assinatura conforme organização, enquanto Docker Engine/Compose em Linux é opção local. Terraform e ferramentas de terceiros devem passar por revisão dos termos na implementação; não presumir que hospedagem/serviço gerenciado ou suporte seja gratuito. O custo local depende da máquina e da opção de distribuição Docker.

## Fontes e atualização

Fontes consultadas em 22/10/2026: [Fargate](https://aws.amazon.com/fargate/pricing/), [RDS PostgreSQL](https://aws.amazon.com/rds/postgresql/pricing/), [VPC/NAT/IPv4](https://aws.amazon.com/vpc/pricing/), [ALB](https://aws.amazon.com/elasticloadbalancing/pricing/), [SQS](https://aws.amazon.com/sqs/pricing/), [CloudWatch](https://aws.amazon.com/cloudwatch/pricing/), [Cognito](https://aws.amazon.com/cognito/pricing/), [Secrets Manager](https://aws.amazon.com/secrets-manager/pricing/), [KMS](https://aws.amazon.com/kms/pricing/), [ECR](https://aws.amazon.com/ecr/pricing/), [WAF](https://aws.amazon.com/waf/pricing/). Revisão de licenças: [Docker Desktop](https://docs.docker.com/subscription/desktop-license/), [Terraform](https://github.com/hashicorp/terraform/blob/main/LICENSE).

Antes de provisionar, exportar orçamento do [AWS Pricing Calculator](https://calculator.aws/) com todas as hipóteses, confirmar tarifas H, configurar AWS Budgets em 80% e 100% do envelope aprovado e comparar consumo real após uma semana. A atualização financeira é parte da implantação futura; a memória de cálculo documental está definida.
