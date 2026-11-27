using System.Diagnostics;
using RestauranteConcorrente.Logging;
using RestauranteConcorrente.Models;
using RestauranteConcorrente.Participants;
using RestauranteConcorrente.Resources;

namespace RestauranteConcorrente.Solutions;

/// <summary>
/// SOLUÇÃO INGÊNUA 1 — SEM CONTROLE DE CONCORRÊNCIA.
/// Orquestra o cenário do Restaurante Concorrente com múltiplas Tasks rodando
/// em paralelo SEM nenhum controle de concorrência (Race Condition / Lost Update / Estoque Negativo).
/// </summary>
public sealed class SolucaoIngenua1 : ISolucaoSimulacao
{
    private const int QuantidadeGarcons = 3;
    private const int PedidosPorGarcom = 6;
    private const int QuantidadeCozinheiros = 4;
    private const int CapacidadeBalcao = 4;

    public int Numero => 1;
    public string Titulo => "Solução Ingênua 1 — Sem Controle de Concorrência";
    public string ProblemaDemonstrado => "Race Condition (Duplicidade, Perda, Estoque Negativo e Lost Update)";

    private readonly EventLogger _logger = new();

    public async Task ExecutarAsync()
    {
        var processo = Process.GetCurrentProcess();
        var cpuInicio = processo.TotalProcessorTime;
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
        var cpuConsumido = processo.TotalProcessorTime - cpuInicio;

        GerarRelatorio(resultadosGarcons, resultadosCozinheiros, estoque, caixa, cronometro.Elapsed, cpuConsumido)
            .Exibir(_logger);
    }

    private RelatorioSimulacao GerarRelatorio(
        ResultadoGarcom[] resultadosGarcons,
        ResultadoCozinheiro[] resultadosCozinheiros,
        Estoque estoque,
        Caixa caixa,
        TimeSpan duracao,
        TimeSpan cpuConsumido)
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

        return new RelatorioSimulacao
        {
            TituloSolucao = "SOLUÇÃO INGÊNUA 1 (SEM CONTROLE DE CONCORRÊNCIA)",
            ProblemaAlvo = ProblemaDemonstrado,
            DuracaoRelogio = duracao,
            TempoCpuConsumido = cpuConsumido,
            CapacidadeBalcao = CapacidadeBalcao,
            PedidosCriados = todosPedidosCriados.Count,
            PratosFinalizadosTotal = todosPreparos.Count,
            PedidosUnicosFinalizados = codigosPreparadosUnicos.Count,
            PedidosRecusadosSemEstoque = resultadosCozinheiros.Sum(c => c.PedidosRecusadosSemEstoque),
            VendasEsperadas = todosPreparos.Count,
            VendasRegistradasCaixa = caixa.VendasRegistradas,
            FaturamentoEsperado = resultadosCozinheiros.Sum(c => c.FaturamentoEsperadoLocal),
            FaturamentoRegistradoCaixa = caixa.FaturamentoTotal,
            EstoqueFinal = estoque.ObterSaldosFinais(),
            PedidosDuplicados = resultadosCozinheiros.Sum(c => c.PedidosDuplicadosObservados),
            DetalhePedidosDuplicados = preparosDuplicados,
            PedidosPerdidos = pedidosPerdidos,
            EstourosCapacidadeBalcao = resultadosGarcons.Sum(g => g.EstourosBalcaoObservados),
            ExcecoesAcessoConcorrente = resultadosGarcons.Sum(g => g.ErrosConcorrenciaLista)
                                      + resultadosCozinheiros.Sum(c => c.ErrosConcorrenciaLista),
            OcorrenciasEstoqueNegativo = resultadosCozinheiros.Sum(c => c.EstoquesNegativosObservados),
            LostUpdatesCaixa = resultadosCozinheiros.Sum(c => c.LostUpdatesCaixaObservados)
        };
    }
}
