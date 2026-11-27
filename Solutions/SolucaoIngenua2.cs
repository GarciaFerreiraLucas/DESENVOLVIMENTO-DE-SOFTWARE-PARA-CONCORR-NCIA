using System.Diagnostics;
using RestauranteConcorrente.Logging;
using RestauranteConcorrente.Models;
using RestauranteConcorrente.Resources;

namespace RestauranteConcorrente.Solutions;

/// <summary>
/// SOLUÇÃO INGÊNUA 2 — DEADLOCK (Bloqueio Mútuo Circular).
/// Corrige as race conditions da Ingênua 1 protegendo Balcão, Estoque e Caixa com lock,
/// mas adquire os locks dos utensílios da cozinha (Tábua e Faca) em ORDEM INVERTIDA
/// conforme o prato, provocando espera circular (deadlock) entre os cozinheiros.
/// </summary>
public sealed class SolucaoIngenua2 : ISolucaoSimulacao
{
    private const int QuantidadeGarcons = 3;
    private const int PedidosPorGarcom = 6;
    private const int QuantidadeCozinheiros = 4;
    private const int CapacidadeBalcao = 4;
    private const int TimeoutDeadlockMs = 2500;

    public int Numero => 2;
    public string Titulo => "Solução Ingênua 2 — Locks em Ordem Invertida nos Utensílios";
    public string ProblemaDemonstrado => "Deadlock (Espera Circular nos Utensílios Tábua/Faca)";

    private readonly EventLogger _logger = new();

    public async Task ExecutarAsync()
    {
        var processo = Process.GetCurrentProcess();
        var cpuInicio = processo.TotalProcessorTime;
        var cronometro = Stopwatch.StartNew();

        _logger.Linha("================================================================================", ConsoleColor.Cyan);
        _logger.Linha("  RESTAURANTE CONCORRENTE — SOLUÇÃO INGÊNUA 2 (DEADLOCK NOS UTENSÍLIOS)", ConsoleColor.Cyan);
        _logger.Linha($"  Participantes: {QuantidadeGarcons} Garçons + {QuantidadeCozinheiros} Cozinheiros | Balcão: {CapacidadeBalcao} (com lock)", ConsoleColor.Gray);
        _logger.Linha("  Ordem invertida: Hambúrguer/Salada [Tábua -> Faca] | Pizza/Lasanha [Faca -> Tábua]", ConsoleColor.DarkYellow);
        _logger.Linha("================================================================================", ConsoleColor.Cyan);

        var balcao = new BalcaoPedidos(CapacidadeBalcao, _logger);
        var estoque = new Estoque(_logger, saldoUniforme: 30);
        var caixa = new Caixa(_logger);
        var utensilios = new UtensiliosCozinha(_logger);

        bool garconsEncerraram = false;
        bool deadlockOcorreu = false;

        // Cada participante roda em sua própria Task dedicada
        var tarefasGarcons = Enumerable.Range(1, QuantidadeGarcons)
            .Select(id => Task.Factory.StartNew(
                () => ExecutarGarcom(id, balcao, () => Volatile.Read(ref deadlockOcorreu)),
                TaskCreationOptions.LongRunning))
            .ToArray();

        var tarefasCozinheiros = Enumerable.Range(1, QuantidadeCozinheiros)
            .Select(id => Task.Factory.StartNew(
                () => ExecutarCozinheiro(
                    id,
                    balcao,
                    estoque,
                    caixa,
                    utensilios,
                    () => Volatile.Read(ref garconsEncerraram),
                    () => Volatile.Read(ref deadlockOcorreu),
                    () => Volatile.Write(ref deadlockOcorreu, true)),
                TaskCreationOptions.LongRunning))
            .ToArray();

        var resultadosGarcons = await Task.WhenAll(tarefasGarcons);
        Volatile.Write(ref garconsEncerraram, true);

        var resultadosCozinheiros = await Task.WhenAll(tarefasCozinheiros);

        cronometro.Stop();
        var cpuConsumido = processo.TotalProcessorTime - cpuInicio;

        if (Volatile.Read(ref deadlockOcorreu))
        {
            _logger.Problema(
                "Sistema",
                $"Simulação encerrada após detectar deadlock circular (timeout de segurança de {TimeoutDeadlockMs} ms) para evitar travamento infinito.");
        }

        GerarRelatorio(resultadosGarcons, resultadosCozinheiros, balcao, estoque, caixa, cronometro.Elapsed, cpuConsumido)
            .Exibir(_logger);
    }

    private ResultadoGarcom ExecutarGarcom(int id, BalcaoPedidos balcao, Func<bool> deadlockOcorreu)
    {
        string nome = $"Garcom-{id}";
        var resultado = new ResultadoGarcom { Autor = nome };
        _logger.Registrar(nome, "Iniciou expediente no salão.", TipoEvento.Garcom);

        for (int i = 1; i <= PedidosPorGarcom && !deadlockOcorreu(); i++)
        {
            Thread.Sleep(Random.Shared.Next(25, 90));
            if (deadlockOcorreu()) break;

            var prato = Cardapio.EscolherAleatorio();
            var pedido = new Pedido($"G{id}-{i:D2}", prato, nome);
            _logger.Registrar(nome, $"Anotou pedido {pedido}.", TipoEvento.Garcom);

            bool colocado = false;
            while (!colocado && !deadlockOcorreu())
            {
                if (balcao.TentarAdicionarComLock(pedido, nome))
                {
                    colocado = true;
                    resultado.PedidosCriados.Add(pedido);
                }
                else
                {
                    Thread.Sleep(Random.Shared.Next(25, 65));
                }
            }
        }

        _logger.Registrar(
            nome,
            $"Encerrou expediente ({resultado.PedidosCriados.Count} pedidos enviados).",
            TipoEvento.Garcom);
        return resultado;
    }

