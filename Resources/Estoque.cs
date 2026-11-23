using RestauranteConcorrente.Logging;
using RestauranteConcorrente.Models;

namespace RestauranteConcorrente.Resources;

/// <summary>
/// Recurso compartilhado: Estoque de ingredientes da cozinha.
/// SOLUÇÃO INGÊNUA 1: intencionalmente SEM nenhum controle de concorrência.
/// </summary>
public sealed class Estoque
{
    private readonly Dictionary<string, int> _quantidades;
    private readonly EventLogger _logger;

    public Estoque(EventLogger logger)
    {
        _logger = logger;

        // Todas as chaves são pré-cadastradas aqui; a disputa ocorre na leitura/escrita dos saldos.
        _quantidades = new Dictionary<string, int>
        {
            ["Queijo"] = 10,
            ["Tomate"] = 8,
            ["Carne"] = 6,
            ["Massa"] = 6,
            ["Pao"] = 6,
            ["Alface"] = 6
        };
    }

    public async Task<(bool Sucesso, int NegativosGerados)> ConsumirSemControleAsync(
        Pedido pedido,
        string autor)
    {
        // Passo 1 (CHECK) sem lock: verifica se todos os ingredientes têm saldo >= 1
        foreach (string ingrediente in pedido.Prato.Ingredientes)
        {
            int disponivel = _quantidades[ingrediente];
            if (disponivel <= 0)
            {
                pedido.Status = StatusPedido.RecusadoSemEstoque;
                _logger.Registrar(
                    autor,
                    $"Recusou pedido #{pedido.Codigo} ({pedido.Prato.Nome}): faltou '{ingrediente}' (saldo: {disponivel}).",
                    TipoEvento.Aviso);
                return (false, 0);
            }
        }

        // Tempo aleatório simulando separação dos ingredientes na despensa (abre janela de corrida)
        await Task.Delay(Random.Shared.Next(20, 60));

        // Passo 2 (ACT) sem lock: lê e decrementa cada ingrediente sem atomicidade
        int negativosGerados = 0;
        foreach (string ingrediente in pedido.Prato.Ingredientes)
        {
            int saldoAtual = _quantidades[ingrediente];
            int novoSaldo = saldoAtual - 1;
            _quantidades[ingrediente] = novoSaldo;

            if (novoSaldo < 0)
            {
                negativosGerados++;
                _logger.Problema(
                    autor,
                    $"Estoque de '{ingrediente}' ficou NEGATIVO ({novoSaldo}) ao preparar #{pedido.Codigo}! (Check-then-act sem lock)");
            }
        }

        _logger.Registrar(
            autor,
            $"Separou ingredientes [{string.Join(", ", pedido.Prato.Ingredientes)}] para #{pedido.Codigo}.",
            TipoEvento.Cozinheiro);

        return (true, negativosGerados);
    }

    /// <summary>
    /// Chamado apenas ao final da simulação (após Task.WhenAll), em thread única.
    /// </summary>
    public IReadOnlyDictionary<string, int> ObterSaldosFinais() => _quantidades;
}
