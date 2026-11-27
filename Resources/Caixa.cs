using RestauranteConcorrente.Logging;
using RestauranteConcorrente.Models;

namespace RestauranteConcorrente.Resources;

/// <summary>
/// Recurso compartilhado: Caixa que acumula o faturamento e a contagem de vendas.
/// SOLUÇÃO INGÊNUA 1: intencionalmente SEM nenhum controle de concorrência (sujeito a Lost Update).
/// </summary>
public sealed class Caixa
{
    private readonly EventLogger _logger;
    private readonly object _lockCaixa = new();

    public decimal FaturamentoTotal { get; private set; }
    public int VendasRegistradas { get; private set; }

    public Caixa(EventLogger logger)
    {
        _logger = logger;
    }

    public async Task<bool> RegistrarVendaSemControleAsync(Pedido pedido, string autor)
    {
        // Leitura sem sincronização
        decimal saldoLido = FaturamentoTotal;
        int vendasLidas = VendasRegistradas;

        // Tempo aleatório simulando processamento do pagamento/lançamento no caixa
        await Task.Delay(Random.Shared.Next(20, 60));

        // Escrita sem sincronização (sobrescreve alterações feitas por outras threads no intervalo)
        decimal saldoAntesDeGravar = FaturamentoTotal;
        decimal novoSaldo = saldoLido + pedido.Prato.Preco;
        int novasVendas = vendasLidas + 1;

        bool houveLostUpdate = saldoAntesDeGravar != saldoLido;

        FaturamentoTotal = novoSaldo;
        VendasRegistradas = novasVendas;

        if (houveLostUpdate)
        {
            _logger.Problema(
                autor,
                $"Lost update no Caixa ao registrar #{pedido.Codigo} (+R$ {pedido.Prato.Preco:F2}): leu R$ {saldoLido:F2}, mas já era R$ {saldoAntesDeGravar:F2} -> sobrescreveu para R$ {novoSaldo:F2}!");
        }
        else
        {
            _logger.Registrar(
                autor,
                $"Finalizou #{pedido.Codigo} e registrou R$ {pedido.Prato.Preco:F2} no Caixa (Total: R$ {novoSaldo:F2}).",
                TipoEvento.Sucesso);
        }

        return houveLostUpdate;
    }

    /// <summary>
    /// Versão protegida com lock (elimina lost update nas soluções posteriores).
    /// </summary>
    public void RegistrarVendaComLock(Pedido pedido, string autor)
    {
        lock (_lockCaixa)
        {
            FaturamentoTotal += pedido.Prato.Preco;
            VendasRegistradas++;

            _logger.Registrar(
                autor,
                $"Finalizou #{pedido.Codigo} e registrou R$ {pedido.Prato.Preco:F2} no Caixa (Total: R$ {FaturamentoTotal:F2}).",
                TipoEvento.Sucesso);
        }
    }
}