    private ResultadoCozinheiro ExecutarCozinheiro(
        int id,
        BalcaoPedidos balcao,
        Estoque estoque,
        Caixa caixa,
        UtensiliosCozinha utensilios,
        Func<bool> garconsEncerraram,
        Func<bool> deadlockOcorreu,
        Action sinalizarDeadlock)
    {
        string nome = $"Cozinheiro-{id}";
        var resultado = new ResultadoCozinheiro { Autor = nome };
        _logger.Registrar(nome, "Entrou na cozinha aguardando pedidos.", TipoEvento.Cozinheiro);

        while (!deadlockOcorreu())
        {
            if (garconsEncerraram() && balcao.EstaVazioComLock())
            {
                break;
            }

            if (!balcao.TentarRetirarComLock(nome, out var pedido) || pedido is null)
            {
                Thread.Sleep(Random.Shared.Next(15, 45));
                continue;
            }

            if (!estoque.TentarConsumirComLock(pedido, nome))
            {
                resultado.PedidosRecusadosSemEstoque++;
                continue;
            }

            // Disputa Tábua e Faca em ordem invertida conforme o prato -> DEADLOCK!
            var statusUtensilios = utensilios.UsarComOrdemInvertida(pedido, nome, TimeoutDeadlockMs);

            if (statusUtensilios == ResultadoUsoUtensilios.DeadlockCircular)
            {
                resultado.DeadlocksDetectados++;
                resultado.PedidosTravadosDeadlock++;
                sinalizarDeadlock();
                break;
            }

            if (statusUtensilios == ResultadoUsoUtensilios.BloqueioEmCascata)
            {
                resultado.BloqueiosCascataDeadlock++;
                resultado.PedidosTravadosDeadlock++;
                sinalizarDeadlock();
                break;
            }

            int tempoFinalizacao = Random.Shared.Next(
                pedido.Prato.TempoPreparoMinMs / 2,
                pedido.Prato.TempoPreparoMaxMs / 2 + 1);
            Thread.Sleep(tempoFinalizacao);

            pedido.Status = StatusPedido.Pronto;
            resultado.PedidosPreparados.Add(pedido);
            resultado.FaturamentoEsperadoLocal += pedido.Prato.Preco;

            caixa.RegistrarVendaComLock(pedido, nome);
        }

        _logger.Registrar(
            nome,
            $"Finalizou turno ({resultado.PedidosPreparados.Count} pratos preparados).",
            TipoEvento.Cozinheiro);
        return resultado;
    }

    private RelatorioSimulacao GerarRelatorio(
        ResultadoGarcom[] resultadosGarcons,
        ResultadoCozinheiro[] resultadosCozinheiros,
        BalcaoPedidos balcao,
        Estoque estoque,
        Caixa caixa,
        TimeSpan duracao,
        TimeSpan cpuConsumido)
    {
        var todosCriados = resultadosGarcons.SelectMany(g => g.PedidosCriados).ToList();
        var todosPreparos = resultadosCozinheiros.SelectMany(c => c.PedidosPreparados).ToList();

        return new RelatorioSimulacao
        {
            TituloSolucao = "SOLUÇÃO INGÊNUA 2 (DEADLOCK)",
            ProblemaAlvo = ProblemaDemonstrado,
            DuracaoRelogio = duracao,
            TempoCpuConsumido = cpuConsumido,
            CapacidadeBalcao = CapacidadeBalcao,
            PedidosCriados = todosCriados.Count,
            PratosFinalizadosTotal = todosPreparos.Count,
            PedidosUnicosFinalizados = todosPreparos.Select(p => p.Codigo).Distinct().Count(),
            PedidosRecusadosSemEstoque = resultadosCozinheiros.Sum(c => c.PedidosRecusadosSemEstoque),
            PedidosNaoAtendidosOuRestantes = balcao.ObterQuantidadeComLock() + resultadosCozinheiros.Sum(c => c.PedidosTravadosDeadlock),
            VendasEsperadas = todosPreparos.Count,
            VendasRegistradasCaixa = caixa.VendasRegistradas,
            FaturamentoEsperado = resultadosCozinheiros.Sum(c => c.FaturamentoEsperadoLocal),
            FaturamentoRegistradoCaixa = caixa.FaturamentoTotal,
            EstoqueFinal = estoque.ObterSaldosFinais(),
            DeadlocksDetectados = resultadosCozinheiros.Sum(c => c.DeadlocksDetectados),
            BloqueiosCascataDeadlock = resultadosCozinheiros.Sum(c => c.BloqueiosCascataDeadlock),
            PedidosTravadosDeadlock = resultadosCozinheiros.Sum(c => c.PedidosTravadosDeadlock)
        };
    }
}
