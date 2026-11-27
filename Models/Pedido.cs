namespace RestauranteConcorrente.Models;

public enum StatusPedido
{
    Criado,
    NoBalcao,
    EmPreparo,
    Pronto,
    RecusadoSemEstoque,
    TravadoEmDeadlock
}

public sealed class Pedido
{
    public string Codigo { get; }
    public Prato Prato { get; }
    public string GarcomOrigem { get; }
    public DateTime CriadoEm { get; }

    // Campos intencionalmente sem proteção de concorrência na Solução Ingênua 1
    public StatusPedido Status { get; set; } = StatusPedido.Criado;
    public string? CozinheiroResponsavel { get; set; }
    public int VezesPego { get; set; }

    public Pedido(string codigo, Prato prato, string garcomOrigem)
    {
        Codigo = codigo;
        Prato = prato;
        GarcomOrigem = garcomOrigem;
        CriadoEm = DateTime.Now;
    }

    public override string ToString() => $"#{Codigo} ({Prato.Nome} - R$ {Prato.Preco:F2})";
}
