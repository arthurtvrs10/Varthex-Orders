# Matriz de compatibilidade (RNF17)

RNF17: o aplicativo deve ser homologado no Windows, na versão e na arquitetura registradas para o **computador-alvo**. O computador-alvo (o da loja) ainda não é conhecido: é a pendência QV07 ([pendencias.md](pendencias.md)).

Aplicativo: Varthex Comanda **1.0.0**, pacote `VarthexComanda-1.0.0-win-x64.zip` (autocontido, cerca de 64 MB, `win-x64`). Enquanto QV07 estiver aberta, o pacote é `win-x64` ([Plataforma Windows e .NET](../docs/19-plataforma-windows-dotnet.md)). Se o computador da loja for ARM64 ou 32 bits, este pacote não serve e é preciso gerar outro.

## Matriz

| Máquina | Windows | Versão | Arquitetura | RAM | Resolução | Escala | Toque | Resultado |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Máquina de desenvolvimento** (*referência, não é o computador-alvo*) | Windows 11 Pro | 10.0.26200 | x64 (64 bits) | cerca de 14,9 GB | 1920 × 1080 | 100 % (96 DPI) | Não verificado | Executa o aplicativo 1.0.0 instalado do pacote: ensaios E1–E6 aprovados em 2026-09-21 ([evidência](evidencias/ensaio-2026-09-21.txt)). RNF03 medido: **1736 ms** de média (5 aberturas; maior 2330 ms; meta ≤ 5000 ms). **Não vale como homologação do computador da loja** |
| **Computador da loja** (*a preencher*) | | | | | | | | |

Legenda do campo **Resultado** para o computador da loja: descreva o que foi executado ([roteiro-ensaio-manual.md](roteiro-ensaio-manual.md)) e o veredito (Aprovado, Reprovado ou Bloqueado), com data e quem executou.

## Escalas de exibição a testar no computador da loja

O documento [Testes e aceitação](../docs/09-testes-aceitacao.md) exige 100 %, 150 % e 200 %, e o [documento 07](../docs/07-experiencia-usuario.md) pede legibilidade entre 100 % e 200 %.

| Escala | Resolução efetiva | Telas conferidas (Atendimento, Encerramento, Produtos, Histórico, Backup, Configurações) | Nada cortado ou sobreposto? | Resultado |
| --- | --- | --- | --- | --- |
| 100 % | | | | |
| 150 % | | | | |
| 200 % | | | | |

Na máquina de desenvolvimento **só a escala de 100 % foi observada**; 150 % e 200 % **não foram testadas** em nenhuma máquina.

## Como ler os dados do computador-alvo

- Windows, versão e arquitetura: `Configurações > Sistema > Sobre` (Edição, Versão, Compilação do SO, Tipo de sistema), ou `winver` e `systeminfo` no Prompt de Comando.
- RAM: `Configurações > Sistema > Sobre` (Memória instalada).
- Resolução e escala: `Configurações > Sistema > Tela` (Resolução de tela, Escala).
- Toque: em `Configurações > Sistema > Sobre` consta "Caneta e toque" (informa se há toque e quantos pontos); na dúvida, observe se a tela responde ao dedo. Se houver toque, teste os botões da grade de comandas e do Encerramento com o dedo.
- Não registre nome de usuário, nome do computador nem qualquer dado pessoal nesta matriz.

## Fora da matriz (ainda sem verificação)

- Comportamento do antivírus e do SmartScreen com o executável (sem assinatura de código).
- Windows anterior ao 10 e edições Server: não avaliados.
- Duas contas do Windows no mesmo computador: cada conta tem instalação e banco próprios (QV12).
