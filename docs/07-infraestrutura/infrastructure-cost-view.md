# Infrastructure / Cost View

- **Classificação:** DIFERENCIAL — estimativa de infraestrutura/licenciamento
- **Status:** ESTIMATIVA PRELIMINAR
- **Moeda:** USD/mês
- **Região de referência:** us-east-1

## 1. Objetivo e limite

O case não fornece volume diário, tamanho médio de evento, retenção, SLA, RTO, RPO, região ou crescimento. Portanto, esta view apresenta **ordem de grandeza baseada em hipóteses declaradas**, e não orçamento produtivo definitivo.

O único indicador explícito de carga é 50 req/s no consolidado, sem especificação da operação. Ele será usado como referência de capacidade/teste, mas não será convertido artificialmente em 50 req/s contínuos durante 24x7 para estimar armazenamento ou tráfego.

## 2. Premissas de sizing para comparação

### Cenário A — referência enxuta
- workloads Linux/x86 em ECS/Fargate;
- 0,25 vCPU / 0,5 GB por task como ponto inicial de teste, sujeito a profiling;
- uma task por responsabilidade em ambiente de referência;
- PostgreSQL gerenciado em configuração pequena compatível com o workload de demonstração;
- armazenamento inicial na ordem de dezenas de GB;
- observabilidade com retenção controlada;
- sem compromisso de Savings Plan na estimativa base.

### Cenário B — produção de referência
- pelo menos duas instâncias das APIs críticas distribuíveis entre AZs;
- workers escaláveis independentemente;
- banco com configuração de disponibilidade/backup compatível com metas a serem validadas;
- observabilidade, segredos e criptografia habilitados;
- event streaming gerenciado se Kafka for mantido.

Os tamanhos definitivos dependem de profiling e dos RNFs ainda abertos.

## 3. Compute reproduzível — Fargate

A página oficial do Fargate publica, para Linux/x86 em us-east-1, **US$ 0,000011244 por vCPU-segundo** e **US$ 0,000001235 por GB-segundo**. O serviço é cobrado pelos recursos solicitados e não exige aquisição de servidores.

Exemplo de ordem de grandeza para uma task 24x7 com 0,25 vCPU e 0,5 GB, usando 730 h/mês:

- CPU ≈ `0.25 × 0.0404784 × 730` = **US$ 7,39/mês**;
- memória ≈ `0.5 × 0.004446 × 730` = **US$ 1,62/mês**;
- total ≈ **US$ 9,01/task/mês**, antes de rede/logs e sem armazenamento efêmero adicional.

Assim, quatro responsabilidades continuamente ativas nessa configuração mínima ficam na ordem de **US$ 36/mês de compute Fargate**. Isso é somente referência matemática; não afirma que 0,25 vCPU/0,5 GB seja sizing produtivo aprovado.

## 4. Componentes de custo

| Componente | Driver principal | Tratamento nesta estimativa |
|---|---|---|
| ECS/Fargate | vCPU, memória, duração | estimável desde já; ~US$ 9/task-mês na configuração mínima acima |
| RDS PostgreSQL | classe, horas, storage, I/O, backup, HA | ordem de grandeza depende de classe/Multi-AZ; fechar após profiling/RPO-RTO |
| Amazon MSK | modalidade, capacidade/throughput, storage/tráfego | ponto potencialmente dominante; confrontar com SQS |
| ALB | horas + LCUs | baixo/moderado para esta escala; depende de tráfego/conexões |
| CloudWatch | ingestão/retenção de logs, métricas | controlar cardinalidade e retenção |
| Secrets Manager/KMS | segredos, chamadas/chaves | pequeno frente a banco/broker, mas não zero |
| ECR | storage/transfer | pequeno para poucas imagens |
| transferência | volume e direção | não estimável sem payload/volume diário |

## 5. Kafka versus SQS — impacto econômico

O benefício de Kafka nesta arquitetura é retenção/replay e reprocessamento, não throughput de 50 req/s. Amazon SQS possui modelo pay-per-request e a AWS informa 1 milhão de requests/mês no Free Tier, tornando-o economicamente atraente para uma integração simples de baixa/média escala.

Por isso, a decisão é apresentada assim:

| Opção | Vantagem | Custo/complexidade | Quando escolher |
|---|---|---|---|
| Kafka / Amazon MSK | replay, retenção, múltiplos consumidores, evolução para event streaming | maior custo e operação/conhecimento | quando replay/event log e evolução justificarem |
| Amazon SQS | simplicidade, custo por uso, DLQ nativa | não oferece o mesmo modelo de log/replay de Kafka | quando o requisito for somente entrega assíncrona confiável |

**Conclusão de custo:** com os requisitos atualmente conhecidos, SQS tende a ser mais econômico; Kafka permanece tecnicamente defensável pelo requisito arquitetural de recuperação/replay adotado, mas deve ser reavaliado se o custo total não justificar essas capacidades.

## 6. Licenciamento

| Tecnologia | Licença de software na solução | Observação |
|---|---:|---|
| .NET | US$ 0 | runtime/SDK sem licença comercial por instância |
| PostgreSQL | US$ 0 | custo em cloud é serviço/infra/operação, não licença comercial do PostgreSQL |
| Apache Kafka | US$ 0 | custo do MSK é serviço gerenciado/infra; não licença Kafka |
| OpenTelemetry | US$ 0 | custo ocorre no backend/armazenamento/telemetria |

Não confundir **licença zero** com **TCO zero**: operação, suporte, storage, rede e observabilidade permanecem custos reais.

## 7. Faixa preliminar

Sem inventar classe de RDS/MSK e disponibilidade que o case não forneceu, a estimativa responsável neste estágio é:

- **compute de aplicação:** dezenas de USD/mês no cenário mínimo;
- **banco gerenciado:** dezenas a centenas de USD/mês conforme classe e HA;
- **event streaming Kafka gerenciado:** pode elevar significativamente o baseline e deve ser validado contra SQS;
- **observabilidade/edge/secrets/storage/rede:** adicionais variáveis conforme volume e retenção;
- **produção com redundância:** naturalmente acima do cenário mínimo.

Um total único neste ponto criaria falsa precisão porque banco, broker, HA e volume representam parcela relevante e não estão especificados no desafio.

## 8. Como tornar a estimativa definitiva

Para fechar um orçamento produtivo, validar:
1. região de produção;
2. lançamentos/dia e pico de escrita;
3. tamanho médio de payload/evento;
4. retenção online e de Kafka;
5. SLA, RTO e RPO;
6. Multi-AZ/DR;
7. crescimento esperado;
8. retenção e volume de logs/traces;
9. tráfego de saída;
10. Kafka versus SQS após validar necessidade real de replay/múltiplos consumidores.

## 9. Fontes de preço

Os números reproduzíveis de Fargate devem ser verificados na página oficial AWS antes da entrega final, pois preços mudam. Os demais componentes serão recalculados no AWS Pricing Calculator quando as premissas acima forem definidas.

Referências oficiais utilizadas na elaboração desta estimativa:
- AWS Fargate Pricing: https://aws.amazon.com/fargate/pricing/
- Amazon ECS Pricing: https://aws.amazon.com/ecs/pricing/
- Amazon SQS FAQ/Pricing guidance: https://aws.amazon.com/sqs/faqs/
- Amazon MSK: https://aws.amazon.com/msk/
- Amazon RDS Pricing: https://aws.amazon.com/rds/pricing/
