using Xunit;

// SqliteConnection.ClearAllPools() e global ao processo: rodar classes de teste em paralelo
// faz uma limpar o pool da outra no meio do teste (ObjectDisposedException intermitente).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
