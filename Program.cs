using System.Text;
using RestauranteConcorrente.Solutions;

Console.OutputEncoding = Encoding.UTF8;

var simulacao = new SolucaoIngenua1();
await simulacao.ExecutarAsync();
