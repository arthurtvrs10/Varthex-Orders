# Operação e implantação

## Instalação

1. registrar versão, arquitetura, memória, resolução e escala do Windows alvo;
2. publicar o aplicativo .NET de forma autocontida para a arquitetura confirmada;
3. criar `%LOCALAPPDATA%\VarthexComanda` com dados separados dos binários;
4. aplicar migrações;
5. criar configuração inicial;
6. testar leitura e escrita;
7. configurar pasta de backup;
8. cadastrar catálogo;
9. executar teste completo offline;
10. registrar versão instalada.

O usuário não precisa instalar o SDK do .NET. A implantação usa o **pacote autocontido** `VarthexComanda-<versão>-win-x64.zip` (gerado por `scripts\publicar.ps1`, veja o documento 19), que traz o runtime do .NET e os scripts `Instalar.cmd`, `Instalar.ps1`, `Desinstalar.cmd` e `Desinstalar.ps1`. Um instalador `.msi`/Inno Setup/WiX não foi adotado; fica para quando atualização e distribuição estiverem estabilizadas.

### Procedimento com o pacote

1. Copie o `.zip` para o computador, extraia-o **inteiro** numa pasta e dê dois cliques em `Instalar.cmd` (sem administrador). O script copia os arquivos para `%LOCALAPPDATA%\Programs\VarthexComanda`, grava `versao-instalada.txt` nessa pasta e cria o atalho `Varthex Comanda` na Área de Trabalho e no Menu Iniciar.
2. Para outra pasta ou sem atalhos, execute no Prompt de Comando ou no PowerShell, dentro da pasta extraída, `.\Instalar.cmd -Destino "<pasta>" -SemAtalhos` (os dois parâmetros são opcionais e independentes; o destino deve ser uma pasta local dedicada ao programa, nunca um caminho de rede, e os scripts recusam destinos perigosos como a raiz do disco, o perfil do usuário e a pasta de dados). `-WhatIf` simula sem alterar nada.
3. Abra o aplicativo pelo atalho e siga os passos manuais abaixo.

**Uma instalação e um conjunto de dados por conta do Windows.** O programa (`%LOCALAPPDATA%\Programs\VarthexComanda`) e os dados (`%LOCALAPPDATA%\VarthexComanda`) ficam no perfil de quem instalou. Com duas contas do Windows no mesmo computador, cada conta tem a sua própria instalação e o seu próprio banco, separado e vazio no começo; os dados de uma conta não aparecem na outra. Definir se o computador é compartilhado é a pendência aberta QV12 (documento 13).

Correspondência com os 10 passos:

| Passo | Como é feito |
| --- | --- |
| 1. registrar Windows, arquitetura, memória, resolução e escala | **Manual** (também responde QV07, ainda em aberto) |
| 2. publicar autocontido | `scripts\publicar.ps1` na máquina de desenvolvimento gera o `.zip` (`win-x64`) |
| 3. pasta de dados separada dos binários | O instalador só grava em `%LOCALAPPDATA%\Programs\VarthexComanda` e nunca toca em `%LOCALAPPDATA%\VarthexComanda`; o próprio aplicativo cria essa pasta ao iniciar |
| 4. aplicar migrações | Automático, na primeira abertura do aplicativo |
| 5. configuração inicial | **Manual**, na tela Configurações |
| 6. testar leitura e escrita | **Manual** (abrir e fechar uma comanda de teste e conferir no Histórico) |
| 7. configurar pasta de backup | **Manual**, na tela Configurações |
| 8. cadastrar catálogo | **Manual**, na tela Produtos |
| 9. teste completo offline | **Manual**, com a rede desligada |
| 10. registrar versão instalada | O instalador grava `versao-instalada.txt` (linhas `Versao:` e `Instalada em:`); a versão também aparece no rodapé da tela Configurações. Anote-a no registro da implantação |

### O que o instalador recusa

- **App aberto:** se o `VarthexComanda.exe` do destino estiver em execução, o script termina com "Feche o Varthex Comanda e tente de novo." e nada é alterado.
- **Pacote incompleto:** `VarthexComanda.exe` não está ao lado do script (por exemplo, executado de dentro do zip).
- **Destino perigoso:** raiz de disco; caminho com menos de três níveis abaixo da raiz; pasta dentro da pasta de dados (`%LOCALAPPDATA%\VarthexComanda` ou o valor de `VARTHEX_COMANDA_DADOS`); pasta que seja, ou contenha, o perfil do usuário, `%LOCALAPPDATA%`, `%APPDATA%`, a Área de Trabalho, Documentos, o Menu Iniciar (Programas), `Program Files`, `Program Files (x86)`, a pasta do Windows ou `%TEMP%`; pasta que esteja dentro da pasta do pacote, ou que a contenha.
- **Pasta não vazia que não é instalação:** o destino tem conteúdo mas não tem `VarthexComanda.exe`.
- Se a cópia falhar no meio, o script remove o que copiou e, numa atualização, restaura a instalação anterior.

