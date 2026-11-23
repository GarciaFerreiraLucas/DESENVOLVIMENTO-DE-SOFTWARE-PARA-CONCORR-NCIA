namespace RestauranteConcorrente.Models;

public sealed class ResultadoGarcom
{
    public required string Autor { get; init; }
    public List<Pedido> PedidosCriados { get; } = [];
    public int EstourosBalcaoObservados { get; set; }
    public int ErrosConcorrenciaLista { get; set; }
}

public sealed class ResultadoCozinheiro
{
    public required string Autor { get; init; }
    public List<Pedido> PedidosPreparados { get; } = [];
    public int PedidosRecusadosSemEstoque { get; set; }
    public int PedidosDuplicadosObservados { get; set; }
    public int EstoquesNegativosObservados { get; set; }
    public int LostUpdatesCaixaObservados { get; set; }
    public int ErrosConcorrenciaLista { get; set; }
    public decimal FaturamentoEsperadoLocal { get; set; }
}
