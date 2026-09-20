# Etapa 7 · Fatia 1 — Robustez operacional (design)

Cobre RF26, RF27, RNF16, RNF20, RNF21 e a regra de "banco corrompido" de
`docs/docs/08-seguranca-backup.md`. É a primeira de quatro fatias da
Etapas 7 + 8 (1 Robustez · 2 Teclado · 3 Publicação · 4 Homologação).

## Estado atual (verificado)

- `LoggingConfigurator` usa `WriteTo.File` só com `RollingInterval.Day`:
  sem limite de tamanho, sem `retainedFileCountLimit` configurado (o padrão da
  biblioteca é 31 arquivos), sem máscara de dados.
- Só o handler de exceção da UI e a falha de startup escrevem no log. Todos os
  `catch (Exception)` dos ViewModels mostram mensagem amigável e **engolem** a
  exceção; `CriarBackupAutomatico` tem um `catch` vazio.
- Logs contêm caminhos completos com o nome do usuário do Windows
  (`Banco pronto em C:\Users\<nome>\...`), o que `docs/08` manda mascarar.
- Banco corrompido: `App` mostra `MessageBox` e chama `Shutdown()`; o operador
  não consegue chegar à tela de Backup para restaurar (o doc manda bloquear
  escritas e orientar a restauração, nunca restaurar sozinho).
- RF27/RNF20: comandas abertas já reaparecem (cada mutação é uma transação
  própria + `ListarAbertas` no startup), mas **não existe teste** que prove.

## Decisões (rulings)

1. **Retenção e tamanho.** Constantes em `LogOptions` (defaults): arquivo
   diário, `rollOnFileSizeLimit`, **5 MB por arquivo**, **30 arquivos**
   retidos ⇒ teto de ~150 MB. Divergência documentada: `docs/08` diz "30
   dias"; retenção por contagem de arquivos equivale a 30 dias em uso normal e
   é o que garante o teto de disco. Não há tela para isso (YAGNI).
2. **Máscara.** Um sink decorador reescreve mensagem renderizada e exceção
   (tipo + mensagem + stack) trocando o perfil do usuário
   (`Environment.GetFolderPath(UserProfile)`, com `/` ou `\`, sem diferenciar
   maiúsculas) por `%USERPROFILE%` e a pasta de backup externa configurada não
   é registrada em texto puro (só "pasta externa"). Nada de dados de negócio
   (nome de produto, valores, número de comanda) nos logs de falha.
3. **Logger nos ViewModels/serviços:** parâmetro final opcional
   `ILogger? logger = null` (o DI injeta o registrado; os testes existentes
   não mudam).
4. **Banco corrompido:** o app abre em *modo de restauração*: `MainWindow`
   com só a aba Backup habilitada e faixa de aviso; as demais abas
   desabilitadas (escritas bloqueadas). `RestaurarPara` deve funcionar com o
   banco ativo corrompido: se a cópia preventiva por `BackupDatabase` falhar,
   copia o arquivo bruto para `backups\corrompido-AAAA-MM-DD-HHMMSS.db.bak`
   (sem entrar na retenção) e segue; a restauração **nunca** é automática.
5. **Fora de escopo:** alerta de pouco espaço em disco (R15) — registrado como
   pendência; `RecuperarAtendimento` como classe (o contrato de doc 15 é
   sugestão; `ListarAbertas` cumpre o comportamento).

## Testes-alvo (rastreabilidade)

CT22/RNF21 (rotação, retenção, tamanho), RF26 (sem dados sensíveis), RF27 +
RN22 + RNF20/CT20 (reabrir repositório novo sobre o mesmo arquivo: mesmas
comandas/itens/totais, nenhuma venda), corrompido → modo restauração.
