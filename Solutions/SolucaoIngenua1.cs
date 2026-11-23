using System.Diagnostics;
using RestauranteConcorrente.Logging;
using RestauranteConcorrente.Models;
using RestauranteConcorrente.Participants;
using RestauranteConcorrente.Resources;

namespace RestauranteConcorrente.Solutions;

/// <summary>
/// Orquestra o cenário do Restaurante Concorrente usando a Solução Ingênua 1:
/// múltiplas Tasks rodando em paralelo SEM nenhum controle de concorrência.
/// </summary>
public sealed class SolucaoIngenua1
{
    private const int QuantidadeGarcons = 3;
    private const int PedidosPorGarcom = 6;
    private const int QuantidadeCozinheiros = 4;
    private const int CapacidadeBalcao = 4;

    private readonly EventLogger _logger = new();

    public async Task ExecutarAsync()
    {
        var cronometro = Stopwatch.StartNew();

        _logger.Linha("================================================================================", ConsoleColor.Cyan);
        _logger.Linha("  RESTAURANTE CONCORRENTE — SOLUÇÃO INGÊNUA 1 (SEM CONTROLE DE CONCORRÊNCIA)", ConsoleColor.Cyan);
        _logger.Linha($"  Participantes: {QuantidadeGarcons} Garçons ({PedidosPorGarcom} pedidos cada) + {QuantidadeCozinheiros} Cozinheiros | Balcão: {CapacidadeBalcao}", ConsoleColor.Gray);
        _logger.Linha("================================================================================", ConsoleColor.Cyan);

        var balcao = new BalcaoPedidos(CapacidadeBalcao, _logger);
        var estoque = new Estoque(_logger);
        var caixa = new Caixa(_logger);

        bool garconsEncerraram = false;

        // Cria e dispara uma Task independente para cada participante (Garçons e Cozinheiros)
        var tarefasGarcons = Enumerable.Range(1, QuantidadeGarcons)
            .Select(id =>
            {
                var garcom = new Garcom(id, PedidosPorGarcom, balcao, _logger);
                return Task.Run(() => garcom.ExecutarAsync());
            })
            .ToArray();

        var tarefasCozinheiros = Enumerable.Range(1, QuantidadeCozinheiros)
            .Select(id =>
            {
                var cozinheiro = new Cozinheiro(id, balcao, estoque, caixa, _logger);
                return Task.Run(() => cozinheiro.ExecutarAsync(() => garconsEncerraram));
            })
            .ToArray();

        // Aguarda todos os garçons enviarem seus pedidos e sinaliza encerramento do salão
        var resultadosGarcons = await Task.WhenAll(tarefasGarcons);
        garconsEncerraram = true;
        _logger.Registrar("Sistema", "Todos os garçons finalizaram. Aguardando a cozinha esvaziar o balcão...");

        var resultadosCozinheiros = await Task.WhenAll(tarefasCozinheiros);
        cronometro.Stop();

        ExibirRelatorioFinal(resultadosGarcons, resultadosCozinheiros, estoque, caixa, cronometro.Elapsed);
    }

