using System.Text;
using RestauranteConcorrente.Solutions;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

Func<ISolucaoSimulacao>[] fabricasSolucoes =
[
    () => new SolucaoIngenua1(),
    () => new SolucaoIngenua2(),
    () => new SolucaoIngenua3()
];

// Permite execução direta via argumento CLI (ex.: "dotnet run -- 2") para testes/automação
if (args.Length > 0 && int.TryParse(args[0], out int opcaoCli)
    && opcaoCli >= 1 && opcaoCli <= fabricasSolucoes.Length)
{
    await fabricasSolucoes[opcaoCli - 1]().ExecutarAsync();
    return;
}

while (true)
{
    ExibirMenu(fabricasSolucoes);
    Console.Write("Escolha uma opção: ");
    string? entrada = Console.ReadLine()?.Trim();

    if (entrada is null || entrada == "0")
    {
        Console.WriteLine("\nEncerrando o Restaurante Concorrente. Até logo!");
        break;
    }

    if (int.TryParse(entrada, out int opcao) && opcao >= 1 && opcao <= fabricasSolucoes.Length)
    {
        Console.WriteLine();
        var solucao = fabricasSolucoes[opcao - 1]();
        await solucao.ExecutarAsync();

        if (!Console.IsInputRedirected)
        {
            Console.WriteLine("\nPressione ENTER para voltar ao menu...");
            Console.ReadLine();
        }
        else
        {
            Console.WriteLine();
        }
    }
    else
    {
        var corOriginal = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\nOpção inválida: '{entrada}'. Informe um número entre 0 e {fabricasSolucoes.Length}.\n");
        Console.ForegroundColor = corOriginal;
    }
}

static void ExibirMenu(Func<ISolucaoSimulacao>[] fabricas)
{
    var corOriginal = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("================================================================================");
    Console.WriteLine("             RESTAURANTE CONCORRENTE — MENU DE SOLUÇÕES INGÊNUAS                ");
    Console.WriteLine("             Desenvolvimento de Software para Concorrência (C# / .NET)          ");
    Console.WriteLine("================================================================================");
    Console.ForegroundColor = corOriginal;

    foreach (var fabrica in fabricas)
    {
        var info = fabrica();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  [{info.Numero}] ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(info.Titulo);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"      Problema: {info.ProblemaDemonstrado}");
    }

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("  [0] ");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("Sair");

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("================================================================================");
    Console.ForegroundColor = corOriginal;
}
