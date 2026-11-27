using RestauranteConcorrente.Logging;

namespace RestauranteConcorrente.Models;

/// <summary>
/// Relatório final unificado de todas as soluções do cenário,
/// consolidando métricas de execução e contadores de problemas de concorrência.
/// </summary>
public sealed class RelatorioSimulacao
{
    public required string TituloSolucao { get; init; }
    public required string ProblemaAlvo { get; init; }
    public required TimeSpan DuracaoRelogio { get; init; }
    public required TimeSpan TempoCpuConsumido { get; init; }

    public int CapacidadeBalcao { get; init; }
    public int PedidosCriados { get; init; }
    public int PratosFinalizadosTotal { get; init; }
    public int PedidosUnicosFinalizados { get; init; }
    public int PedidosRecusadosSemEstoque { get; init; }
    public int PedidosNaoAtendidosOuRestantes { get; init; }

    public int VendasEsperadas { get; init; }
    public int VendasRegistradasCaixa { get; init; }
    public decimal FaturamentoEsperado { get; init; }
    public decimal FaturamentoRegistradoCaixa { get; init; }
    public required IReadOnlyDictionary<string, int> EstoqueFinal { get; init; }

    // Contadores de problemas de concorrência
    public int PedidosDuplicados { get; init; }
    public IReadOnlyList<string> DetalhePedidosDuplicados { get; init; } = [];
    public IReadOnlyList<string> PedidosPerdidos { get; init; } = [];
    public int EstourosCapacidadeBalcao { get; init; }
    public int ExcecoesAcessoConcorrente { get; init; }
    public int OcorrenciasEstoqueNegativo { get; init; }
    public int LostUpdatesCaixa { get; init; }
    public int DeadlocksDetectados { get; init; }
    public int BloqueiosCascataDeadlock { get; init; }
    public int PedidosTravadosDeadlock { get; init; }
    public long IteracoesEsperaOcupada { get; init; }

