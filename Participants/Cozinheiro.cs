using RestauranteConcorrente.Logging;
using RestauranteConcorrente.Models;
using RestauranteConcorrente.Resources;

namespace RestauranteConcorrente.Participants;

/// <summary>
/// Participante consumidor: retira pedidos do balcão, consome ingredientes do estoque,
/// prepara os pratos em tempo aleatório e registra a venda no caixa.
/// Cada instância executa em sua própria Task.
/// </summary>
public sealed class Cozinheiro
{
    private readonly int _id;
    private readonly BalcaoPedidos _balcao;
    private readonly Estoque _estoque;
    private readonly Caixa _caixa;
    private readonly EventLogger _logger;

    public string Nome => $"Cozinheiro-{_id}";

    public Cozinheiro(
        int id,
        BalcaoPedidos balcao,
        Estoque estoque,
        Caixa caixa,
        EventLogger logger)
    {
        _id = id;
        _balcao = balcao;
        _estoque = estoque;
        _caixa = caixa;
        _logger = logger;
    }

    public async Task<ResultadoCozinheiro> ExecutarAsync(Func<bool> garconsEncerraram)
    {
        var resultado = new ResultadoCozinheiro { Autor = Nome };
        var limiteSeguranca = DateTime.UtcNow.AddSeconds(20);

        _logger.Registrar(Nome, "Entrou na cozinha aguardando pedidos.", TipoEvento.Cozinheiro);

        while (DateTime.UtcNow < limiteSeguranca)
        {
            if (garconsEncerraram() && _balcao.EstaVazio)
            {
                break;
            }

            var (pedido, duplicado, erroLista) = await _balcao.RetirarSemControleAsync(Nome);

            if (erroLista)
            {
                resultado.ErrosConcorrenciaLista++;
            }

            if (duplicado)
            {
                resultado.PedidosDuplicadosObservados++;
            }

            if (pedido is null)
            {
                await Task.Delay(Random.Shared.Next(15, 50));
                continue;
            }

            var (temIngredientes, negativosGerados) = await _estoque.ConsumirSemControleAsync(pedido, Nome);
            resultado.EstoquesNegativosObservados += negativosGerados;

            if (!temIngredientes)
            {
                resultado.PedidosRecusadosSemEstoque++;
                continue;
            }

            // Tempo aleatório de preparo do prato usando Random.Shared
            int tempoPreparo = Random.Shared.Next(
                pedido.Prato.TempoPreparoMinMs,
                pedido.Prato.TempoPreparoMaxMs + 1);

            _logger.Registrar(
                Nome,
                $"Preparando {pedido} ({tempoPreparo} ms)...",
                TipoEvento.Cozinheiro);

            await Task.Delay(tempoPreparo);

            pedido.Status = StatusPedido.Pronto;
            resultado.PedidosPreparados.Add(pedido);
            resultado.FaturamentoEsperadoLocal += pedido.Prato.Preco;

            bool houveLostUpdate = await _caixa.RegistrarVendaSemControleAsync(pedido, Nome);
            if (houveLostUpdate)
            {
                resultado.LostUpdatesCaixaObservados++;
            }
        }

        _logger.Registrar(
            Nome,
            $"Finalizou turno ({resultado.PedidosPreparados.Count} pratos preparados).",
            TipoEvento.Cozinheiro);

        return resultado;
    }
}
