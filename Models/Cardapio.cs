namespace RestauranteConcorrente.Models;

public static class Cardapio
{
    public static IReadOnlyList<Prato> Pratos { get; } =
    [
        new Prato(
            Nome: "Hambúrguer Artesanal",
            Preco: 34.00m,
            Ingredientes: ["Pao", "Carne", "Queijo"],
            TempoPreparoMinMs: 140,
            TempoPreparoMaxMs: 280),

        new Prato(
            Nome: "Pizza Margherita",
            Preco: 48.00m,
            Ingredientes: ["Massa", "Queijo", "Tomate"],
            TempoPreparoMinMs: 180,
            TempoPreparoMaxMs: 320),

        new Prato(
            Nome: "Lasanha Bolonhesa",
            Preco: 42.00m,
            Ingredientes: ["Massa", "Carne", "Queijo", "Tomate"],
            TempoPreparoMinMs: 200,
            TempoPreparoMaxMs: 360),

        new Prato(
            Nome: "Salada Caesar",
            Preco: 26.00m,
            Ingredientes: ["Alface", "Queijo", "Tomate"],
            TempoPreparoMinMs: 90,
            TempoPreparoMaxMs: 180)
    ];

    public static Prato EscolherAleatorio()
    {
        int indice = Random.Shared.Next(Pratos.Count);
        return Pratos[indice];
    }
}
