# Licenciamento offline

Decisão autorizada pelo responsável em 29/09/2026. O licenciamento comercial
é separado das vendas de comandas: não integra cobrança nem registra pagamentos
dos consumidores. Mantém RN11 e RNF01.

## Regras

- RF28: ativar e renovar por chave assinada, vinculada ao código do computador.
- RN24: planos vitalício ou 1, 2 e 3 meses de calendário, com início e vencimento
  fixos escolhidos pelo emissor. No dia do vencimento, à meia-noite UTC−3, a
  licença deixa de valer. A chave vitalícia não tem vencimento.
- RN25: licença ausente, inválida, futura, de outro computador ou vencida impede
  acesso ao aplicativo até a ativação. Ao vencer em execução, o diálogo modal
  bloqueia a interface na próxima verificação (até 15 segundos; operações já
  iniciadas podem concluir). Fechar o diálogo obrigatório encerra o aplicativo.
- RN26: ativação e expiração não apagam banco, fotos, histórico ou backups.
- RN27: último uso protegido pelo Windows por usuário; atraso do relógio superior
  a 5 minutos exige correção. Renovação também respeita esse registro.

## Operação do responsável

O cliente copia o código na tela de ativação e envia a você. Abra
`artifacts/emissor/VarthexComanda.Licencas.exe`, informe o caminho da chave privada,
o código, o nome do cliente, meses (0 para vitalícia) e a data inicial.
Confira o vencimento e confirme a emissão após conferir o pagamento externamente.
O emissor salva um TXT com a chave ao lado do executável. Envie somente esse TXT
ou seu conteúdo ao cliente, que cola o texto no aplicativo.

Para renovar antes do vencimento, escolha a data inicial e a duração de modo que
o vencimento cubra o período contratado. Uma chave com início futuro só pode ser
ativada a partir dessa data; a licença atual continua em uso até lá. A tela
**Licença** permite consultar o vencimento e substituir a chave atual.

Meses usam calendário (31 de janeiro + 1 mês = último dia de fevereiro).
Reinstalação e reutilização da chave não reiniciam o prazo. Reinstalar o Windows
pode mudar o código do computador e exigir reemissão.

## Chaves e publicação

A chave privada foi criada em `artifacts/segredos/licenca-privada.pem` e é ignorada
pelo Git. Faça backup seguro dela: é necessária para emitir futuras licenças.
Não envie a pasta artifacts inteira: ela contém o emissor e a chave privada.
Distribua apenas `VarthexComanda-1.1.0-Setup-win-x64.exe` aos clientes.
A chave pública está embutida no aplicativo; a privada nunca entra no pacote.

O emissor é publicado por `scripts/publicar-emissor.ps1`. O instalador usa
`scripts/gerar-instalador.ps1`. A geração inicial do par de chaves é uma operação
única: `dotnet run --project backend/tools/VarthexComanda.Licencas -- inicializar
<privada.pem> <publica.pem>`. Não substitua o par existente após distribuir o app.

Estado local: `%LOCALAPPDATA%/VarthexComanda/licenca/estado.dat`, fora do banco de
negócio e dos backups/restaurações. Protegido por DPAPI do usuário Windows.

## Limites e aceitação

Não há revogação remota, verificação automática de pagamento nem fonte de hora
confiável offline. Usuários com controle do computador podem apagar/restaurar o
estado local, clonar o identificador ou modificar o executável. A proteção do
relógio dificulta abuso casual, sem garantir resistência a essas ações.
Versões antigas sem licenciamento continuam funcionando; não são revogáveis offline.

CT23: verificar ativação válida, assinatura alterada, emissor diferente, computador
incorreto, licença futura, vencimento exato, reutilização, renovação, vitalícia e
relógio atrasado. Testes em `LicencaOfflineTests`. Complementar manualmente com
ativação no Windows, fechamento/reabertura, bloqueio com app aberto e confirmação
de que banco e backups permanecem disponíveis em disco.
