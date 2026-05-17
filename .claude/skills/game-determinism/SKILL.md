---
name: game-determinism
description: Use when implementing PRNG/random logic, debugging save desyncs or replay mismatches, reviewing code in `core/sim/eras/*` for nondeterminism, or when adding any subsystem that involves randomness, time, iteration order, or floating-point math in simulation. Also use proactively when reviewing any PR that touches simulation code.
---

# game-determinism

Закон 1 архитектуры. Симуляция должна давать **одинаковый результат при одних и тех же входах + сиде** на любом железе, в любой ОС, в любой версии .NET, навсегда.

## Зачем

- **Сейвы.** Загруженное состояние = продолжение того же мира, а не отдельной реальности.
- **Replay.** Воспроизведение игры по логу команд (для отладки и шейринга).
- **Multiplayer (long-term).** Lockstep-синхронизация: клиенты прогоняют одну и ту же симуляцию, синхронизируются только команды.
- **Регрессионные тесты.** Фиксированный сид → детерминированный результат → проверка инвариантов.

Если детерминизм сломан — это **архитектурный bug**, исправляется до merge. Никаких «потом починим».

## Сидированный PRNG

Используем кастомный `Rng`, не `System.Random`.

```csharp
// core/prng/Rng.cs
public sealed class Rng
{
    private ulong _state;

    public Rng(string seed) => _state = SplitMix64(HashSeed(seed));
    public Rng(ulong state) => _state = state;

    // xoshiro256** или SplitMix64 — выбираем стабильный, документированный
    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public double NextDouble()
        => (NextUInt64() >> 11) * (1.0 / (1UL << 53)); // 53-bit mantissa

    public int NextInt(int minInclusive, int maxExclusive)
    {
        var range = (ulong)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextUInt64() % range);
    }

    /// <summary>
    /// Создаёт дочерний PRNG, детерминированно отличающийся от родителя.
    /// Изменения в одной подсистеме не должны двигать случайности в другой.
    /// </summary>
    public Rng Fork(string subsystemName)
    {
        var hash = HashSeed(subsystemName);
        return new Rng(_state ^ hash);
    }

    private static ulong HashSeed(string s) { /* стабильный хеш, не string.GetHashCode! */ }
    private static ulong SplitMix64(ulong x) { /* ... */ }
}
```

**Критично:** `string.GetHashCode()` в .NET **рандомизирован между запусками** для защиты от hash flooding. Использовать **нельзя**. Пишем свой стабильный хеш (FNV-1a, MurmurHash3 или собственный SplitMix-вариант).

## Правило форка

Каждая подсистема симуляции получает свой PRNG через `rng.Fork(name)`:

```csharp
// при инициализации мира
var world = new GameWorld(seed);
world.RegisterSubsystemRng("climate",   world.Rng.Fork("climate"));
world.RegisterSubsystemRng("ai",        world.Rng.Fork("ai"));
world.RegisterSubsystemRng("characters",world.Rng.Fork("characters"));
// ...
```

Это позволяет:

- Изменять логику в `climate` без сдвига случайностей в `ai` (старые сейвы остаются стабильными).
- Параллелить независимые подсистемы без рисков общего состояния PRNG.

## Запреты в `core/`, `sim/`, `eras/`

| Запрещено | Почему | Чем заменить |
|---|---|---|
| `System.Random` | Не сидируется напрямую, реализация может меняться между .NET-версиями | `Rng` (наш) |
| `Random.Shared` | Глобальное состояние, не сериализуется | `world.Rng.Fork(...)` |
| `Godot.GD.Randf()` | Глобальный движковый PRNG | `Rng` (наш) |
| `Godot.RandomNumberGenerator` | Без явного сидирования | `Rng` (наш) |
| `DateTime.Now`, `DateTime.UtcNow` | Зависит от реального времени | `world.Clock.YearBP`, `world.Tick` |
| `Environment.TickCount`, `Stopwatch.GetTimestamp` | Wall-clock | то же |
| `string.GetHashCode()` | Рандомизирован между запусками | свой `StableHash` |
| `Dictionary<K,V>` insertion-order iteration | Стабилен в .NET, но любой рефакторинг порядка вставки ломает старые сейвы | Сортировка ключей перед итерацией |
| `HashSet<T>` iteration | Та же проблема | `SortedSet<T>` или сортировка |
| `LINQ` `.OrderBy` без явного `StringComparer.Ordinal` | Зависит от культуры | `.OrderBy(x => x, StringComparer.Ordinal)` |
| `float` в sim-логике | Точность зависит от инструкций процессора | `double` (см. ниже про fixed-point на будущее) |
| `Math.Sin/Cos/Sqrt` на разных платформах | В теории детерминированы в .NET, но не гарантировано на ARM vs x64 | `System.MathF`/`Math` — приемлемо в MVP, fixed-point позже если нужно |

