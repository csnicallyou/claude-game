---
name: game-architecture
description: Use when designing or modifying core systems of Epochs of Humanity — ECS structure, IEraModule API, cross-layer contracts (sim/render/ui/eras), folder organization, top-level architectural decisions, or anything in `core/*`. Also use when adding a new era module or wiring a cross-cutting subsystem.
---

# game-architecture

Каркас гранд-стратегии Epochs of Humanity на Godot 4 + C#. Четыре закона, слои, ECS, EraModule API.

## Четыре закона (canonical, см. CLAUDE.md §4)

1. **Детерминизм.** Те же входы + сид = тот же результат, всегда.
2. **Разделение sim/render.** Симуляция не знает про Godot. Рендер читает срез через `WorldView`.
3. **Эпоха = модуль.** Ядро не импортирует эпохи.
4. **Dual-Reality.** Симуляция объективна; восприятие персонажей — отдельный слой.

Нарушение любого = архитектурный bug, исправляется до merge.

## Слои и направления зависимости

```
core/     ← никто из проекта не импортирует, но core/ ничего из проекта не импортирует тоже
sim/      ← зависит только от core/
render/   ← зависит от core/ и sim/, читает через WorldView (read-only)
ui/       ← зависит от render/ и core/, шлёт Commands
eras/X/   ← зависит от core/ и sim/, регистрируется через IEraModule
data/     ← только данные, никакого кода
```

**Запрещённые импорты:**

- `core → sim/render/ui/eras` — никогда.
- `sim → render/ui/eras` — никогда.
- `eras/A → eras/B` — эпохи не знают друг о друге.
- `using Godot;` в `core/`, `sim/`, `eras/` — никогда (Закон 2).

Проверять можно через `dotnet list package --include-transitive` + статический анализатор (ArchUnitNET — рассмотреть в v0.3+).

## ECS

Используем библиотеку **Arch** (NuGet `Arch`). Это data-oriented ECS с хорошей производительностью для большого числа сущностей.

```csharp
// core/ecs/World.cs
using Arch.Core;

public sealed class GameWorld
{
    private readonly Arch.Core.World _arch;
    public long Tick { get; private set; }
    public Rng Rng { get; }

    public GameWorld(string seed)
    {
        _arch = Arch.Core.World.Create();
        Rng = new Rng(seed);
    }

    public Entity CreateEntity() => _arch.Create();
    public void Destroy(Entity e) => _arch.Destroy(e);
    public ref T Get<T>(Entity e) => ref _arch.Get<T>(e);
    // ... query API через arch
}
```

**Правила компонентов:**

- Компоненты — `struct` или `record struct`, плоские данные, никаких методов.
- Никаких ссылок на Godot.Node, Godot.Resource, Texture — это рендер-слой.
- Никаких `Action`/`Func`/`delegate` в компонентах — не сериализуются.

```csharp
// core/components/Position.cs
public record struct Position(double X, double Y, double PrevX, double PrevY);

// core/components/Age.cs
public record struct Age(int Years);

// sim/characters/components/Chief.cs
public record struct Chief(Entity DynastyRoot);
```

**Системы — чистые статические методы:**

```csharp
// sim/aging/AgingSystem.cs
public static class AgingSystem
{
    public static void Tick(GameWorld world, double dt)
    {
        var query = new QueryDescription().WithAll<Age>();
        world.Query(query, (ref Age age) =>
        {
            // ... логика, читает компоненты, пишет компоненты
        });
    }
}
```

Системы регистрируются в фиксированном порядке (см. `game-loop`). Порядок — часть детерминизма (Закон 1).

## EraModule API

```csharp
// core/era/IEraModule.cs
public interface IEraModule
{
    string Id { get; }
    string DisplayNameKey { get; } // ключ локализации, не строка
    TimeRange TimeRange { get; }

    void Init(IEraInitContext ctx);          // при старте новой игры
    void Tick(IEraTickContext ctx);          // каждый sim-тик
    void Content(IEraContentContext ctx);    // регистрация контента
    void Rehydrate(IEraRehydrateContext ctx, EraSnapshot snapshot); // при загрузке
}

// core/era/TimeRange.cs
public record struct TimeRange(int StartYearBP, int EndYearBP);
// BP = Before Present (стандарт археологии)
```

