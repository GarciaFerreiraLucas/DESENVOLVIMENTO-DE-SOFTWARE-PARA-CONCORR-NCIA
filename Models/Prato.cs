namespace RestauranteConcorrente.Models;

public sealed record Prato(
    string Nome,
    decimal Preco,
    IReadOnlyList<string> Ingredientes,
    int TempoPreparoMinMs,
    int TempoPreparoMaxMs);