## Floating-point: текущая политика

**В MVP — `double` с осознанием рисков.** В .NET 8 операции `+, -, *, /, sqrt` IEEE-754 детерминированы между x64 и ARM64 при использовании `System.Math`. Трансцендентные функции (`Sin`, `Cos`, `Exp`, `Log`) — теоретически да, но не на 100%. Для MVP-singleplayer и сейвов это приемлемо.

**Для будущего lockstep multiplayer** — пересмотр:

- Либо переходим на fixed-point для критичных подсистем (бой, наследование, голод).
- Либо ограничиваем целевые архитектуры (только x86-64) — выкидывает ARM (Apple Silicon, мобильные).

Решение откладываем до v0.5-v0.6. Записать в `docs/decisions/` когда придёт время.

## Iteration order

Это самый частый источник тонких bugs.

```csharp
// ❌ ПЛОХО — порядок зависит от внутренностей Dictionary
foreach (var (id, pop) in world.Pops)
{
    SimulatePop(pop);
}

// ✓ ХОРОШО — явная сортировка
foreach (var (id, pop) in world.Pops.OrderBy(kv => kv.Key))
{
    SimulatePop(pop);
}

// ✓ ЕЩЁ ЛУЧШЕ — для горячего кода: хранить в массиве, отсортированном по ID
foreach (var pop in world.PopsArray) // сортированный массив, поддерживаемый в инвариант
{
    SimulatePop(pop);
}
```

Arch ECS гарантирует фиксированный порядок query (по архетипу и индексу). Это устойчиво — но **только если не перетасовываются архетипы** между версиями. Безопасный паттерн — сортировать entity-id перед обработкой в критичных местах.

## Команды и порядок

`ICommand`'ы из UI/AI кладутся в очередь. В начале тика — сортируем по `(IssuedAtTick, CommandTypeOrder, SerializedHash)` и исполняем по порядку. Это даёт детерминированную обработку независимо от того, когда команда фактически пришла.

```csharp
// core/loop/CommandQueue.cs
public void ExecuteAtTick(GameWorld world, long tick)
{
    var batch = _pending
        .Where(c => c.IssuedAtTick == tick)
        .OrderBy(c => c.IssuedAtTick)
        .ThenBy(c => CommandTypeRegistry.Order(c.GetType()))
        .ThenBy(c => CommandHasher.Hash(c)) // tie-breaker
        .ToList();

    foreach (var cmd in batch)
    {
        if (cmd.Validate(world).Ok)
            cmd.Execute(world);
    }
}
```

## Тестирование детерминизма

Каждая sim-подсистема имеет тест:

```csharp
[Fact]
public void Climate_Deterministic_AcrossRuns()
{
    var seed = "test-seed-001";
    var world1 = new GameWorld(seed);
    var world2 = new GameWorld(seed);

    for (int i = 0; i < 1000; i++)
    {
        ClimateSystem.Tick(world1);
        ClimateSystem.Tick(world2);
    }

    Assert.Equal(WorldHasher.Hash(world1), WorldHasher.Hash(world2));
}
```

`WorldHasher.Hash` — детерминированный хеш всего состояния мира (для сравнения). Реализуется через сериализацию в детерминированный JSON + SHA256.

## Smoke-тест перед коммитом

В CI запускаем «long-run» тест на каждый PR: 50 000 тиков с фиксированным сидом → хеш состояния должен совпадать с эталоном. Если не совпадает — где-то прокрался non-determinism.

## Диагностика

«Сейв сохранил → загрузил → симуляция расходится» = детерминизм сломан. Шаги:

1. Сохрани сейв в момент X. Останови. Загрузи. Тикни 100 раз.
2. Параллельно — продолжи оригинал на 100 тиков.
3. Сравни хеши обоих состояний.
4. Если разные — bisect: какая подсистема первой даёт расхождение?
5. Источник: обычно `Dictionary`-iter, `string.GetHashCode`, `DateTime.Now`, или необёрнутый `Random`.

## Антипаттерны

- **«Здесь же неважно, какой порядок — список маленький».** Сегодня маленький, через год — большой, и сейв из v0.3 не загрузится в v0.4.
- **`Math.Random()` ради простоты в тестах.** В тестах — тоже `Rng` с фиксированным сидом.
- **«Запишем seed в начале, дальше уже всё детерминировано».** Нет, если код использует глобальный non-det источник.
- **Использование `Guid.NewGuid()` для генерации ID сущностей в sim.** Используй `world.NextEntityId()` (счётчик).
- **Параллельная итерация без сортировки результата.** `.AsParallel().Select(...)` без `.OrderBy` — недетерминированный порядок.
