using RestauranteConcorrente.Logging;
using RestauranteConcorrente.Models;

namespace RestauranteConcorrente.Resources;

/// <summary>
/// Recurso compartilhado: Balcão onde os garçons colocam pedidos e os cozinheiros retiram.
/// SOLUÇÃO INGÊNUA 1: intencionalmente SEM nenhum controle de concorrência.
/// </summary>
public sealed class BalcaoPedidos
{
    private readonly List<Pedido> _pedidos = new(capacity: 64);
    private readonly object _lockBalcao = new();
    private readonly EventLogger _logger;

    public int CapacidadeMaxima { get; }
    public int Quantidade => _pedidos.Count;
    public bool EstaVazio => _pedidos.Count == 0;

    public BalcaoPedidos(int capacidadeMaxima, EventLogger logger)
    {
        CapacidadeMaxima = capacidadeMaxima;
        _logger = logger;
    }

    public async Task<(bool Adicionado, bool EstourouCapacidade, bool ErroLista)> AdicionarSemControleAsync(
        Pedido pedido,
        string autor)
    {
        try
        {
            // Check sem sincronização: vários garçons podem passar juntos quando há 1 vaga
            if (_pedidos.Count >= CapacidadeMaxima)
            {
                return (false, false, false);
            }

            // Tempo aleatório simulando a colocação da comanda no balcão
            await Task.Delay(Random.Shared.Next(15, 45));

            // Act sem sincronização
            _pedidos.Add(pedido);
            pedido.Status = StatusPedido.NoBalcao;

            int quantidadeAtual = _pedidos.Count;
            _logger.Registrar(
                autor,
                $"Colocou pedido {pedido} no balcão ({quantidadeAtual}/{CapacidadeMaxima}).",
                TipoEvento.Garcom);

            bool estourou = quantidadeAtual > CapacidadeMaxima;
            if (estourou)
            {
                _logger.Problema(
                    autor,
                    $"Balcão ultrapassou a capacidade máxima ({quantidadeAtual}/{CapacidadeMaxima}) ao inserir #{pedido.Codigo}!");
            }

            return (true, estourou, false);
        }
        catch (Exception ex)
        {
            _logger.Problema(
                autor,
                $"Erro de concorrência ao inserir #{pedido.Codigo} no balcão: {ex.GetType().Name} - {ex.Message}");
            return (false, false, true);
        }
    }

    public async Task<(Pedido? Pedido, bool Duplicado, bool ErroLista)> RetirarSemControleAsync(string autor)
    {
        Pedido? pedidoLido = null;

        try
        {
            // Check sem sincronização: vários cozinheiros veem o mesmo primeiro item
            if (_pedidos.Count == 0)
            {
                return (null, false, false);
            }

            // Leitura do primeiro item sem proteção
            pedidoLido = _pedidos[0];

            // Tempo aleatório simulando leitura da comanda antes de tirá-la do gancho
            await Task.Delay(Random.Shared.Next(15, 45));

            // Remoção sem proteção: pode remover outro pedido ou falhar se a lista esvaziou
            _pedidos.RemoveAt(0);

            if (pedidoLido is null)
            {
                _logger.Problema(
                    autor,
                    "Leu referência nula no balcão devido a mutação concorrente na List<Pedido>!");
                return (null, false, true);
            }

            string? responsavelAnterior = pedidoLido.CozinheiroResponsavel;
            pedidoLido.VezesPego++;

            if (responsavelAnterior is not null)
            {
                _logger.Problema(
                    autor,
                    $"Condição de corrida no balcão! Pegou o MESMO pedido #{pedidoLido.Codigo} ({pedidoLido.Prato.Nome}) que JÁ estava com {responsavelAnterior} ({pedidoLido.VezesPego}ª vez)!");
                return (pedidoLido, true, false);
            }

            pedidoLido.CozinheiroResponsavel = autor;
            pedidoLido.Status = StatusPedido.EmPreparo;

            _logger.Registrar(
                autor,
                $"Retirou pedido {pedidoLido} do balcão (restam {_pedidos.Count}/{CapacidadeMaxima}).",
                TipoEvento.Cozinheiro);

            return (pedidoLido, false, false);
        }
        catch (Exception ex)
        {
            string detalhePedido = pedidoLido is not null
                ? $"enquanto tentava retirar #{pedidoLido.Codigo}"
                : "ao consultar o início da fila";

            _logger.Problema(
                autor,
                $"Erro de concorrência no balcão {detalhePedido}: {ex.GetType().Name} (lista alterada por outra thread)!");
            return (null, false, true);
        }
    }

    // -------------------------------------------------------------------------
    // Operações com lock (usadas a partir da Solução Ingênua 2 para eliminar
    // a race condition na lista e evidenciar outros problemas de concorrência).
    // -------------------------------------------------------------------------

    public bool EstaVazioComLock()
    {
        lock (_lockBalcao)
        {
            return _pedidos.Count == 0;
        }
    }

    public int ObterQuantidadeComLock()
    {
        lock (_lockBalcao)
        {
            return _pedidos.Count;
        }
    }

    public bool TentarAdicionarComLock(Pedido pedido, string autor)
    {
        lock (_lockBalcao)
        {
            if (_pedidos.Count >= CapacidadeMaxima)
            {
                return false;
            }

            _pedidos.Add(pedido);
            pedido.Status = StatusPedido.NoBalcao;

            _logger.Registrar(
                autor,
                $"Colocou pedido {pedido} no balcão ({_pedidos.Count}/{CapacidadeMaxima}).",
                TipoEvento.Garcom);
            return true;
        }
    }

    public bool TentarRetirarComLock(string autor, out Pedido? pedido)
    {
        lock (_lockBalcao)
        {
            if (_pedidos.Count == 0)
            {
                pedido = null;
                return false;
            }

            pedido = _pedidos[0];
            _pedidos.RemoveAt(0);
            pedido.CozinheiroResponsavel = autor;
            pedido.VezesPego++;
            pedido.Status = StatusPedido.EmPreparo;

            _logger.Registrar(
                autor,
                $"Retirou pedido {pedido} do balcão (restam {_pedidos.Count}/{CapacidadeMaxima}).",
                TipoEvento.Cozinheiro);
            return true;
        }
    }
}
