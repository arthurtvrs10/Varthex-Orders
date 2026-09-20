# Segurança, privacidade e backup

## Privacidade

O MVP não cadastra cliente. Não solicitar nome, CPF, telefone, endereço ou dados do cartão. Observações aceitam somente informação operacional.

## Segurança local

- usar conta do sistema operacional protegida por senha;
- armazenar dados fora da pasta de executáveis;
- restringir a pasta ao usuário autorizado;
- ativar chaves estrangeiras do SQLite;
- usar consultas parametrizadas;
- habilitar modo de diário adequado;
- não registrar senhas, tokens, cartão ou imagens de documentos;
- aplicar assinatura do instalador quando houver distribuição pública.

## Local dos arquivos

```text
%LOCALAPPDATA%\VarthexComanda\
  data\
    varthex-comanda.db
  logs/
  backups/
```

Em produção, derive a pasta com `Environment.SpecialFolder.LocalApplicationData`. Não grave o banco ao lado do executável e não exija permissão de administrador para escrever dados operacionais.

## Instância única e recuperação

- usar um mutex nomeado para impedir duas instâncias na mesma sessão do Windows;
- manter a restrição única do banco como segunda linha de defesa;
- confirmar cada gravação antes de apresentar sucesso na interface;
- ao iniciar, validar migrações e carregar comandas `ABERTA` sem alterar seu estado;
- se a integridade falhar, bloquear novas gravações e orientar a restauração: o aplicativo abre em modo de restauração (só a aba Backup, com faixa de aviso; a lista vem dos arquivos da pasta de backups) e, ao restaurar, guarda uma cópia bruta do banco corrompido em `backups\corrompido-*.db.bak`, fora da retenção;
- não tentar restaurar automaticamente um backup sem confirmação humana.

## Logs

- gravar arquivos locais estruturados por data;
- aplicar rotação diária e limite total configurado;
- conservar 30 arquivos por padrão (equivale a cerca de 30 dias em uso normal; a contagem de arquivos é o que garante o teto de disco), com arquivos de no máximo 5 MB e teto total de cerca de 150 MB (`LogOptions`);
- mascarar caminhos ou conteúdos que revelem dados desnecessários: o perfil do usuário do Windows vira `%USERPROFILE%`, a pasta de backup externa e a pasta de dados fora do perfil também são mascaradas; falhas técnicas registram só o nome da operação e a exceção, nunca produto, valor ou número de comanda;
- nunca registrar itens como dados de cartão, senha, token ou autorização.

## Política de backup

| Elemento | Padrão inicial |
| --- | --- |
| Frequência | Primeira abertura do dia e encerramento do aplicativo quando houve mudanças |
| Retenção local | 30 cópias diárias |
| Nome | `lanchonete-AAAA-MM-DD-HHMMSS.db` |
| Integridade | `PRAGMA integrity_check` e checksum |
| Cópia externa | Pendrive ou pasta sincronizada ao fim do dia |
| Teste de restauração | Trimestral em pasta separada |

## Processo de backup

1. concluir a transação em andamento;
2. criar snapshot consistente pela API de backup do SQLite;
3. salvar em arquivo temporário no destino;
4. executar verificação de integridade;
5. calcular checksum;
6. renomear para o nome definitivo;
7. registrar sucesso;
8. aplicar retenção somente após nova cópia válida.

## Processo de restauração

1. impedir novos lançamentos;
2. validar extensão, versão e integridade da cópia;
3. mostrar data e consequência;
4. obter confirmação;
5. copiar a base ativa para recuperação preventiva;
6. restaurar primeiro em arquivo temporário;
7. substituir a base apenas após validação;
8. reiniciar e verificar consultas essenciais.

Falha em qualquer etapa preserva a base ativa.
