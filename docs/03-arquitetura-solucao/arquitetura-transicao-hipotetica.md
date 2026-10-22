# Transição arquitetural e aplicabilidade

**Estado: DOCUMENTAÇÃO APROVADA.** O PDF de Soluções condiciona transição à necessidade de migração de legado. Não há legado informado; portanto esse diferencial é **não aplicável ao escopo**, sem pendência de arquitetura.

A passagem efetiva local→AWS está definida no [plano de implantação](../07-infraestrutura/implantacao-e-operacao.md), incluindo homologação, adaptador SQS, identidade, dados e reversão. Ela não pressupõe migrar dados produtivos de uma máquina local.

## Referência opcional para futuro legado

Se existir um monólito, a sequência proposta é: inventariar regras/dados/dependências; definir fonte financeira e identidades; introduzir publicação confiável sem dual write; carregar snapshot na projeção nova e capturar alterações posteriores; reconciliar em paralelo sem oferecer duas fontes autoritativas; migrar consultas gradualmente; manter plano de retorno; separar registro apenas após preservação de identidade e trilha financeira.

Critérios de corte seriam igualdade por snapshot, tratamento de eventos posteriores, ausência de duplicidade e teste de retorno. Cronograma, CDC e tecnologia dependeriam do inventário real. Esta referência não é execução planejada nem requisito adicional dos PDFs.
