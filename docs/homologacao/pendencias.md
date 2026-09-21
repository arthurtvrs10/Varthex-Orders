# Pendências da homologação

Complementa [Pendências de validação](../docs/13-pendencias-validacao.md), que guarda as **perguntas** (QV01–QV14), sem alterá-las. Aqui está o que o código e os documentos **realmente** dizem sobre cada uma hoje, quem responde e o que falta. Nenhuma QV tem resposta registrada pelo responsável; a única resolvida por uma fatia de código foi a QV02.

Classificação da situação:

- **Decidida no código:** o comportamento existe e a decisão está registrada (CHANGELOG, escopo ou documento).
- **Comportamento existente sem decisão registrada:** o aplicativo já faz alguma coisa, mas ninguém confirmou que é o que a loja precisa; a "resposta" é uma suposição do desenvolvimento.
- **Sem rastro:** nada no código nem na documentação depende ou trata da questão.

## QV01–QV14

| Código | Pergunta (resumo) | Decisor | Situação real | O que falta |
| --- | --- | --- | --- | --- |
| QV01 | Como o cliente é associado ao número (ficha, mesa, pulseira…) | Responsável | **Sem rastro.** O aplicativo só conhece o número da comanda; nada no código depende do método físico | O responsável dizer o método; entra no treinamento e no guia do operador |
| QV02 | Quantos números aparecem e podem ser digitados | Operação | **Decidida no código** (CHANGELOG 1.10): Configurações tem "quantidade máxima de comandas"; sem valor configurado não há teto; o número é digitado no campo do Atendimento. Nunca foi confirmada com a loja | Operação informar a quantidade real e configurá-la; nota: a massa mínima usa 20 |
| QV03 | Produtos por peso, meia porção, adicionais ou combinações | Operação | **Comportamento existente sem decisão registrada.** A quantidade é inteira positiva (RN06) e não há peso, adicional nem combinação; meia porção ou combo só cabem como um produto próprio | Operação confirmar se o cardápio real precisa de algo além disso; se sim, solicitação de mudança |
| QV04 | Texto que deixa claro que a cobrança foi fora do aplicativo | Caixa | **Comportamento existente sem decisão registrada.** A tela de Encerramento diz "Digite este valor na maquininha." e pergunta "A cobrança foi aprovada fora do sistema?"; ninguém do caixa validou a redação | Caixa validar (roteiro, parte 9; treinamento) |
| QV05 | Adquirente e modelo da maquininha | Responsável | **Sem rastro.** A integração com a maquininha está fora do MVP ([ADR 0003](../docs/adr/0003-pagamento-fora-mvp.md)); nada no código usa a informação | Responsável informar; só serve para estimar uma integração futura |
| QV06 | Desconto, acréscimo, cortesia ou estorno no MVP | Responsável | **Decidida no código/escopo, sem confirmação:** não há nenhum desses recursos; o [roadmap](../docs/11-roadmap-riscos.md) os coloca no "MVP 2" (descontos, cancelamento de venda). Cancelar **comanda** aberta existe (RF13); cancelar **venda** encerrada não | Responsável confirmar que pode esperar |
| QV07 | Versão, arquitetura, memória, resolução e escala do computador | Desenvolvimento | **Aberta.** O pacote é `win-x64` por suposição. Só há dados da máquina de desenvolvimento, como referência ([matriz](matriz-compatibilidade.md)) | Ler os dados do computador da loja e preencher a matriz; se não for x64, gerar outro pacote |
| QV08 | Impressora térmica necessária inicialmente | Responsável | **Decidida no escopo, sem confirmação:** impressão está fora do MVP ([escopo](../docs/01-escopo-mvp.md)); o aplicativo não imprime | Responsável confirmar (prazo: antes da homologação) |
| QV09 | Dispositivo externo dos backups | Responsável | **Comportamento existente sem decisão registrada.** O aplicativo aceita qualquer pasta externa (pendrive, disco, rede local), configurável em Configurações e pela tela de Backup; nenhum dispositivo foi definido nem a troca de pendrive foi testada | Responsável escolher o dispositivo; executar o roteiro, parte 10 |
| QV10 | Quem altera preços, cancela comandas e restaura backup | Responsável | **Decidida no escopo, sem confirmação:** "usuários e permissões" estão fora do MVP; **qualquer pessoa** que abra o aplicativo faz tudo isso (as ações destrutivas pedem confirmação, RNF08) | Responsável aceitar o risco ou pedir usuários/permissões (MVP 2) |
| QV11 | Por quanto tempo manter o histórico | Responsável | **Comportamento existente sem decisão registrada.** O histórico é mantido sem limite e sem poda automática (a rotação existe só para logs e backups) | Responsável definir; se houver prazo, é mudança nova |
| QV12 | O computador é compartilhado por várias contas do Windows | Responsável | **Comportamento existente e documentado, sem decisão:** programa e dados ficam por conta do Windows ([documento 12](../docs/12-operacao-implantacao.md)); uma segunda conta teria banco próprio e vazio | Responsável responder; se for compartilhado, definir uma conta única para o aplicativo |
| QV13 | Iniciar automaticamente com o Windows | Operação | **Comportamento existente sem decisão registrada.** O instalador cria só atalhos na Área de Trabalho e no Menu Iniciar; não há início automático | Operação decidir (prazo: antes da homologação); se sim, é mudança |
| QV14 | Limite de itens, quantidade e valor por comanda | Responsável | **Comportamento existente sem decisão registrada.** Nenhum limite por comanda no código (só quantidade inteira positiva e preço maior que zero; o teto é o número máximo de comandas, QV02). Não existem testes de limite | Responsável definir os limites; só então escrever testes de limite |

