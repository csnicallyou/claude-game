---
name: game-perception
description: Use when implementing or modifying the Dual-Reality system (Law 4) — character worldviews, perception layer, mythology/animism/religion mechanics, event interpretation, or anything in `core/perception/*` and `eras/*/worldviews/*`. Also use when adding a new era to design that era's worldview evolution.
---

# game-perception

Закон 4 архитектуры. Над объективной симуляцией (климат, биология, экономика) лежит **слой восприятия**: персонажи интерпретируют события через когнитивно-культурную линзу своей эпохи и культуры.

Это **уникальный hook** Epochs of Humanity — ни в CK, ни в EU, ни в SoS такого нет. Засуха случается по объективным причинам (атмосферная аномалия), но для палеолитического вождя это «дух реки разгневан», и он принимает решения через эту линзу.

## Архитектурный инвариант

```
[ Симуляция ] — объективные факты
       ↓
[ Событийная шина ] — публикация IObjectiveEvent'ов
       ↓
[ Слой восприятия ] — конвертация в PerceivedEvent по worldview персонажа
       ↓
[ Решения персонажа ] — на основе PerceivedEvent, не оригинала
```

Симуляция **не знает**, что в голове у персонажей. Слой восприятия читает срез симуляции и накладывает свою линзу. **Эти два API не смешиваются.**

## Базовые типы

```csharp
// core/events/IObjectiveEvent.cs
public interface IObjectiveEvent
{
    string Id { get; }
    long Tick { get; }
}

// sim/health/DroughtBeganEvent.cs
public sealed record DroughtBeganEvent(
    string Id, long Tick,
    Entity Region, double SeverityIndex) : IObjectiveEvent;

// core/perception/PerceivedEvent.cs
public sealed record PerceivedEvent(
    string ObjectiveEventId,       // ссылка на оригинал
    string PerceivedCauseId,       // как персонаж это объясняет
    EmotionalResponse Emotion,     // что чувствует
    ActionSuggestion[] Suggestions,// какие действия персонаж считает уместными
    double Confidence);            // насколько убеждён в своей интерпретации

public enum EmotionalResponse { Calm, Worried, Fearful, Angry, Hopeful, Reverent, Awed }

public sealed record ActionSuggestion(
    string ActionKey,
    string LocalizationKey,
    double Weight);
```

## IWorldview API

Worldview — это интерпретатор. Каждая эпоха может иметь несколько (например, в палеолите: общий анимизм + варианты по культурам).

```csharp
// core/perception/IWorldview.cs
public interface IWorldview
{
    string Id { get; }                          // "paleolithic-animism"
    EraId ApplicableEra { get; }
    int Priority { get; }                       // если у культуры несколько — берём с большим Priority

    /// <summary>
    /// Превращает объективное событие в субъективное восприятие конкретного персонажа.
    /// Чистая функция: не мутирует state, не делает IO.
    /// </summary>
    PerceivedEvent Interpret(
        IObjectiveEvent objectiveEvent,
        ICharacterContext character);
}

public interface ICharacterContext
{
    Entity Id { get; }
    int Age { get; }
    string CultureId { get; }
    string WorldviewId { get; }      // активная линза этого персонажа
    IReadOnlyList<string> Traits { get; } // черты характера: смелый, набожный, скептик
    Personality Personality { get; }
    Relationships Relationships { get; }
}
```

## Палеолитическое мировоззрение (MVP)

`eras/paleolithic/worldviews/PaleolithicAnimismWorldview.cs`:

```csharp
public sealed class PaleolithicAnimismWorldview : IWorldview
{
    public string Id => "paleolithic-animism";
    public EraId ApplicableEra => new("paleolithic");
    public int Priority => 0;

    public PerceivedEvent Interpret(IObjectiveEvent ev, ICharacterContext ch)
    {
        return ev switch
        {
            DroughtBeganEvent drought => InterpretDrought(drought, ch),
            HuntFailedEvent hunt => InterpretFailedHunt(hunt, ch),
            DeathEvent death => InterpretDeath(death, ch),
            // ... все объективные события, релевантные палеолиту
            _ => DefaultInterpretation(ev, ch)
        };
    }

    private PerceivedEvent InterpretDrought(DroughtBeganEvent drought, ICharacterContext ch)
    {
        // Анимист видит засуху как недовольство духа реки/неба
        var cause = ch.HasTrait("skeptic") ? "natural-dryness" : "river-spirit-angry";
        var emotion = drought.SeverityIndex > 0.7 ? EmotionalResponse.Fearful : EmotionalResponse.Worried;

        var suggestions = new List<ActionSuggestion>();
        if (cause == "river-spirit-angry")
        {
            suggestions.Add(new("perform-river-ritual", "ritual.river.title", 1.0));
            suggestions.Add(new("consult-shaman", "shaman.consult.title", 0.7));
        }
        suggestions.Add(new("migrate-to-water", "migrate.water.title", 0.5));

        return new PerceivedEvent(
            drought.Id, cause, emotion, suggestions.ToArray(),
            Confidence: ch.HasTrait("devout") ? 0.9 : 0.6);
    }
}
```

## Механики палеолита через worldview

