namespace RestauranteConcorrente.Logging;

public enum TipoEvento
{
    Sistema,
    Garcom,
    Cozinheiro,
    Sucesso,
    Aviso,
    Problema
}

/// <summary>
/// Responsável por registrar cada evento da simulação com horário (ms),
/// ID da thread e autor do evento.
/// </summary>
public sealed class EventLogger
{
    // Lock usado EXCLUSIVAMENTE para evitar mistura de cores/caracteres no Console.
    // Nenhum recurso ou lógica do cenário (Balcão, Estoque, Caixa) usa controle de concorrência.
    private readonly object _consoleLock = new();

    public void Registrar(string autor, string mensagem, TipoEvento tipo = TipoEvento.Sistema)
    {
        var horario = DateTime.Now.ToString("HH:mm:ss.fff");
        var threadId = Environment.CurrentManagedThreadId;
        var linha = $"[{horario}] [T{threadId:D2}] [{autor,-13}] {mensagem}";

        lock (_consoleLock)
        {
            var corOriginal = Console.ForegroundColor;
            Console.ForegroundColor = ObterCor(tipo);
            Console.WriteLine(linha);
            Console.ForegroundColor = corOriginal;
        }
    }

    public void Problema(string autor, string mensagem)
        => Registrar(autor, $"[PROBLEMA] {mensagem}", TipoEvento.Problema);

    public void Linha(string texto, ConsoleColor? cor = null)
    {
        lock (_consoleLock)
        {
            var corOriginal = Console.ForegroundColor;
            if (cor.HasValue)
            {
                Console.ForegroundColor = cor.Value;
            }

            Console.WriteLine(texto);
            Console.ForegroundColor = corOriginal;
        }
    }

    private static ConsoleColor ObterCor(TipoEvento tipo) => tipo switch
    {
        TipoEvento.Garcom => ConsoleColor.Cyan,
        TipoEvento.Cozinheiro => ConsoleColor.Yellow,
        TipoEvento.Sucesso => ConsoleColor.Green,
        TipoEvento.Aviso => ConsoleColor.DarkYellow,
        TipoEvento.Problema => ConsoleColor.Red,
        _ => ConsoleColor.White
    };
}
