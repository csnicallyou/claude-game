---
name: game-loop
description: Use when implementing or modifying the main game loop, tick scheduler, time speed controls (pause/1x/2x/3x), fixed-timestep logic, or anything in `core/loop/*` and `core/time/*`. Also use for FPS issues, simulation lag, or "the game runs too fast/slow/jittery" bugs.
---

# game-loop

Главный цикл Epochs of Humanity. Фиксированный шаг симуляции + переменный рендер на Godot.

## Базовый принцип

Симуляция тикает на **фиксированном** шаге (`SIM_STEP_MS`). Godot вызывает `_Process` для рендера и `_PhysicsProcess` для физики, но **мы не используем `_PhysicsProcess` для нашей симуляции** — у нас своя сетка времени и две тиковые скорости (стратегическая + тактическая, Закон 1 + блок 4 интервью).

Один autoloaded Godot Node — `GameLoop` — управляет тиками. Сами sim-системы — engine-agnostic C# и не наследуются от `Node`.

```csharp
// render/loop/GameLoopNode.cs (это единственная связь с Godot)
using Godot;

public partial class GameLoopNode : Node
{
    private GameLoop _loop = null!; // инициализируется в _Ready

    public override void _Ready()
    {
        var seed = OS.GetUniqueId() + DateTime.UtcNow.Ticks; // только для новой игры
        _loop = new GameLoop(new GameWorld(seed.ToString()), SystemSchedule.Default);
        _loop.Start();
    }

    public override void _Process(double delta)
    {
        // delta — real-time секунды от Godot
        _loop.Frame(delta);
    }
}
```

```csharp
// core/loop/GameLoop.cs (engine-agnostic)
public sealed class GameLoop
{
    private const double StrategicStepSeconds = 0.1;  // 100ms = 10 тиков/сек на 1×
    private const double TacticalStepSeconds  = 0.05; // 50ms  = 20 тиков/сек на 1×
    private const double MaxRealDtSeconds = 0.25;     // cap против фриза

    private double _strategicAccumulator;
    private double _tacticalAccumulator;
    private GameSpeed _speed = GameSpeed.Normal; // Paused/Normal/Fast/Faster
    private ActiveLayer _activeLayer = ActiveLayer.Strategic; // тактический включается при зуме

    public void Frame(double realDtSeconds)
    {
        var dt = Math.Min(realDtSeconds, MaxRealDtSeconds);
        if (_speed == GameSpeed.Paused) return;

        var scaled = dt * GetSpeedMultiplier(_speed);

        // стратегический тик идёт всегда
        _strategicAccumulator += scaled;
        while (_strategicAccumulator >= StrategicStepSeconds)
        {
            TickStrategic();
            _strategicAccumulator -= StrategicStepSeconds;
        }

        // тактический тик — только если игрок смотрит в поселение
        if (_activeLayer == ActiveLayer.Tactical)
        {
            _tacticalAccumulator += scaled;
            while (_tacticalAccumulator >= TacticalStepSeconds)
            {
                TickTactical();
                _tacticalAccumulator -= TacticalStepSeconds;
            }
        }
    }

    private static double GetSpeedMultiplier(GameSpeed s) => s switch
    {
        GameSpeed.Paused => 0.0,
        GameSpeed.Normal => 1.0,
        GameSpeed.Fast   => 3.0,
        GameSpeed.Faster => 8.0,
        _ => throw new ArgumentOutOfRangeException()
    };

    private void TickStrategic() { /* выполняет стратегические системы */ }
    private void TickTactical() { /* выполняет тактические системы */ }
}
```

## Правила

- **Никогда** не вызывай sim-логику из `_Process` или `_PhysicsProcess` напрямую. Только через `GameLoop.Frame` → `TickStrategic`/`TickTactical`.
- **Cap на `dt`** (0.25 сек) обязателен. Иначе после возврата из фона/блокировки окна симуляция сделает тысячи тиков подряд.
- `GameSpeed.Paused` → пауза. Аккумулятор не растёт.
- Скорости — множители real-time. Не делаем `TickStrategic()` несколько раз через отдельную ветку — только аккумулятор.

## Интерполяция в рендере

`alpha = strategicAccumulator / strategicStep` (0..1) — позиция между предыдущим и текущим тиком. Юнит движущийся из A в B рендерится в `lerp(A, B, alpha)`.