Ao responder qualquer QV, siga o "Registro da decisão" do [documento 13](../docs/13-pendencias-validacao.md) (registrar a resposta, atualizar requisitos e regras, testes, CHANGELOG e, se houver impacto duradouro, um ADR) e atualize a linha correspondente desta tabela.

## Pendências técnicas

Cada item indica o dono provável e a situação. Nenhuma delas foi resolvida por esta etapa (que só produz documentos e evidências); nenhuma impede o uso em teste, mas as marcadas com (H) bloqueiam a homologação.

### Provas que exigem pessoa ou máquina

| Item | Situação | Dono |
| --- | --- | --- |
| **(H)** CT21: instalar o pacote num Windows limpo, sem nenhum .NET | Não executado; a máquina de desenvolvimento tem o SDK. Roteiro, parte 11 | Pessoa com uma VM ou computador de teste |
| **(H)** CT13 físico: operar com a rede desligada no computador da loja | Só provas de apoio (estrutural e E6). Roteiro, parte 3 | Pessoa no computador da loja |
| **(H)** Computador da loja desconhecido (QV07): RNF02, RNF03, RNF07, RNF17 | RNF03 medido só na máquina de desenvolvimento (1736 ms); os demais sem medição | Desenvolvimento e pessoa no local |
| **(H)** Teclado físico no aplicativo real (CT17) | Só testes de ViewModel e XAML; a **última rodada de ajustes de foco da fatia de teclado teve só verificação ao vivo parcial**. Roteiro, parte 4 | Pessoa com teclado |
| Escalas 100/150/200 % | Só 100 % observada, na máquina de desenvolvimento | Pessoa no computador da loja |
| Reinício do computador com comanda aberta | Só o término forçado do processo (E3) foi ensaiado. Roteiro, parte 7 | Pessoa no computador da loja |
| Maquininha real (CT09) | E4 simulou a falha sem a maquininha. Roteiro, parte 9 | Pessoa com a maquininha |
| Restauração pela caixa "Selecionar arquivo..." | **Não automatizada:** o diálogo de arquivos do Windows não expõe padrões de UI Automation. A recusa do corrompido foi exercitada pelo modo de restauração (E5-B). Roteiro, parte 10.2 | Pessoa |
| Troca de pendrive e ensaio trimestral de restauração no computador real | Não executados. Roteiro, parte 10 | Pessoa |
| Treinamento dos operadores | O guia existe ([guia-do-operador.md](guia-do-operador.md)); o treinamento não | Responsável |
| Antivírus e SmartScreen com o executável | Não avaliados | Pessoa, na instalação |
| Restauração de um backup **antigo** (esquema da primeira migração) pela tela | Coberto por teste de migração (CT16), mas não ensaiado com o app real | Pessoa (roteiro, parte 10.2) |