| Механика | Объективный слой | Слой восприятия |
|---|---|---|
| **Анимизм** | Нет духов в симуляции | Каждая природная сущность (река, гора, лес, ключевое животное) имеет «дух» в восприятии. Поведение объектов интерпретируется как воля духа. |
| **Шаманизм** | Шаман — персонаж с высокими `Intuition` + `Empathy`, наблюдает за окружением | В восприятии — «способен общаться с духами через транс». Геймплейно: шаман может проводить ритуал → меняет worldview-state у племени (снижает стресс), а ещё иногда (RNG-based, но обоснованный наблюдательностью) даёт реально полезный совет. |
| **Тотемизм** | Клан помечен ассоциацией с животным/растением | В восприятии — табу на охоту на тотем; нарушение → стресс, потеря loyalty. |
| **Культ предков** | Умершие персонажи остаются в `Dynasty.Ancestors[]` | Старейшины «передают волю предков» (по факту — компиляция решений тех же предков, взвешенная их `Wisdom`). |
| **Табу** | Просто правила в коде, нарушение → событие | В восприятии — «навлёк гнев духов». Через цепочку belief → стресс → плохие решения → реальные плохие последствия. **Самоисполняющееся пророчество — это и есть механика палеолитической религии.** |
| **Видения и сны** | RNG-based события | Информационные подсказки игроку (иногда правдивые, иногда нет), обернутые в нарративный wrapping. |

## Эволюция мировоззрений по эпохам

Когда добавляется новая эпоха-модуль — она регистрирует свой `IWorldview` через `IEraContentContext`. Эпохи переключают worldview у племени через события tech progression (например, открытие земледелия → культуры сдвигаются к `neolithic-fertility`).

| Эпоха | Worldview ID | Ключевые механики |
|---|---|---|
| Палеолит | `paleolithic-animism` | Анимизм, шаманизм, тотемизм, культ предков, табу |
| Мезолит | `mesolithic-ancestor-cult` | + сакральные места, культ предков-героев |
| Неолит | `neolithic-fertility` | + аграрные культы, богини-матери, мегалиты |
| Бронзовый век | `bronze-pantheon` | + пантеон, царь-жрец, кодифицированная мифология |
| Античность | `antiquity-statereligion` | + государственные религии, философия, скептицизм |
| Средневековье | `medieval-monotheism` | + монотеизм, теология, ереси, миссионерство |
| Новое время | `modern-secularism` | + наука vs религия, рационализм, секуляризация |
| Современность | `contemporary-ideology` | + идеологии (национализм, либерализм, etc.) |

**Принцип эволюции:** каждый следующий worldview **не отменяет** предыдущий полностью, а наращивает слои. Анимизм продолжает существовать у крестьян в Средневековье как фольклор, но не определяет принятия решений у элиты.

## Конфигурация в data/

Содержимое мифов, имён духов, ритуалов — в JSON-конфигах в `data/eras/<era-id>/worldviews/`:

```json
// data/eras/paleolithic/worldviews/animism-spirits.json
{
  "spirits": [
    { "id": "river-spirit", "domain": "river", "moodFactors": ["drought", "flood", "fish-abundance"] },
    { "id": "fire-spirit", "domain": "hearth", "moodFactors": ["fed", "neglected"] },
    { "id": "mammoth-ancestor-spirit", "domain": "hunt-mammoth", "moodFactors": ["respectful-hunt", "wasteful-hunt"] }
  ],
  "rituals": [
    { "id": "river-appeasement", "appliesTo": "river-spirit", "cost": { "food": 5 }, "effect": "perceived-spirit-calm" }
  ]
}
```

Это **моддабельно** — моддеры могут добавлять духов, ритуалы, табу (см. `game-modding`).

## Что НЕ делает слой восприятия

- **Не мутирует объективное состояние мира.** Дух не существует «реально». Только в `PerceivedEvent` и в стате персонажа.
- **Не блокирует «правильные» решения игрока.** Игрок-вождь может в любой момент сказать «к чёрту духов, копаем колодец» — будут социальные последствия (старейшины недовольны), но механика не запретит.
- **Не одинаков для всех.** Скептики, чужеземцы, шаманы — у каждого свой `confidence` в интерпретации.

## Тестирование

```csharp
[Fact]
public void Animism_DroughtInterpretedAsAngrySpirit_ByDevoutCharacter()
{
    var worldview = new PaleolithicAnimismWorldview();
    var character = TestCharacter.Devout();
    var drought = new DroughtBeganEvent("ev-1", 0, default, 0.8);

    var perceived = worldview.Interpret(drought, character);

    Assert.Equal("river-spirit-angry", perceived.PerceivedCauseId);
    Assert.Contains(perceived.Suggestions, s => s.ActionKey == "perform-river-ritual");
}
```

## Антипаттерны

- **Worldview напрямую меняет климат/экономику.** Нет. Мутация только через `ICommand` в sim-слой.
- **Один общий worldview на всех.** Нет, каждый персонаж имеет свой `WorldviewId`.
- **Игнорировать `Confidence`.** Низкая уверенность = персонаж склонен к альтернативным интерпретациям, это даёт нарратив.
- **Хардкод духов и ритуалов в C#.** Имена и описания — в `data/`, не в коде. Код знает только структуру.
- **«Магия как реальная сила» в палеолите.** Нет, мы делаем `plausible` (см. CLAUDE.md §3, блок 8.3). Эффект через belief → behavior → outcome, не через прямую магию.
