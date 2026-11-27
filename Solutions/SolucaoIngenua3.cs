using System.Diagnostics;
using RestauranteConcorrente.Logging;
using RestauranteConcorrente.Models;
using RestauranteConcorrente.Resources;

namespace RestauranteConcorrente.Solutions;

/// <summary>
/// SOLUÇÃO INGÊNUA 3 — ESPERA OCUPADA (Busy-Waiting / Spin-Wait).
/// Corrige as race conditions da Ingênua 1 (com locks) e o deadlock da Ingênua 2
/// (ordem global fixa Tábua -> Faca), porém os garçons e cozinheiros ficam em laços
/// 'while' verificando continuamente o balcão sem dormir nem aguardar sinalização,
/// queimando CPU com milhões de verificações inúteis.
/// </summary>
public sealed class SolucaoIngenua3 : ISolucaoSimulacao
{
    private const int QuantidadeGarcons = 3;
    private const int PedidosPorGarcom = 4;
    private const int QuantidadeCozinheiros = 4;
    private const int CapacidadeBalcao = 3;

    public int Numero => 3;
    public string Titulo => "Solução Ingênua 3 — Polling Contínuo no Balcão";
    public string ProblemaDemonstrado => "Espera Ocupada (Busy-Waiting queimando CPU em loop)";

    private readonly EventLogger _logger = new();

    public async Task ExecutarAsync()
    {
        var processo = Process.GetCurrentProcess();
        var cpuInicio = processo.TotalProcessorTime;
        var cronometro = Stopwatch.StartNew();

        _logger.Linha("================================================================================", ConsoleColor.Cyan);
        _logger.Linha("  RESTAURANTE CONCORRENTE — SOLUÇÃO INGÊNUA 3 (ESPERA OCUPADA / BUSY-WAITING)", ConsoleColor.Cyan);
        _logger.Linha($"  Participantes: {QuantidadeGarcons} Garçons ({PedidosPorGarcom} pedidos cada) + {QuantidadeCozinheiros} Cozinheiros | Balcão: {CapacidadeBalcao}", ConsoleColor.Gray);
        _logger.Linha("  Locks corretos e ordenados [Tábua -> Faca], MAS polling em loop 'while' sem bloqueio!", ConsoleColor.DarkYellow);
        _logger.Linha("================================================================================", ConsoleColor.Cyan);

        var balcao = new BalcaoPedidos(CapacidadeBalcao, _logger);
        var estoque = new Estoque(_logger, saldoUniforme: 30);
        var caixa = new Caixa(_logger);
        var utensilios = new UtensiliosCozinha(_logger);

        bool garconsEncerraram = false;

        // Cada participante roda em sua própria Task dedicada
        var tarefasGarcons = Enumerable.Range(1, QuantidadeGarcons)
            .Select(id => Task.Factory.StartNew(
                () => ExecutarGarcomBusyWait(id, balcao),
                TaskCreationOptions.LongRunning))
            .ToArray();

        var tarefasCozinheiros = Enumerable.Range(1, QuantidadeCozinheiros)
            .Select(id => Task.Factory.StartNew(
                () => ExecutarCozinheiroBusyWait(
                    id,
                    balcao,
                    estoque,
                    caixa,
                    utensilios,
                    () => Volatile.Read(ref garconsEncerraram)),
                TaskCreationOptions.LongRunning))
            .ToArray();

        var resultadosGarcons = await Task.WhenAll(tarefasGarcons);
        Volatile.Write(ref garconsEncerraram, true);
        _logger.Registrar("Sistema", "Todos os garçons finalizaram. Aguardando a cozinha esvaziar o balcão...");

        var resultadosCozinheiros = await Task.WhenAll(tarefasCozinheiros);

        cronometro.Stop();
        var cpuConsumido = processo.TotalProcessorTime - cpuInicio;

        GerarRelatorio(resultadosGarcons, resultadosCozinheiros, estoque, caixa, cronometro.Elapsed, cpuConsumido)
            .Exibir(_logger);
    }

    private ResultadoGarcom ExecutarGarcomBusyWait(int id, BalcaoPedidos balcao)
    {
        string nome = $"Garcom-{id}";
        var resultado = new ResultadoGarcom { Autor = nome };
        _logger.Registrar(nome, "Iniciou expediente no salão.", TipoEvento.Garcom);

        for (int i = 1; i <= PedidosPorGarcom; i++)
        {
            // Intervalo aleatório entre pedidos das mesas
            Thread.Sleep(Random.Shared.Next(120, 260));

            var prato = Cardapio.EscolherAleatorio();
            var pedido = new Pedido($"G{id}-{i:D2}", prato, nome);
            _logger.Registrar(nome, $"Anotou pedido {pedido}.", TipoEvento.Garcom);

            // ESPERA OCUPADA (BUG): testa continuamente se abriu vaga no balcão sem dormir/bloquear!
            long iteracoesEspera = 0;
            while (!balcao.TentarAdicionarComLock(pedido, nome))
            {
                iteracoesEspera++;
            }

            resultado.IteracoesEsperaOcupada += iteracoesEspera;
            resultado.PedidosCriados.Add(pedido);

            if (iteracoesEspera >= 50_000)
            {
                _logger.Problema(
                    nome,
                    $"Espera ocupada (busy-waiting)! Executou {iteracoesEspera:N0} verificações inúteis em loop aguardando vaga no balcão para #{pedido.Codigo}!");
            }
        }

        _logger.Registrar(
            nome,
            $"Encerrou expediente ({resultado.PedidosCriados.Count} pedidos | {resultado.IteracoesEsperaOcupada:N0} spins).",
            TipoEvento.Garcom);
        return resultado;
    }

