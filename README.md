# Restaurante Concorrente — Solução Ingênua 1 (C# / .NET)

Simulação concorrente de uma cozinha e salão de restaurante desenvolvida em **C# (.NET 10)** para demonstrar, na prática e em tempo real nos logs, os problemas clássicos causados pelo acesso simultâneo a recursos compartilhados **sem nenhum controle de concorrência**.

---

## Cenário

Um restaurante em horário de movimento possui vários participantes atuando em paralelo, cada um executando em sua própria `Task` com tempos aleatórios gerados por `Random.Shared`:

- **3 Garçons (`Garcom-1`, `Garcom-2`, `Garcom-3`) — Produtores:** atendem mesas em intervalos aleatórios, escolhem um prato do cardápio e colocam a comanda no **Balcão de Pedidos** (capacidade máxima de 4 pedidos).
- **4 Cozinheiros (`Cozinheiro-1` a `Cozinheiro-4`) — Consumidores:** retiram pedidos do **Balcão de Pedidos**, verificam e consomem os ingredientes no **Estoque**, preparam o prato em tempo aleatório e registram a venda no **Caixa**.

### Recursos Compartilhados (sem sincronização na Solução Ingênua 1)

| Recurso | Classe | Tipo interno | Problema observado sem controle |
| :--- | :--- | :--- | :--- |
| **Balcão de Pedidos** | `Resources/BalcaoPedidos.cs` | `List<Pedido>` | Pedidos pegos e preparados em duplicidade, pedidos perdidos, estouro de capacidade (`> 4`) e `ArgumentOutOfRangeException` |
| **Estoque** | `Resources/Estoque.cs` | `Dictionary<string, int>` | Condição de corrida *check-then-act* gerando **estoque negativo** (`-1`, `-2`) |
| **Caixa** | `Resources/Caixa.cs` | `decimal` + `int` | **Lost Update** (atualizações perdidas), fazendo o faturamento registrado ficar menor que o faturamento real |

---

## Estrutura do Projeto

```text
.
├── Program.cs                        # Ponto de entrada da aplicação
├── RestauranteConcorrente.csproj     # Projeto .NET 10
├── Logging/
│   └── EventLogger.cs                # Logger com horário (ms), ID da thread e autor
├── Models/
│   ├── Prato.cs                      # Modelo de prato (nome, preço, ingredientes, tempos)
│   ├── Cardapio.cs                   # Cardápio com seleção aleatória (Random.Shared)
│   ├── Pedido.cs                     # Comanda do pedido
│   └── Resultados.cs                 # Métricas locais retornadas por cada Task
├── Resources/
│   ├── BalcaoPedidos.cs              # Balcão compartilhado sem sincronização
│   ├── Estoque.cs                    # Estoque compartilhado sem sincronização
│   └── Caixa.cs                      # Caixa compartilhado sem sincronização
├── Participants/
│   ├── Garcom.cs                     # Participante produtor (executa em Task)
│   └── Cozinheiro.cs                 # Participante consumidor (executa em Task)
└── Solutions/
    └── SolucaoIngenua1.cs            # Orquestração da Solução Ingênua 1 e relatório final
```

---

## Como Executar

Pré-requisito: **.NET SDK 10.0** (ou compatível).

```bash
dotnet run
```

Cada linha do log registra **horário com milissegundos**, **ID da thread**, **autor** e o **evento**:

```text
[16:55:46.403] [T10] [Cozinheiro-2 ] Retirou pedido #G3-01 (Pizza Margherita - R$ 48,00) do balcão (restam 1/4).
[16:55:46.417] [T10] [Cozinheiro-3 ] [PROBLEMA] Condição de corrida no balcão! Pegou o MESMO pedido #G3-01 (Pizza Margherita) que JÁ estava com Cozinheiro-4 (2ª vez)!
[16:55:46.420] [T07] [Cozinheiro-1 ] [PROBLEMA] Erro de concorrência no balcão enquanto tentava retirar #G3-01: ArgumentOutOfRangeException (lista alterada por outra thread)!
[16:55:46.620] [T09] [Garcom-2     ] [PROBLEMA] Balcão ultrapassou a capacidade máxima (5/4) ao inserir #G2-03!
[16:55:47.232] [T09] [Cozinheiro-1 ] [PROBLEMA] Lost update no Caixa ao registrar #G2-03 (+R$ 34,00): leu R$ 192,00, mas já era R$ 226,00 -> sobrescreveu para R$ 226,00!
```

Ao final da execução, o console exibe o **Relatório Final** consolidando todos os problemas de concorrência detectados na rodada (duplicidades, pedidos perdidos, estouros de capacidade, exceções de acesso concorrente, estoque negativo e diferença de faturamento por *lost update*).