    public void Exibir(EventLogger logger)
    {
        decimal diferencaFaturamento = FaturamentoEsperado - FaturamentoRegistradoCaixa;
        var ingredientesNegativos = EstoqueFinal.Where(kv => kv.Value < 0).ToList();
        double percentualCpu = DuracaoRelogio.TotalMilliseconds > 0
            ? (TempoCpuConsumido.TotalMilliseconds / DuracaoRelogio.TotalMilliseconds) * 100.0
            : 0.0;

        logger.Linha("");
        logger.Linha("================================================================================", ConsoleColor.Magenta);
        logger.Linha($"  RELATÓRIO FINAL — {TituloSolucao}", ConsoleColor.Magenta);
        logger.Linha($"  Problema demonstrado: {ProblemaAlvo}", ConsoleColor.Gray);
        logger.Linha("================================================================================", ConsoleColor.Magenta);
        logger.Linha($"Tempo de execução (relógio) ........: {DuracaoRelogio.TotalSeconds:F2} s");
        logger.Linha($"Tempo de CPU consumido .............: {TempoCpuConsumido.TotalSeconds:F2} s ({percentualCpu:F0}% de uso médio de CPU)");
        logger.Linha($"Pedidos criados pelos garçons ......: {PedidosCriados}");
        logger.Linha($"Pratos finalizados .................: {PratosFinalizadosTotal} ({PedidosUnicosFinalizados} pedidos únicos)");
        logger.Linha($"Pedidos recusados (sem estoque) ....: {PedidosRecusadosSemEstoque}");
        if (PedidosNaoAtendidosOuRestantes > 0)
        {
            logger.Linha($"Pedidos não atendidos / no balcão ..: {PedidosNaoAtendidosOuRestantes}");
        }
        logger.Linha($"Vendas esperadas x Caixa ...........: {VendasEsperadas} esperadas | {VendasRegistradasCaixa} registradas");
        logger.Linha($"Faturamento esperado ...............: R$ {FaturamentoEsperado:F2}");
        logger.Linha($"Faturamento registrado no Caixa ....: R$ {FaturamentoRegistradoCaixa:F2}");
        logger.Linha($"Estoque final ......................: {string.Join(" | ", EstoqueFinal.Select(kv => $"{kv.Key}: {kv.Value}"))}");
        logger.Linha("");
        logger.Linha("CONTADORES DE PROBLEMAS DE CONCORRÊNCIA:", ConsoleColor.White);

        string detalheDup = DetalhePedidosDuplicados.Count > 0
            ? $" [{string.Join(", ", DetalhePedidosDuplicados)}]"
            : string.Empty;

        ImprimirVerificacao(
            logger,
            PedidosDuplicados == 0 && DetalhePedidosDuplicados.Count == 0,
            "Race Condition (duplicidade) ...: 0 ocorrências.",
            $"Race Condition (duplicidade) ...: {Math.Max(PedidosDuplicados, DetalhePedidosDuplicados.Count)} ocorrência(s){detalheDup}.");

        ImprimirVerificacao(
            logger,
            PedidosPerdidos.Count == 0,
            "Pedidos perdidos no balcão .....: 0 pedidos perdidos.",
            $"Pedidos perdidos no balcão .....: {PedidosPerdidos.Count} [{string.Join(", ", PedidosPerdidos)}].");

        ImprimirVerificacao(
            logger,
            EstourosCapacidadeBalcao == 0,
            $"Capacidade do balcão (<= {CapacidadeBalcao}) .....: 0 estouros.",
            $"Capacidade do balcão (> {CapacidadeBalcao}) ......: {EstourosCapacidadeBalcao} estouro(s).");

        ImprimirVerificacao(
            logger,
            ExcecoesAcessoConcorrente == 0,
            "Exceções em coleção compartilhada: 0 exceções.",
            $"Exceções em coleção compartilhada: {ExcecoesAcessoConcorrente} ocorrência(s).");

        ImprimirVerificacao(
            logger,
            ingredientesNegativos.Count == 0 && OcorrenciasEstoqueNegativo == 0,
            "Estoque negativo (check-then-act): 0 ocorrências.",
            $"Estoque negativo (check-then-act): {OcorrenciasEstoqueNegativo} ocorrência(s) [{string.Join(", ", ingredientesNegativos.Select(kv => $"{kv.Key}={kv.Value}"))}].");

        ImprimirVerificacao(
            logger,
            diferencaFaturamento == 0 && LostUpdatesCaixa == 0,
            "Lost Update no Caixa ...........: 0 ocorrências (faturamento exato).",
            $"Lost Update no Caixa ...........: {LostUpdatesCaixa} ocorrência(s) | Diferença perdida: R$ {diferencaFaturamento:F2}.");

        ImprimirVerificacao(
            logger,
            DeadlocksDetectados == 0 && PedidosTravadosDeadlock == 0,
            "Deadlock (espera circular) .....: 0 ocorrências.",
            $"Deadlock (espera circular) .....: {DeadlocksDetectados} thread(s) em deadlock | {BloqueiosCascataDeadlock} bloqueio(s) em cascata | {PedidosTravadosDeadlock} pedido(s) travado(s).");

        ImprimirVerificacao(
            logger,
            IteracoesEsperaOcupada == 0,
            "Espera ocupada (busy-waiting) ..: 0 iterações desperdiçadas.",
            $"Espera ocupada (busy-waiting) ..: {IteracoesEsperaOcupada:N0} verificações inúteis em loop ({TempoCpuConsumido.TotalSeconds:F2} s de CPU queimada!).");

        logger.Linha("================================================================================", ConsoleColor.Magenta);
    }

    private static void ImprimirVerificacao(EventLogger logger, bool ok, string mensagemOk, string mensagemErro)
    {
        if (ok)
        {
            logger.Linha($"  [OK]       {mensagemOk}", ConsoleColor.Green);
        }
        else
        {
            logger.Linha($"  [PROBLEMA] {mensagemErro}", ConsoleColor.Red);
        }
    }
}
