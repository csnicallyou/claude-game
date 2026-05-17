---
name: game-eras
description: Use when implementing or modifying an era module (`eras/paleolithic/*`, future `eras/mesolithic/*` etc.), designing era-to-era transitions, era content registration, era-specific mechanics, or anything that touches the `IEraModule` contract. Also use when planning a new era module post-v1.0.
---

# game-eras

Закон 3 архитектуры: эпоха = модуль. Ядро не знает о существовании конкретных эпох. Эпоха регистрируется через стабильный `IEraModule` API.

## Зачем

Игра растёт от палеолита к современности через модули. Если бы эпохи были «впаяны» в ядро, добавление каждой следующей требовало бы переписывать половину кода. Модули — это контракт.

## Контракт IEraModule

```csharp
// core/era/IEraModule.cs
public interface IEraModule
{
    string Id { get; }                       // "paleolithic", "mesolithic"...
    string DisplayNameKey { get; }           // ключ локализации, не строка
    TimeRange TimeRange { get; }             // в годах BP (Before Present)

    /// <summary>
    /// Вызывается один раз при создании нового мира, если эта эпоха активна на старте.
    /// </summary>
    void Init(IEraInitContext ctx);

    /// <summary>
    /// Вызывается каждый стратегический sim-тик. Чистая логика, только sim-API.
    /// </summary>
    void Tick(IEraTickContext ctx);

    /// <summary>
    /// Регистрация контента эпохи: технологии, события, биомы, worldview.
    /// Вызывается один раз после Init, до первого Tick.
    /// </summary>
    void Content(IEraContentContext ctx);

    /// <summary>
    /// При загрузке сейва — восстановление специфичного для эпохи состояния.
    /// Init НЕ вызывается на загрузке.
    /// </summary>
    void Rehydrate(IEraRehydrateContext ctx, EraSnapshot snapshot);
}
```

## Структура папки эпохи

```
eras/paleolithic/
├── PaleolithicModule.cs           # реализация IEraModule
├── content/                       # код регистрации контента
│   ├── PaleolithicTechTree.cs     # технологии: огонь, лук, погребение...
│   ├── PaleolithicEvents.cs       # CK3-стиль событий: видения, ритуалы
│   ├── PaleolithicBiomes.cs       # биомы Леванта 45k BCE
│   └── PaleolithicSpecies.cs      # Sapiens, Neanderthal, мегафауна
├── worldviews/                    # мировоззрения эпохи (см. game-perception)
│   ├── PaleolithicAnimismWorldview.cs
│   └── PaleolithicShamanismOverlay.cs
├── systems/                       # эпоха-специфичные sim-системы
│   ├── MammothExtinctionSystem.cs
│   └── SapiensNeanderthalEncounterSystem.cs
└── balancing/                     # балансовые JSON-конфиги
    ├── tech-costs.json
    └── event-weights.json
```

## Пример реализации

```csharp
// eras/paleolithic/PaleolithicModule.cs
public sealed class PaleolithicModule : IEraModule
{
    public string Id => "paleolithic";
    public string DisplayNameKey => "era.paleolithic.name";
    public TimeRange TimeRange => new(StartYearBP: 45_000, EndYearBP: 10_000);

    public void Init(IEraInitContext ctx)
    {
        // Стартовая геометрия: расставляем стартовые племена на Леванте
        PaleolithicStartConditions.Apply(ctx);
    }

    public void Content(IEraContentContext ctx)
    {
        PaleolithicTechTree.Register(ctx);
        PaleolithicEvents.Register(ctx);
        PaleolithicBiomes.Register(ctx);
        PaleolithicSpecies.Register(ctx);
        ctx.Worldviews.Register(new PaleolithicAnimismWorldview());
    }

    public void Tick(IEraTickContext ctx)
    {
        // Эпоха-специфичные системы. Общие (климат, demographics) — в sim/, не здесь.
        MammothExtinctionSystem.Tick(ctx);
        SapiensNeanderthalEncounterSystem.Tick(ctx);
    }

    public void Rehydrate(IEraRehydrateContext ctx, EraSnapshot snapshot)
    {
        // Восстановить эпоха-специфичные данные из snapshot
        if (snapshot.TryGet<MammothPopulationData>(out var mammoth))
            MammothExtinctionSystem.Restore(ctx, mammoth);
    }
}
```

## Контексты: что эпохе доступно, а что нет

```csharp
public interface IEraInitContext
{
    GameWorld World { get; }
    IContentRegistry Content { get; }
    IPrng Rng { get; }
    IGeographyLoader Geography { get; }
    // НЕТ: render, ui, commands, save
}

public interface IEraTickContext
{
    GameWorld World { get; }
    long Tick { get; }
    GameClock Clock { get; }
    IPrng Rng { get; }
    IObjectiveEventBus Events { get; }
    IContentRegistry Content { get; } // read-only здесь
    // НЕТ: render, ui
}

public interface IEraContentContext
{
    ITechRegistry TechTree { get; }
    IEventRegistry Events { get; }
    IBiomeRegistry Biomes { get; }
    ISpeciesRegistry Species { get; }
    IWorldviewRegistry Worldviews { get; }
    IDataLoader Data { get; }  // чтение из data/eras/<era-id>/
    // НЕТ: world (на этапе Content мира ещё нет, есть только контент)
}
```

**Принцип:** контекст даёт ровно то, что нужно. Передавать `GameWorld` целиком везде — антипаттерн, теряется контроль над тем, что эпоха может трогать.

## Регистрация эпох (composition root)