**Контексты дают эпохе ограниченный доступ:**

```csharp
public interface IEraInitContext
{
    GameWorld World { get; }
    IContentRegistry Content { get; }
    IPrng Rng { get; } // дочерний PRNG, форкнутый от мирового
    // НЕ даём: рендер, UI, команды
}

public interface IEraTickContext
{
    GameWorld World { get; }
    long Tick { get; }
    IPrng Rng { get; }
    IEventBus Events { get; } // публикация объективных событий
}
```

**Запрет:** передавать `GameWorld` напрямую без контекста. Контекст — стабильный API, мир — нет.

## Команды (Command Pattern)

UI и игрок не мутируют состояние напрямую. Любое действие — `ICommand`:

```csharp
// core/commands/ICommand.cs
public interface ICommand
{
    long IssuedAtTick { get; }
    CommandValidation Validate(GameWorld world);
    void Execute(GameWorld world);
}

// sim/characters/commands/ArrangeMarriageCommand.cs
public sealed record ArrangeMarriageCommand(
    Entity Initiator,
    Entity Target,
    long IssuedAtTick) : ICommand
{
    public CommandValidation Validate(GameWorld world) { /* ... */ }
    public void Execute(GameWorld world) { /* ... */ }
}
```

Команды кладутся в очередь, исполняются в начале следующего тика в детерминированном порядке (сортировка по `IssuedAtTick`, затем по типу команды, затем по полям). **Это даёт основу для будущего lockstep multiplayer.**

## События (Event Bus)

Симуляция публикует **объективные** события (`IObjectiveEvent`). Слой восприятия (Закон 4) превращает их в **воспринимаемые** (`PerceivedEvent`) для каждого персонажа в зависимости от его worldview.

```csharp
// core/events/IObjectiveEvent.cs
public interface IObjectiveEvent
{
    string Id { get; }
    long Tick { get; }
}

// sim/health/events/DroughtBeganEvent.cs
public sealed record DroughtBeganEvent(
    string Id, long Tick,
    Entity RegionId, double SeverityIndex) : IObjectiveEvent;
```

Подписки на события — типизированные, без `string`-эвентов. Используем discriminated unions (через sealed records + pattern matching).

## Производительность — потолки MVP

- 60 FPS рендер.
- 10 тиков/сек симуляции на 1× скорости.
- Активные индивиды в тактическом слое: ≤ 500.
- Pops в стратегическом слое: ≤ ~10 000.
- Если система тикает > 5 ms — выноси в `System.Threading.Tasks` или агрегируй.

## Чек-лист при добавлении подсистемы в `sim/`

1. Данные — компоненты-структуры в `sim/<domain>/components/`.
2. Логика — статическая система в `sim/<domain>/<Domain>System.cs`.
3. PRNG — через `world.Rng.Fork("subsystem-name")`.
4. Регистрация в фиксированном порядке в `core/loop/SystemSchedule.cs`.
5. Юнит-тест: фиксированный сид → детерминированный результат.
6. Если читает контент эпохи — через `IEraContentContext`, не напрямую.

## Чек-лист при добавлении эпохи

1. Папка `eras/<era-id>/`, класс `<Era>Module : IEraModule`.
2. Регистрация в `eras/Registry.cs` (composition root).
3. Никаких импортов из других эпох.
4. Контент — в `data/eras/<era-id>/*.json` + `eras/<era-id>/content/Loader.cs`.
5. Worldview эпохи — в `eras/<era-id>/worldviews/` (см. `game-perception`).
6. Если эпоха хочет механику, которая может пригодиться будущим — выносим в `sim/` отдельным PR до добавления эпохи.

## Антипаттерны

- **Глобальный синглтон** `GameInstance.Current` — нет. Передаём `GameWorld` явно.
- **Эвент-шина по строкам** — нет, типизированные `IObjectiveEvent`-наследники.
- **Прямая мутация мира из UI** — нет, только через `ICommand`.
- **`using Godot;` в `core/sim/eras/*`** — нарушение Закона 2.
- **`System.Random`/`DateTime.Now` в симуляции** — нарушение Закона 1.
- **Класс с методами как компонент** — компонент только структура.
- **«Мини-движки» внутри эпох** (свой game loop, свой PRNG) — эпоха использует ресурсы ядра.
