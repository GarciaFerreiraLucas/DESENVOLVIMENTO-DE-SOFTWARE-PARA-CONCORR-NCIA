using RestauranteConcorrente.Logging;
using RestauranteConcorrente.Models;

namespace RestauranteConcorrente.Resources;

public enum ResultadoUsoUtensilios
{
    Sucesso,
    DeadlockCircular,
    BloqueioEmCascata
}

/// <summary>
/// Recurso compartilhado: Utensílios únicos da cozinha (Tábua e Faca).
/// - Na Solução Ingênua 2: aquisição em ORDEM INVERTIDA conforme o prato -> provoca DEADLOCK real.
/// - Na Solução Ingênua 3: aquisição em ORDEM GLOBAL FIXA (Tábua -> Faca) -> elimina o deadlock.
/// </summary>
public sealed class UtensiliosCozinha
{
    private readonly object _lockTabua = new();
    private readonly object _lockFaca = new();
    private readonly EventLogger _logger;

    private volatile string? _donoTabua;
    private volatile string? _donoFaca;

    public UtensiliosCozinha(EventLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// SOLUÇÃO INGÊNUA 2: adquire os dois locks em ordens opostas dependendo do prato.
    /// - Hambúrguer / Salada: Tábua -> Faca
    /// - Pizza / Lasanha:     Faca  -> Tábua
    /// Usa Monitor.TryEnter com timeout de segurança apenas para detectar o travamento
    /// e devolver o controle ao menu/relatório sem exigir Ctrl+C.
    /// </summary>
    public ResultadoUsoUtensilios UsarComOrdemInvertida(
        Pedido pedido,
        string autor,
        int timeoutDeadlockMs = 2500)
    {
        bool pegaTabuaPrimeiro = pedido.Prato.Nome is "Hambúrguer Artesanal" or "Salada Caesar";

        object primeiroLock = pegaTabuaPrimeiro ? _lockTabua : _lockFaca;
        object segundoLock = pegaTabuaPrimeiro ? _lockFaca : _lockTabua;
        string nomePrimeiro = pegaTabuaPrimeiro ? "Tábua" : "Faca";
        string nomeSegundo = pegaTabuaPrimeiro ? "Faca" : "Tábua";

        bool adquiriuPrimeiro = false;
        bool adquiriuSegundo = false;

        try
        {
            adquiriuPrimeiro = Monitor.TryEnter(primeiroLock, timeoutDeadlockMs);
            if (!adquiriuPrimeiro)
            {
                string donoAtual = pegaTabuaPrimeiro
                    ? (_donoTabua ?? "outro cozinheiro")
                    : (_donoFaca ?? "outro cozinheiro");

                _logger.Problema(
                    autor,
                    $"Bloqueado em cascata por {timeoutDeadlockMs} ms aguardando [{nomePrimeiro}] (preso com {donoAtual}) para #{pedido.Codigo}!");
                return ResultadoUsoUtensilios.BloqueioEmCascata;
            }

            if (pegaTabuaPrimeiro) _donoTabua = autor;
            else _donoFaca = autor;

            _logger.Registrar(
                autor,
                $"Adquiriu [{nomePrimeiro}] para {pedido}. Preparando corte...",
                TipoEvento.Cozinheiro);

            // Tempo aleatório com o primeiro utensílio em mãos (abre janela para a espera circular)
            Thread.Sleep(Random.Shared.Next(70, 150));

            _logger.Registrar(
                autor,
                $"Segurando [{nomePrimeiro}], tentando adquirir [{nomeSegundo}] para #{pedido.Codigo}...",
                TipoEvento.Cozinheiro);

            // Tentativa de adquirir o segundo utensílio enquanto segura o primeiro -> DEADLOCK CIRCULAR!
            adquiriuSegundo = Monitor.TryEnter(segundoLock, timeoutDeadlockMs);
            if (!adquiriuSegundo)
            {
                string donoOutro = pegaTabuaPrimeiro
                    ? (_donoFaca ?? "outro cozinheiro")
                    : (_donoTabua ?? "outro cozinheiro");

                pedido.Status = StatusPedido.TravadoEmDeadlock;
                _logger.Problema(
                    autor,
                    $"DEADLOCK DETECTADO! Ficou travado {timeoutDeadlockMs} ms segurando [{nomePrimeiro}] e esperando [{nomeSegundo}] (em posse de {donoOutro}) no pedido #{pedido.Codigo}!");

                // Mantém o primeiro lock por um breve instante para que a outra thread do ciclo
                // também atinja o timeout e registre a segunda ponta da espera circular no log.
                Thread.Sleep(150);
                return ResultadoUsoUtensilios.DeadlockCircular;
            }

            if (pegaTabuaPrimeiro) _donoFaca = autor;
            else _donoTabua = autor;

            // Tempo aleatório finalizando preparo com os dois utensílios
            int tempoCorte = Random.Shared.Next(40, 100);
            _logger.Registrar(
                autor,
                $"Usando [{nomePrimeiro} + {nomeSegundo}] em #{pedido.Codigo} ({tempoCorte} ms)...",
                TipoEvento.Cozinheiro);
            Thread.Sleep(tempoCorte);

            return ResultadoUsoUtensilios.Sucesso;
        }
        finally
        {
            if (adquiriuSegundo)
            {
                if (pegaTabuaPrimeiro) _donoFaca = null;
                else _donoTabua = null;
                Monitor.Exit(segundoLock);
            }

            if (adquiriuPrimeiro)
            {
                if (pegaTabuaPrimeiro) _donoTabua = null;
                else _donoFaca = null;
                Monitor.Exit(primeiroLock);
            }
        }
    }

    /// <summary>
    /// SOLUÇÃO INGÊNUA 3: ordem global fixa (sempre Tábua -> Faca), eliminando deadlock.
    /// </summary>
    public void UsarComOrdemFixa(Pedido pedido, string autor)
    {
        lock (_lockTabua)
        {
            _donoTabua = autor;
            try
            {
                lock (_lockFaca)
                {
                    _donoFaca = autor;
                    try
                    {
                        int tempoCorte = Random.Shared.Next(35, 85);
                        _logger.Registrar(
                            autor,
                            $"Usando [Tábua -> Faca] (ordem fixa) em #{pedido.Codigo} ({tempoCorte} ms)...",
                            TipoEvento.Cozinheiro);
                        Thread.Sleep(tempoCorte);
                    }
                    finally
                    {
                        _donoFaca = null;
                    }
                }
            }
            finally
            {
                _donoTabua = null;
            }
        }
    }
}