```csharp
// eras/Registry.cs (на границе render-слоя, может видеть все эпохи)
public static class EraRegistry
{
    public static IEraModule[] All() => new IEraModule[]
    {
        new PaleolithicModule(),
        // new MesolithicModule(),     // когда появится
        // new NeolithicModule(),
    };

    public static IEraModule? Find(string id)
        => All().FirstOrDefault(e => e.Id == id);
}
```

Это единственное место, где «все эпохи знают друг друга». Ядро (`core/`) и общая симуляция (`sim/`) не импортируют `EraRegistry`.

## Эпоха-к-эпохе переход

Эпохи **не наследуются** друг от друга. Переход — это явное событие в sim:

1. Какая-то технология открывается (например, «оседлое земледелие»).
2. Sim публикует `EraThresholdReachedEvent("neolithic-revolution", regionId)`.
3. Игрок получает событие в стиле CK3: «Твой народ научился возделывать пшеницу. Хочешь основать первую деревню?».
4. При согласии — активируется `MesolithicModule` параллельно с `PaleolithicModule` для конкретного региона (а не глобально).

**Эпохи могут сосуществовать в разных регионах.** В одной части карты — палеолит, в другой — неолит. Это и есть реальная история.

## Балансировка эпохи

Магические числа эпохи — **только в JSON-конфигах** в `data/eras/<era-id>/` или `eras/<era-id>/balancing/`. Никаких хардкод-чисел в C#.

```csharp
// ❌ ПЛОХО
const double MammothExtinctionThreshold = 0.15;

// ✓ ХОРОШО
var threshold = ctx.Content.Data.Load<MammothBalance>("mammoth-balance.json").ExtinctionThreshold;
```

## Технологии эпохи

```csharp
// eras/paleolithic/content/PaleolithicTechTree.cs
public static class PaleolithicTechTree
{
    public static void Register(IEraContentContext ctx)
    {
        ctx.TechTree.Register(new Tech(
            Id: "fire-control",
            NameKey: "tech.fire-control.name",
            DescriptionKey: "tech.fire-control.desc",
            Prerequisites: Array.Empty<string>(),
            ResearchCost: new TechCost(KnowledgePoints: 10, RequiredObservations: 0)));

        ctx.TechTree.Register(new Tech(
            Id: "spear",
            NameKey: "tech.spear.name",
            DescriptionKey: "tech.spear.desc",
            Prerequisites: new[] { "fire-control" },
            ResearchCost: new TechCost(KnowledgePoints: 25, RequiredObservations: 0)));

        // ... остальные технологии: bow, throwing-spear, sewing, burial,
        //     cave-art, dog-domestication, pottery, microblades...
    }
}
```

Список технологий палеолита (для содержимого `data/eras/paleolithic/technologies/`):

| Tech | Появляется в реальности | Game effect |
|---|---|---|
| `fire-control` | До 45k BCE (имеют все играбельные виды) | Огонь = +тепло, готовка, защита от хищников |
| `composite-spear` | ~45k BCE | +охота |
| `atlatl` (копьеметалка) | ~30k BCE | +дальняя охота, +шанс убить мегафауну |
| `bow-arrow` | ~30-25k BCE спорно, точно к 20k | +ranged, изменяет тактический бой |
| `needle-sewing` | ~30k BCE (Денисова, Сунгирь) | Одежда → выживание в холоде |
| `burial` | Уже у неандертальцев, но регулярно у Sapiens с ~30k | Социальный эффект, культ предков |
| `cave-art` | ~36k BCE (Шове), пик ~17k (Ласко) | Культурный престиж, мемная передача |
| `dog-domestication` | ~30k BCE спорно, точно к 15k | Помощь в охоте, защита |
| `microblades` | ~20k BCE | Эффективное использование камня |
| `pottery` | ~30k BCE (Дольни-Вестонице, фигурки), бытовая ~20k | Хранение, готовка в воде |

Точные годы — в `data/eras/paleolithic/technologies/*.json`, по реальным археологическим находкам.

## События эпохи (CK3-стиль)

```csharp
// eras/paleolithic/content/PaleolithicEvents.cs
public static class PaleolithicEvents
{
    public static void Register(IEraContentContext ctx)
    {
        ctx.Events.Register(new EventDefinition(
            Id: "shaman-vision-drought",
            Trigger: new EventTrigger.WhenObjective<DroughtBeganEvent>(severity: 0.5),
            Weight: ev => ev.SeverityIndex * 10,
            ChoicesKey: new[]
            {
                "event.shaman_vision_drought.choice.ritual",
                "event.shaman_vision_drought.choice.migrate",
                "event.shaman_vision_drought.choice.skeptic",
            },
            // ...
        ));
    }
}
```

## Что НЕ делать в эпохе

- **Импортировать другую эпоху.** Если механика нужна сразу нескольким — выноси в `sim/`.
- **`using Godot;`** — нарушение Закона 2.
- **`System.Random`/`DateTime.Now`** — нарушение Закона 1.
- **Прямую мутацию `GameWorld` снаружи переданного контекста.**
- **Перерегистрацию того же `Tech.Id` дважды** — `IContentRegistry` должен бросать исключение.
- **Хранение состояния эпохи в статических полях.** Всё через `EraSnapshot` и rehydrate, иначе не сериализуется.

## Антипаттерны

- **`if (era == "paleolithic")` в ядре.** Это нарушение Закона 3. Если ядру нужно знать про эпоху — значит, плохо спроектирован API.
- **«Базовый класс эпохи» с общим кодом.** Нет, у эпох нет общего абстрактного класса. Только интерфейс. Общий код — в `sim/`.
- **Прямое обращение к `eras/paleolithic/...` из `eras/mesolithic/...`.** Никогда. Если нужна continuity (например, технологии Палеолита доступны в Мезолите) — через общий `TechRegistry` в `sim/`.
