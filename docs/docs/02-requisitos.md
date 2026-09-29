# Requisitos

## Requisitos funcionais

| Código | Prioridade | Requisito | Comportamento esperado |
| --- | --- | --- | --- |
| RF01 | Alta | Cadastrar categoria | Informar nome e situação ativa |
| RF02 | Alta | Alterar e desativar categoria | Preservar produtos e histórico existentes |
| RF03 | Alta | Cadastrar produto | Informar nome, categoria, preço e situação ativa |
| RF04 | Alta | Alterar e desativar produto | Novos lançamentos usam o novo preço sem alterar o passado |
| RF05 | Alta | Localizar produtos | Filtrar por categoria e nome |
| RF06 | Alta | Exibir números de comanda | Distinguir números livres e ocupados |
| RF07 | Alta | Abrir comanda | Criar sessão com número livre, data e hora |
| RF08 | Alta | Consultar comanda aberta | Mostrar itens, quantidades, subtotais e total |
| RF09 | Alta | Adicionar item | Lançar produto ativo com um clique ou toque |
| RF10 | Alta | Alterar quantidade | Aceitar somente quantidade inteira positiva |
| RF11 | Alta | Remover item | Exigir confirmação quando necessário |
| RF12 | Alta | Calcular total | Recalcular subtotais e total após cada alteração |
| RF13 | Alta | Cancelar comanda | Confirmar, marcar como cancelada e liberar o número |
| RF14 | Alta | Iniciar encerramento | Revalidar estado, itens, subtotais e total |
| RF15 | Alta | Exibir total a pagar | Mostrar itens e total para digitação manual na maquininha |
| RF16 | Alta | Manter comanda aberta | Permitir retorno se a cobrança externa não for concluída |
| RF17 | Alta | Encerrar comanda | Gravar venda e fechamento em transação única, sem pagamento |
| RF18 | Alta | Consultar histórico | Filtrar por data e localizar pelo número da comanda |
| RF19 | Alta | Exibir detalhes da venda | Mostrar itens, quantidades, preços, total e horário |
| RF20 | Média | Exibir resumo diário | Informar quantidade de vendas, total e ticket médio |
| RF21 | Alta | Criar backup automático | Gerar cópia datada conforme política |
| RF22 | Alta | Criar backup manual | Permitir pasta local ou unidade externa |
| RF23 | Alta | Validar backup | Verificar formato, versão, integridade e checksum |
| RF24 | Alta | Restaurar backup | Validar, criar cópia preventiva, restaurar e reiniciar |
| RF25 | Média | Configurar estabelecimento | Manter nome, números de comanda e pasta de backup |
| RF26 | Média | Registrar falhas técnicas | Gravar log local sem dados sensíveis |
| RF27 | Alta | Recuperar atendimento interrompido | Ao iniciar, carregar comandas abertas e seus itens sem duplicar ou descartar lançamentos confirmados |

## Requisitos não funcionais

| Código | Categoria | Requisito | Verificação |
| --- | --- | --- | --- |
| RNF01 | Disponibilidade | Todas as funções do MVP operam sem internet | Teste com rede desconectada |
| RNF02 | Desempenho | Ação comum responde em até 500 ms no equipamento-alvo | 95% das medições |
| RNF03 | Inicialização | Aplicativo pronto em até 5 segundos em condição normal | Média de cinco execuções |
| RNF04 | Integridade | Encerramento usa transação ACID | Teste de falha e rollback |
| RNF05 | Durabilidade | Venda confirmada permanece após reinício inesperado | Teste de reinício |
| RNF06 | Usabilidade | Adicionar produto frequente exige no máximo dois cliques após abrir a comanda | Teste de tarefa |
| RNF07 | Legibilidade | Texto e controles possuem contraste e tamanho confortáveis | Revisão no equipamento |
| RNF08 | Prevenção de erro | Ação destrutiva exige confirmação e explica o efeito | Teste de interface |
| RNF09 | Portabilidade | Instalador inclui runtime necessário | Instalação limpa |
| RNF10 | Armazenamento | Dinheiro é persistido como inteiro em centavos | Inspeção de esquema |
| RNF11 | Backup | Cópia automática não bloqueia atendimento por mais de 2 segundos | Teste com base de referência |
| RNF12 | Recuperação | Arquivo inválido é rejeitado antes de substituir a base | Teste com arquivo corrompido |
| RNF13 | Privacidade | Não solicita CPF nem telefone; nome do cliente é opcional, por solicitação do responsável em 29/09/2026 | Inspeção de telas e banco |
| RNF14 | Segurança local | Pasta de dados usa permissões do usuário do sistema operacional | Inspeção de permissões |
| RNF15 | Manutenibilidade | Banco evolui por migrações versionadas e reversíveis por backup | Teste de atualização |
| RNF16 | Observabilidade | Falhas geram mensagem compreensível e log técnico local | Teste de exceções |
| RNF17 | Compatibilidade | O aplicativo deve ser homologado no Windows e na versão e arquitetura registradas para o computador-alvo | Matriz de compatibilidade |
| RNF18 | Acessibilidade | Operações essenciais funcionam por teclado e possuem foco visível | Teste de navegação |
| RNF19 | Instância única | Somente uma instância pode usar a base local em uma sessão do Windows | Tentativa de segunda abertura |
| RNF20 | Recuperação de sessão | Comandas abertas persistidas devem reaparecer após encerramento inesperado | Teste de término forçado |
| RNF21 | Controle de logs | Logs devem ter rotação e limite configurado para não preencher o disco | Teste de retenção e tamanho |

## Dependências

RF29: informar, corrigir e remover um nome opcional de até 80 caracteres na comanda
aberta; exibir na grade, no encerramento e no histórico. O número continua sendo
o identificador; nomes repetidos são permitidos.

RF28: ativar e renovar licença offline por chave assinada vinculada ao computador,
conforme [Licenciamento offline](20-licenciamento-offline.md). Planos vitalício ou
1, 2 e 3 meses; sem integração de pagamentos.

- RF09 depende de RF03 e de produto ativo.
- RF14 a RF17 dependem de comanda aberta com ao menos um item.
- RF18 e RF19 dependem de venda criada pelo RF17.
- RF20 considera apenas vendas concluídas.
- RF24 depende de backup validado pelo RF23.
- RF27 depende da persistência transacional de RF07 a RF12.