    private ResultadoCozinheiro ExecutarCozinheiroBusyWait(
        int id,
        BalcaoPedidos balcao,
        Estoque estoque,
        Caixa caixa,
        UtensiliosCozinha utensilios,
        Func<bool> garconsEncerraram)
    {
        string nome = $"Cozinheiro-{id}";
        var resultado = new ResultadoCozinheiro { Autor = nome };
        _logger.Registrar(nome, "Entrou na cozinha aguardando pedidos (em polling contínuo).", TipoEvento.Cozinheiro);

        long spinsNesteIntervalo = 0;

        while (true)
        {
            if (balcao.TentarRetirarComLock(nome, out var pedido) && pedido is not null)
            {
                if (spinsNesteIntervalo >= 150_000)
                {
                    _logger.Problema(
                        nome,
                        $"Espera ocupada (busy-waiting)! Executou {spinsNesteIntervalo:N0} verificações inúteis em loop esperando pedido no balcão (queimando CPU)!");
                }

                resultado.IteracoesEsperaOcupada += spinsNesteIntervalo;
                spinsNesteIntervalo = 0;

                if (!estoque.TentarConsumirComLock(pedido, nome))
                {
                    resultado.PedidosRecusadosSemEstoque++;
                    continue;
                }

                // Ordem global fixa Tábua -> Faca (sem deadlock)
                utensilios.UsarComOrdemFixa(pedido, nome);

                int tempoPreparo = Random.Shared.Next(
                    pedido.Prato.TempoPreparoMinMs,
                    pedido.Prato.TempoPreparoMaxMs + 1);
                Thread.Sleep(tempoPreparo);

                pedido.Status = StatusPedido.Pronto;
                resultado.PedidosPreparados.Add(pedido);
                resultado.FaturamentoEsperadoLocal += pedido.Prato.Preco;

                caixa.RegistrarVendaComLock(pedido, nome);
            }
            else
            {
                if (garconsEncerraram() && balcao.EstaVazioComLock())
                {
                    if (spinsNesteIntervalo >= 150_000)
                    {
                        _logger.Problema(
                            nome,
                            $"Espera ocupada (busy-waiting)! Executou {spinsNesteIntervalo:N0} verificações inúteis em loop antes do encerramento!");
                    }

                    resultado.IteracoesEsperaOcupada += spinsNesteIntervalo;
                    break;
                }

                // ESPERA OCUPADA (BUG): não bloqueia nem cede o núcleo — repete o loop imediatamente!
                spinsNesteIntervalo++;
            }
        }

        _logger.Registrar(
            nome,
            $"Finalizou turno ({resultado.PedidosPreparados.Count} pratos | {resultado.IteracoesEsperaOcupada:N0} verificações inúteis em loop).",
            TipoEvento.Cozinheiro);
        return resultado;
    }

    private RelatorioSimulacao GerarRelatorio(
        ResultadoGarcom[] resultadosGarcons,
        ResultadoCozinheiro[] resultadosCozinheiros,
        Estoque estoque,
        Caixa caixa,
        TimeSpan duracao,
        TimeSpan cpuConsumido)
    {
        var todosCriados = resultadosGarcons.SelectMany(g => g.PedidosCriados).ToList();
        var todosPreparos = resultadosCozinheiros.SelectMany(c => c.PedidosPreparados).ToList();

        long totalSpins = resultadosGarcons.Sum(g => g.IteracoesEsperaOcupada)
                        + resultadosCozinheiros.Sum(c => c.IteracoesEsperaOcupada);

        return new RelatorioSimulacao
        {
            TituloSolucao = "SOLUÇÃO INGÊNUA 3 (ESPERA OCUPADA / BUSY-WAITING)",
            ProblemaAlvo = ProblemaDemonstrado,
            DuracaoRelogio = duracao,
            TempoCpuConsumido = cpuConsumido,
            CapacidadeBalcao = CapacidadeBalcao,
            PedidosCriados = todosCriados.Count,
            PratosFinalizadosTotal = todosPreparos.Count,
            PedidosUnicosFinalizados = todosPreparos.Select(p => p.Codigo).Distinct().Count(),
            PedidosRecusadosSemEstoque = resultadosCozinheiros.Sum(c => c.PedidosRecusadosSemEstoque),
            VendasEsperadas = todosPreparos.Count,
            VendasRegistradasCaixa = caixa.VendasRegistradas,
            FaturamentoEsperado = resultadosCozinheiros.Sum(c => c.FaturamentoEsperadoLocal),
            FaturamentoRegistradoCaixa = caixa.FaturamentoTotal,
            EstoqueFinal = estoque.ObterSaldosFinais(),
            IteracoesEsperaOcupada = totalSpins
        };
    }
}