### Limitações conhecidas do produto (registradas pelas fatias anteriores)

| Item | Situação | Observação |
| --- | --- | --- |
| Sem alerta de pouco espaço em disco (R15) | Não implementado | O [risco R15](../docs/11-roadmap-riscos.md) prevê alerta; hoje só há rotação e teto de logs (5 MB por arquivo, 30 arquivos). O teste "disco com pouco espaço" do [documento 09](../docs/09-testes-aceitacao.md) não existe |
| `corrompido-*.db.bak` nunca é podado | Limitação | Cada restauração a partir de banco corrompido guarda uma cópia bruta que fica para sempre; cresce com o uso |
| Reiniciar após restaurar consome um arquivo de log da retenção (30) | Limitação | O reinício abre um novo arquivo de log (CHANGELOG 1.12); a retenção é de 30 arquivos, então reinícios frequentes empurram os mais antigos para fora |
| O `.fotos.zip` do backup é gerado no thread da interface | Limitação | Só ocorre quando as fotos mudam; a tela pode ficar parada durante a geração do zip (CHANGELOG 1.15) |
| Sem assinatura de código | Limitação | Pode disparar o SmartScreen e alertas de antivírus |
| Sem instalador MSI/Inno/WiX/MSIX | Decisão adiada | A implantação é pelo pacote `.zip` com `Instalar.cmd`; sem atualização automática; retorno à versão anterior é manual |
| Instalação e dados por conta do Windows (QV12) | Comportamento documentado | Ver QV12 |
| Enter no campo de número **seleciona** a comanda já aberta em vez de mostrar erro | Comportamento por decisão da fatia de teclado | O critério do CT02 ("recusar duplicidade") é atendido pelo banco; confirmar com a loja que a seleção é o desejado |
| A tecla `s` (sem `Alt`) aciona "Sim" nas confirmações | Limitação (CHANGELOG 1.14) | Risco de confirmar sem querer; decidir se se mantém |
| Foco visível: sem estilo para listas e cabeçalhos de colunas; a cor do foco não acompanha o modo de alto contraste do Windows; nomes de acessibilidade (leitor de tela) só nos campos de número e busca | Limitação (CHANGELOG 1.14) | Afeta RNF18 além do teclado básico |
| CT06: quantidade fracionária coberta só por construção | Limite da prova | Não há campo de digitação que permita tentar 1,5 |
| CT19: só a trava (mutex) tem teste unitário; a ordem em `App.OnStartup` não é testada | Limite da prova | O ensaio E2 cobre o comportamento com o aplicativo real |
| CT08: total exibido conferido no ViewModel, sem teste visual de pixel | Limite da prova | — |
| `RestaurarPara` chamado direto grava cópia preventiva antes de falhar na integridade | Detalhe do serviço | Não é o caminho da tela (que valida antes e não escreve); sem efeito funcional |
| "Polimento do Histórico" | Sem requisito concreto | Ficou fora da fatia de acabamento (Etapa 7) por não haver requisito; precisa de um pedido específico |
| Mensagens de erro de fluxo sem padrão único | Ficou fora da fatia de acabamento | As janelas de aviso foram unificadas; as mensagens de erro dos fluxos ainda não |
| Fotos órfãs | Só registradas no log | O aplicativo conta, mas não apaga fotos sem produto |

### Resolvido, para registro

- A coluna "Ativo" da lista de Produtos passou a mostrar "Sim/Não" (CHANGELOG 1.15).
- As fotos entram no backup e voltam na restauração (CHANGELOG 1.15).
- As janelas nativas de aviso viraram a janela grande de aviso no padrão de toque (CHANGELOG 1.15).