O instalador não compara versões: reinstalar a mesma versão, ou uma mais antiga, também é aceito.

### Situação da validação do pacote

Verificado em máquina de desenvolvimento: o pacote `VarthexComanda-1.0.0-win-x64.zip` (cerca de 64 MB, cerca de 430 arquivos, autocontido, multiarquivo) é gerado; uma cópia instalada iniciou **sem `dotnet` no `PATH`** e com pasta de dados descartável (`VARTHEX_COMANDA_DADOS`), a janela apareceu e o log registrou "Iniciando Varthex Comanda 1.0.0".

**Pendente de validação humana** (não feito): CT21 num Windows limpo, sem nenhum .NET instalado (exige outra máquina ou VM); comportamento do antivírus; versão e arquitetura do Windows-alvo (QV07); instalação com atalhos no computador do balcão.

## Rotina diária

1. abrir o aplicativo e verificar aviso de integridade;
2. confirmar produtos e preços;
3. registrar cada item na entrega;
4. conferir itens e total no caixa;
5. digitar o total na maquininha;
6. encerrar somente após aprovação externa;
7. consultar resumo no fim do dia;
8. confirmar backup e cópia externa.

## Atualização

1. encerrar comandas abertas ou adiar;
2. criar e validar backup;
3. registrar versão atual;
4. executar instalador e migrações;
5. testar catálogo, última venda, nova comanda e backup;
6. manter procedimento de retorno com a versão e a cópia anteriores.

Com o pacote:

1. feche o Varthex Comanda (o instalador recusa continuar com o aplicativo aberto);
2. extraia o pacote da versão nova e execute `Instalar.cmd`;
3. o script mostra "Atualizando de X para Y" (X vem de `versao-instalada.txt` da instalação existente, ou da versão do executável se o arquivo não existir), move a instalação atual para `%LOCALAPPDATA%\Programs\VarthexComanda.anterior` (uma só cópia, substituída a cada atualização; se já existir uma pasta com esse nome que não contenha `VarthexComanda.exe`, o script recusa e não apaga nada até que ela seja renomeada ou removida), copia os arquivos novos, regrava `versao-instalada.txt` e recria os atalhos;
4. os dados em `%LOCALAPPDATA%\VarthexComanda` não são tocados; as migrações do banco ocorrem na primeira abertura da versão nova (com cópia preventiva, se houver migração pendente);
5. faça os testes do passo 5 acima.

**Retorno manual** (não há atualização nem retorno automáticos): se a versão nova apresentar problema, feche o aplicativo; renomeie `%LOCALAPPDATA%\Programs\VarthexComanda` para `VarthexComanda.defeituosa`; renomeie `VarthexComanda.anterior` para `VarthexComanda`; abra pelo atalho. Se a versão nova já migrou o banco, restaure um backup feito antes da atualização (tela de Configurações). `Desinstalar.cmd` também apaga a pasta `.anterior`; faça o retorno antes de desinstalar.

**Desinstalar:** `Desinstalar.cmd` (dois cliques; parâmetro opcional `-Destino`, por exemplo `.\Desinstalar.cmd -Destino "<pasta>"`) recusa continuar com o aplicativo aberto, ou sem nenhuma instalação encontrada; remove a pasta do programa **inteira** (não guarde arquivos próprios nela), a cópia `.anterior` e os atalhos "Varthex Comanda" que apontam para essa instalação; nunca remove a pasta de dados.

## Diagnóstico

Coletar:

- versão do aplicativo (rodapé da tela Configurações, `versao-instalada.txt` na pasta do programa ou a linha "Iniciando Varthex Comanda <versão>" no início do log);
- sistema operacional;
- horário do erro;
- operação executada;
- mensagem exibida;
- trecho do log sem dado sensível;
- resultado do `PRAGMA integrity_check`;
- espaço disponível em disco.

O executável instalado chama-se `VarthexComanda.exe` (pasta `%LOCALAPPDATA%\Programs\VarthexComanda`). Também verificar se já existe outra instância do `VarthexComanda.exe`, se a pasta em `%LOCALAPPDATA%` está acessível e se o antivírus bloqueou o executável ou algum arquivo nativo do SQLite.

Nunca solicitar foto de cartão, senha, token ou credencial da conta da maquininha.
