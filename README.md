# Restaurante Concorrente — Soluções Ingênuas (C# / .NET)

Simulação concorrente de uma cozinha e salão de restaurante desenvolvida em **C# (.NET 10)** para demonstrar, na prática e em tempo real nos logs, os principais problemas clássicos de concorrência através de uma progressão de **3 Soluções Ingênuas**, selecionáveis via **menu interativo no console** e encerradas com um **Relatório Final unificado com contadores dos problemas**.

---

## Cenário

Um restaurante em horário de movimento possui vários participantes atuando em paralelo, cada um executando em sua própria `Task` com tempos aleatórios gerados por `Random.Shared`:

- **3 Garçons (`Garcom-1` a `Garcom-3`) — Produtores:** atendem mesas em intervalos aleatórios, escolhem pratos do cardápio e colocam as comandas no **Balcão de Pedidos**.
- **4 Cozinheiros (`Cozinheiro-1` a `Cozinheiro-4`) — Consumidores:** retiram pedidos do **Balcão**, consomem ingredientes do **Estoque**, utilizam os **Utensílios da Cozinha (`Tábua` e `Faca`)**, finalizam o prato e registram a venda no **Caixa**.

---

## Menu e Soluções Ingênuas Implementadas

Ao executar `dotnet run`, o programa apresenta um **menu interativo no console** com uma opção para cada solução (também aceita argumento de linha de comando, ex.: `dotnet run -- 2`):

| Opção | Solução | O que faz | Problema de Concorrência Demonstrado |
| :---: | :--- | :--- | :--- |
| **`[1]`** | **Solução Ingênua 1** — Sem Controle de Concorrência | Todos os recursos (`Balcão`, `Estoque` e `Caixa`) são acessados diretamente sem qualquer sincronização. | **Race Condition**, pedidos duplicados/perdidos, estouro de capacidade do balcão, exceção em `List<T>`, **estoque negativo** (*check-then-act*) e **Lost Update** no Caixa. |
| **`[2]`** | **Solução Ingênua 2** — Locks em Ordem Invertida nos Utensílios | Protege `Balcão`, `Estoque` e `Caixa` com `lock` (eliminando as race conditions), mas trava `Tábua` e `Faca` em ordens opostas conforme o prato (`Hambúrguer`/`Salada`: `Tábua -> Faca`; `Pizza`/`Lasanha`: `Faca -> Tábua`). | **Deadlock (Espera Circular)** — cozinheiros travam esperando mutuamente os utensílios e bloqueiam o restante da cozinha em cascata (detectado após timeout de segurança de 2,5 s para exibir o relatório). |
| **`[3]`** | **Solução Ingênua 3** — Polling Contínuo no Balcão | Mantém os locks corretos e adota **ordem global fixa** nos utensílios (`Tábua -> Faca`, eliminando o deadlock), mas faz garçons e cozinheiros testarem o balcão em laço `while` contínuo sem dormir nem bloquear. | **Espera Ocupada (*Busy-Waiting*)** — zero corrupção de dados e zero deadlock, porém milhões de verificações inúteis em loop queimando múltiplos núcleos de CPU. |

---

## Relatório Final com Contadores do Problema

Ao final de cada execução, a classe [Models/RelatorioSimulacao.cs](Models/RelatorioSimulacao.cs) exibe:
- **Métricas gerais:** tempo de relógio, tempo de CPU consumido (`TotalProcessorTime`), pedidos criados, finalizados, recusados, não atendidos, vendas/faturamento esperado vs. registrado no Caixa e estoque final.
- **Contadores de problemas de concorrência** (com indicação `[OK]` ou `[PROBLEMA]`):
  - Race Condition (pedidos duplicados)
  - Pedidos perdidos no balcão
  - Estouros de capacidade do balcão
  - Exceções em coleção compartilhada
  - Estoque negativo (*check-then-act*)
  - Lost Update no Caixa (e diferença em R\$)
  - Deadlock (threads em espera circular, bloqueios em cascata e pedidos travados)
  - Espera ocupada (*busy-waiting* — total de iterações desperdiçadas e CPU queimada)

---

## Estrutura do Projeto

```text
.
├── Program.cs                        # Menu interativo no console (e suporte a CLI)
├── RestauranteConcorrente.csproj     # Projeto .NET 10
├── Logging/
│   └── EventLogger.cs                # Logger com horário (ms), ID da thread e autor
├── Models/
│   ├── Prato.cs                      # Modelo de prato (nome, preço, ingredientes, tempos)
│   ├── Cardapio.cs                   # Cardápio com seleção aleatória (Random.Shared)
│   ├── Pedido.cs                     # Comanda do pedido e status
│   ├── Resultados.cs                 # Contadores locais retornados por cada Task
│   └── RelatorioSimulacao.cs         # Relatório final unificado com contadores dos problemas
├── Resources/
│   ├── BalcaoPedidos.cs              # Balcão (versão sem controle e versão com lock)
│   ├── Estoque.cs                    # Estoque (versão sem controle e versão com lock)
│   ├── Caixa.cs                      # Caixa (versão sem controle e versão com lock)
│   └── UtensiliosCozinha.cs          # Tábua e Faca (ordem invertida vs. ordem fixa)
├── Participants/
│   ├── Garcom.cs                     # Participante produtor (Task) da Ingênua 1
│   └── Cozinheiro.cs                 # Participante consumidor (Task) da Ingênua 1
└── Solutions/
    ├── ISolucaoSimulacao.cs          # Interface comum das soluções
    ├── SolucaoIngenua1.cs            # Ingênua 1: Race Condition / Lost Update / Estoque Negativo
    ├── SolucaoIngenua2.cs            # Ingênua 2: Deadlock nos Utensílios
    └── SolucaoIngenua3.cs            # Ingênua 3: Espera Ocupada (Busy-Waiting)
```

---

## Como Executar

Pré-requisito: **.NET SDK 10.0** (ou compatível).

```bash
# Abre o menu interativo no console
dotnet run

# Ou executa diretamente uma solução específica (1, 2 ou 3)
dotnet run -- 1
dotnet run -- 2
dotnet run -- 3
```
