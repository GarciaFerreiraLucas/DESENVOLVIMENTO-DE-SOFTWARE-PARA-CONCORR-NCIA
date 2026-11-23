using RestauranteConcorrente.Logging;
using RestauranteConcorrente.Models;
using RestauranteConcorrente.Resources;

namespace RestauranteConcorrente.Participants;

/// <summary>
/// Participante produtor: atende mesas e envia pedidos ao balcão em tempos aleatórios.
/// Cada instância executa em sua própria Task.
/// </summary>
public sealed class Garcom
{
    private readonly int _id;
    private readonly int _quantidadePedidos;
    private readonly BalcaoPedidos _balcao;
    private readonly EventLogger _logger;

    public string Nome => $"Garcom-{_id}";

    public Garcom(int id, int quantidadePedidos, BalcaoPedidos balcao, EventLogger logger)
    {
        _id = id;
        _quantidadePedidos = quantidadePedidos;
        _balcao = balcao;
        _logger = logger;
    }

    public async Task<ResultadoGarcom> ExecutarAsync()
    {
        var resultado = new ResultadoGarcom { Autor = Nome };
        _logger.Registrar(Nome, "Iniciou expediente no salão.", TipoEvento.Garcom);

        for (int i = 1; i <= _quantidadePedidos; i++)
        {
            // Tempo aleatório até o cliente fazer um novo pedido
            int tempoAtendimento = Random.Shared.Next(30, 120);
            await Task.Delay(tempoAtendimento);

            var prato = Cardapio.EscolherAleatorio();
            var codigo = $"G{_id}-{i:D2}";
            var pedido = new Pedido(codigo, prato, Nome);

            _logger.Registrar(Nome, $"Anotou pedido {pedido}.", TipoEvento.Garcom);

            bool colocado = false;
            int tentativas = 0;

            while (!colocado && tentativas < 40)
            {
                tentativas++;
                var (adicionado, estourou, erroLista) = await _balcao.AdicionarSemControleAsync(pedido, Nome);

                if (estourou)
                {
                    resultado.EstourosBalcaoObservados++;
                }

                if (erroLista)
                {
                    resultado.ErrosConcorrenciaLista++;
                }

                if (adicionado)
                {
                    colocado = true;
                    resultado.PedidosCriados.Add(pedido);
                }
                else
                {
                    // Balcão cheio ou falha transitória: aguarda tempo aleatório e tenta novamente
                    await Task.Delay(Random.Shared.Next(20, 60));
                }
            }
        }

        _logger.Registrar(
            Nome,
            $"Encerrou expediente ({resultado.PedidosCriados.Count} pedidos enviados).",
            TipoEvento.Garcom);

        return resultado;
    }
}