Для этого компоненты с визуальной позицией хранят prev-значения:

```csharp
public record struct Position(double X, double Y, double PrevX, double PrevY);

// в конце тика, до мутации:
position.PrevX = position.X;
position.PrevY = position.Y;
// дальше логика, меняющая X/Y
```

Рендер читает обе пары и интерполирует:

```csharp
// render/characters/CharacterRenderer.cs
var visualX = MathHelper.Lerp(position.PrevX, position.X, alpha);
var visualY = MathHelper.Lerp(position.PrevY, position.Y, alpha);
sprite.Position = new Vector2((float)visualX, (float)visualY);
```

## Под капотом тика

`TickStrategic()` / `TickTactical()` инкрементируют соответствующий счётчик тиков и прогоняют системы в **фиксированном порядке** из `SystemSchedule`:

```csharp
// core/loop/SystemSchedule.cs
public static class SystemSchedule
{
    public static readonly Action<GameWorld>[] Strategic =
    {
        ClimateSystem.Tick,
        PopulationSystem.Tick,
        EconomySystem.Tick,
        DiplomacySystem.Tick,
        AgingSystem.Tick,
        // ... строго фиксированный порядок
    };

    public static readonly Action<GameWorld>[] Tactical =
    {
        TacticalMovementSystem.Tick,
        TacticalNeedsSystem.Tick,
        TacticalCombatSystem.Tick,
        // ...
    };
}
```

Порядок — часть детерминизма (Закон 1). **Не сортировать динамически. Не использовать reflection-based discovery систем.**

## Двухуровневые тики и время

Стратегический тик = 1 сезон игрового времени (грубо 3 месяца).
Тактический тик = 1 час игрового времени.

Соответствие real-time на 1× скорости:

- Стратегический: 100 ms = 1 сезон → 400 ms = 1 год → ~10 секунд = 25 лет.
- Тактический: 50 ms = 1 час → ~1.2 сек = 1 сутки.

Конвертация — отдельная утилита:

```csharp
// core/time/GameClock.cs
public sealed class GameClock
{
    public long StrategicTick { get; private set; }
    public long TacticalTick { get; private set; }
    public int CurrentYearBP { get; private set; } // 45000, 44999, ...
    public Season Season { get; private set; }     // Spring/Summer/Autumn/Winter

    public void AdvanceStrategic() { /* +1 season, может уменьшить YearBP */ }
    public void AdvanceTactical()  { /* +1 hour */ }
}
```

## Threading

В MVP **всё в одном потоке** (Godot main thread). Преждевременная многопоточность — источник non-determinism.

Когда подсистема упрётся в 5+ ms на тик — рассмотреть:

1. SIMD через `System.Numerics` (без многопоточности, детерминированно).
2. Batch-обработку через Arch ECS bulk operations.
3. `Parallel.ForEach` — **только** для CPU-bound батчей без межсущностных зависимостей и с явной сортировкой результата для детерминизма.

## Диагностика

- Замедление симуляции → измерь системы через `Stopwatch` **снаружи** игровой логики (можно, это не sim). Логируй p50/p95 в dev-режиме.
- «Игра дёргается» → проверь интерполяцию: рендер должен использовать `alpha`.
- «После переключения окна игра убегает вперёд» → проверь cap на `dt`.
- «Сейв сделан в момент X, загружен — симуляция идёт чуть иначе» → ищи нарушение детерминизма (см. `game-determinism`).

## Скорости — UX

Игрок переключает скорость через клавиши:

- `Space` — пауза/возобновление
- `1` / `2` / `3` — Normal / Fast / Faster

При паузе sim замирает, но UI и рендер продолжают (можно крутить камеру, тыкать в панели). Это стандартный паттерн Paradox.

## Антипаттерны

- `Timer.SetTimeout` / `await Task.Delay` в sim-логике — нет.
- Изменение `SIM_STEP_MS` для смены скорости — нет, тик стабилен. Только множитель real-time.
- Симуляция внутри `_PhysicsProcess` — нет, мы не используем Godot физический шаг.
- «Сделаю тик через сигнал Godot» — нет, сигналы не детерминированы по порядку доставки в общем случае.
- Реестр систем через рефлексию (`Assembly.GetTypes()...`) — нет, порядок не детерминирован.