    private void ExibirRelatorioFinal(
        ResultadoGarcom[] resultadosGarcons,
        ResultadoCozinheiro[] resultadosCozinheiros,
        Estoque estoque,
        Caixa caixa,
        TimeSpan duracao)
    {
        var todosPedidosCriados = resultadosGarcons.SelectMany(g => g.PedidosCriados).ToList();
        var todosPreparos = resultadosCozinheiros.SelectMany(c => c.PedidosPreparados).ToList();

        var codigosCriados = todosPedidosCriados.Select(p => p.Codigo).ToHashSet();
        var codigosPreparadosUnicos = todosPreparos.Select(p => p.Codigo).ToHashSet();
        var codigosRecusados = todosPedidosCriados
            .Where(p => p.Status == StatusPedido.RecusadoSemEstoque)
            .Select(p => p.Codigo)
            .ToHashSet();

        var pedidosPerdidos = codigosCriados
            .Except(codigosPreparadosUnicos)
            .Except(codigosRecusados)
            .OrderBy(c => c)
            .ToList();

        var preparosDuplicados = todosPreparos
            .GroupBy(p => p.Codigo)
            .Where(g => g.Count() > 1)
            .Select(g => $"#{g.Key} ({g.Count()}x)")
            .OrderBy(s => s)
            .ToList();

        int totalDuplicados = resultadosCozinheiros.Sum(c => c.PedidosDuplicadosObservados);
        int totalEstourosBalcao = resultadosGarcons.Sum(g => g.EstourosBalcaoObservados);
        int totalErrosLista = resultadosGarcons.Sum(g => g.ErrosConcorrenciaLista)
                              + resultadosCozinheiros.Sum(c => c.ErrosConcorrenciaLista);
        int totalEstoquesNegativos = resultadosCozinheiros.Sum(c => c.EstoquesNegativosObservados);
        int totalLostUpdates = resultadosCozinheiros.Sum(c => c.LostUpdatesCaixaObservados);
        int totalRecusados = resultadosCozinheiros.Sum(c => c.PedidosRecusadosSemEstoque);

        decimal faturamentoEsperado = resultadosCozinheiros.Sum(c => c.FaturamentoEsperadoLocal);
        decimal diferencaFaturamento = faturamentoEsperado - caixa.FaturamentoTotal;

        var saldosFinais = estoque.ObterSaldosFinais();
        var ingredientesNegativos = saldosFinais.Where(kv => kv.Value < 0).ToList();

        _logger.Linha("");
        _logger.Linha("================================================================================", ConsoleColor.Magenta);
        _logger.Linha("               RELATÓRIO FINAL — SOLUÇÃO INGÊNUA 1 (SEM CONTROLE)", ConsoleColor.Magenta);
        _logger.Linha("================================================================================", ConsoleColor.Magenta);
        _logger.Linha($"Tempo total de execução ............: {duracao.TotalSeconds:F2} s");
        _logger.Linha($"Pedidos criados pelos garçons ......: {todosPedidosCriados.Count}");
        _logger.Linha($"Pratos finalizados (total execuções): {todosPreparos.Count} ({codigosPreparadosUnicos.Count} pedidos únicos)");
        _logger.Linha($"Pedidos recusados (sem estoque) ....: {totalRecusados}");
        _logger.Linha($"Vendas esperadas x Caixa ...........: {todosPreparos.Count} esperadas | {caixa.VendasRegistradas} registradas");
        _logger.Linha($"Faturamento esperado ...............: R$ {faturamentoEsperado:F2}");
        _logger.Linha($"Faturamento registrado no Caixa ....: R$ {caixa.FaturamentoTotal:F2}");
        _logger.Linha($"Estoque final ......................: {string.Join(" | ", saldosFinais.Select(kv => $"{kv.Key}: {kv.Value}"))}");
        _logger.Linha("");
        _logger.Linha("PROBLEMAS DE CONCORRÊNCIA DETECTADOS:", ConsoleColor.Red);

        string detalheDuplicados = preparosDuplicados.Count > 0
            ? $" [{string.Join(", ", preparosDuplicados)}]"
            : string.Empty;

        ImprimirVerificacao(
            totalDuplicados == 0 && preparosDuplicados.Count == 0,
            "Nenhum pedido preparado em duplicidade.",
            $"Pedidos pegos/preparados em duplicidade: {Math.Max(totalDuplicados, preparosDuplicados.Count)} ocorrência(s){detalheDuplicados}.");

        ImprimirVerificacao(
            pedidosPerdidos.Count == 0,
            "Nenhum pedido perdido no balcão.",
            $"Pedidos perdidos (sumiram da lista sem preparo): {pedidosPerdidos.Count} [{string.Join(", ", pedidosPerdidos)}].");

        ImprimirVerificacao(
            totalEstourosBalcao == 0,
            "Capacidade do balcão sempre respeitada.",
            $"Estouros de capacidade do balcão (> {CapacidadeBalcao}): {totalEstourosBalcao} ocorrência(s).");

        ImprimirVerificacao(
            totalErrosLista == 0,
            "Nenhum erro de acesso concorrente à List<Pedido>.",
            $"Exceções/falhas de acesso concorrente no balcão: {totalErrosLista} ocorrência(s).");

        ImprimirVerificacao(
            ingredientesNegativos.Count == 0 && totalEstoquesNegativos == 0,
            "Nenhum ingrediente ficou com estoque negativo.",
            $"Estoque negativo detectado ({totalEstoquesNegativos} ocorrência(s)): {string.Join(", ", ingredientesNegativos.Select(kv => $"{kv.Key}={kv.Value}"))}.");

        ImprimirVerificacao(
            diferencaFaturamento == 0 && totalLostUpdates == 0,
            "Faturamento do Caixa consistente (sem Lost Update).",
            $"Faturamento inconsistente! Diferença perdida: R$ {diferencaFaturamento:F2} ({totalLostUpdates} lost update(s) detectado(s)).");

        _logger.Linha("================================================================================", ConsoleColor.Magenta);
    }

    private void ImprimirVerificacao(bool ok, string mensagemOk, string mensagemErro)
    {
        if (ok)
        {
            _logger.Linha($"  [OK]       {mensagemOk}", ConsoleColor.Green);
        }
        else
        {
            _logger.Linha($"  [PROBLEMA] {mensagemErro}", ConsoleColor.Red);
        }
    }
}
