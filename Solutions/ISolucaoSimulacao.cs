namespace RestauranteConcorrente.Solutions;

public interface ISolucaoSimulacao
{
    int Numero { get; }
    string Titulo { get; }
    string ProblemaDemonstrado { get; }
    Task ExecutarAsync();
}
