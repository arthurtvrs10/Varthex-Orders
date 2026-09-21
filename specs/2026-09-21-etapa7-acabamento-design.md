# Etapa 7 · Fatia 3 — Acabamento (design)

Fecha pontas soltas antes da publicação: janelas nativas restantes, "Ativo"
como Sim/Não, fotos no backup e a documentação que ficou inconsistente.

## Estado atual (verificado)

- Seis `MessageBox.Show` nativos restam: `App.xaml.cs` (segunda instância,
  erro não tratado na interface, falha ao preparar o banco, falha ao reiniciar
  após restauração, e o caso "banco corrompido" já virou modo de restauração)
  e `BackupView.xaml.cs` ("Backup restaurado com sucesso…"). São pequenas,
  pensadas para mouse, contrárias à decisão de UI por toque.
- A lista de Produtos mostra a coluna "Ativo" como `True/False`.
- O backup gerenciado copia só o `.db`; as fotos (`fotos\`) ficam de fora
  (limitação declarada no changelog 1.12). Restaurar um `.db` antigo deixa fotos
  novas órfãs e, ao contrário, um PC novo perde todas as fotos.
- `docs/docs/15-contratos-aplicacao.md` descreve `VendaRepository.salvar` etc.;
  o código separa leitura/escrita (`IVendaRepository` só leitura,
  `EncerrarComanda` transacional no `IComandaRepository`).

## Decisões (rulings)

1. **`JanelaAviso`** (janela WPF de um botão, mesmo visual e tamanho de toque da
   `ConfirmacaoView`, `Enter`/`Esc` fecham, ícone/cor por tipo: Informação,
   Aviso, Erro) com API estática `JanelaAviso.Mostrar(titulo, mensagem, tipo)`
   — estática porque a mensagem de segunda instância aparece antes de haver
   `ServiceProvider`. Segue a regra do `JanelaConfirmador` (dono só se a
   janela principal existir; senão centro da tela; `System.Windows.Application`
   qualificado).
2. **Sim/Não:** conversor `BooleanoParaSimNaoConverter` na coluna da lista de
   Produtos (a caixa "Ativo" do formulário continua).
3. **Fotos no backup:** ao criar um backup gerenciado, se `fotos\` tiver
   arquivos, cria ao lado `varthex-comanda-….db.fotos.zip` (System.IO.Compression,
   só os arquivos de imagem, nível ótimo) — o `.db` e seu `.sha256` continuam
   como hoje. A retenção apaga o zip junto do `.db`. Falha ao zipar **não**
   invalida o backup do banco (log Warning; resultado continua Sucesso).
   **Restaurar** um `.db` que tenha `.fotos.zip` ao lado extrai as fotos para
   `fotos\` *sem apagar* nenhum arquivo existente (mesmo nome = sobrescreve,
   proteção contra zip-slip com `Path.GetFileName`). O relatório de validação
   não muda. O backup externo copia o zip junto quando existir.
4. **Fotos órfãs:** **não** apagar nada automaticamente (uma restauração de banco
   antigo poderia apagar fotos válidas). Só registrar no log, na inicialização,
   quantas fotos não são referenciadas por nenhum produto.
5. **Docs:** nota de reconciliação em `docs/docs/15` explicando que os nomes são
   sugestões e o mapeamento real; `docs/docs/08` diz "30 cópias" (já está) e
   passa a mencionar `.fotos.zip`.
6. Fora de escopo: "polimento do Histórico" (sem requisito concreto — fica como
   pendência), migração das mensagens de erro de fluxo para o padrão único.
