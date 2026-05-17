---
name: game-modding
description: Use when implementing the data-driven content layer, JSON config schema, mod loader, content registries, or anything in `data/`, `core/data-loader/`. Also use when adding a new content type that should be moddable (technologies, events, biomes, names, species, worldview content).
---

# game-modding

Моддинг с первого дня — но только через **JSON-конфиги**, не через код. Code-моды (DLL/Lua) — после v1.0. Это согласуется с CLAUDE.md §3 (блок 7.3).

## Принцип

**Вся игровая логика читает из `data/`.** Биомы, технологии, события, имена, тексты — всё в JSON. Код знает только структуру (схему), не конкретные значения.

Моддер кладёт свой JSON-файл в свою папку, игра при запуске «накатывает» моды поверх ванильных конфигов.

## Что моддабельно (MVP)

| Контент | Папка | Формат |
|---|---|---|
| Биомы | `data/biomes/*.json` | Биом: имя, цвет, продуктивность, климат |
| Виды (sapiens, neanderthal, мегафауна) | `data/species/*.json` | Параметры популяции, поведения, диета |
| Культуры (стартовые) | `data/cultures/*.json` | Имя, происхождение, корпус имён, стартовые традиции |
| Технологии | `data/technologies/*.json` | Дерево tech-tree, стоимости, эффекты |
| События (CK3-стиль) | `data/events/*.json` | Триггер, веса, варианты выбора, последствия |
| Имена | `data/names/*.json` | Корпусы для генерации имён персонажей |
| Содержимое мифологии | `data/eras/<era-id>/worldviews/*.json` | Духи, ритуалы, табу |
| Локализация | `localization/<lang>.csv` | Текстовые строки по ключам |

Что **не моддабельно через JSON** (требует кода):

- Игровой цикл, тики, скорости.
- Новые типы событий (только новые экземпляры существующих типов).
- Новые worldview-классы (только содержимое существующих).
- Новые системы симуляции.

Эти требуют code-модов — после v1.0.

## Загрузка и порядок

```csharp
// core/data-loader/DataLoader.cs
public sealed class DataLoader
{
    /// <summary>
    /// Грузит все *.json из ванильной папки + всех включённых модов.
    /// Mod-файлы накатываются ПОВЕРХ ваниль по id (merge semantics).
    /// </summary>
    public T[] LoadAll<T>(string relativePath) where T : IDataEntry
    {
        var vanilla = LoadFrom<T>(VanillaRoot, relativePath);
        var modded = EnabledMods
            .OrderBy(m => m.LoadOrder)
            .SelectMany(m => LoadFrom<T>(m.Root, relativePath));

        return MergeById(vanilla, modded);
    }

    private T[] MergeById<T>(IEnumerable<T> vanilla, IEnumerable<T> modded)
        where T : IDataEntry
    {
        var dict = vanilla.ToDictionary(x => x.Id, StringComparer.Ordinal);
        foreach (var entry in modded)
            dict[entry.Id] = entry; // mod перезаписывает по id
        return dict.Values
            .OrderBy(x => x.Id, StringComparer.Ordinal) // детерминизм!
            .ToArray();
    }
}

public interface IDataEntry
{
    string Id { get; }
}
```

**Критично:** после merge сортируем по Id для детерминизма (см. `game-determinism`). Иначе порядок сущностей в мире будет зависеть от порядка загрузки модов.

## Структура мода

```
my-cool-mod/
├── mod.json                       # манифест
├── data/
│   ├── biomes/
│   │   └── extra-arctic.json     # новый биом
│   ├── technologies/
│   │   └── fishing-net.json      # новая технология
│   └── events/
│       └── solar-eclipse.json    # новое событие
└── localization/
    ├── ru.csv                     # переводы добавленных строк
    └── en.csv
```

```json
// my-cool-mod/mod.json
{
  "id": "extra-paleolithic-content",
  "name": "Extra Paleolithic Content",
  "version": "1.0.0",
  "gameVersionRange": ">=0.1.0 <2.0.0",
  "author": "ModderName",
  "dependencies": [],
  "loadOrder": 100
}
```

## JSON-схемы

Схемы — в `docs/modding/schemas/` (JSON Schema формат), генерируем из C# типов через NJsonSchema (NuGet) или вручную.

```json
// docs/modding/schemas/tech.schema.json (фрагмент)
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["id", "nameKey", "prerequisites", "cost"],
  "properties": {
    "id": { "type": "string", "pattern": "^[a-z][a-z0-9-]*$" },
    "nameKey": { "type": "string" },
    "prerequisites": { "type": "array", "items": { "type": "string" } },
    "cost": {
      "type": "object",
      "required": ["knowledgePoints"],
      "properties": {
        "knowledgePoints": { "type": "integer", "minimum": 0 },
        "requiredObservations": { "type": "integer", "minimum": 0 }
      }
    }
  }
}
```

Чем строже схема — тем меньше моддер может сделать «случайно неправильно», тем легче отлавливать ошибки.

## Валидация модов

При загрузке мода:

1. Парсим `mod.json`. Невалиден → отказ, лог.
2. Проверяем `gameVersionRange` — совместим ли с текущей версией.
3. Для каждого `data/*.json` — валидируем по JSON Schema.
4. Проверяем reference integrity: технология `bow` ссылается на `prerequisites: ["fire-control"]` — существует ли `fire-control`?
5. Если что-то не так — мод **не загружается**, игроку показывается ошибка с диагностикой. **Не пытайся восстановить с частично загруженным модом.**

## Локализация и моды

Моды могут добавлять строки в `localization/<lang>.csv`. Ключи именуются как `mod.<mod-id>.<key>` чтобы не пересекаться с ванилью:

```csv
# my-cool-mod/localization/ru.csv
mod.extra-paleolithic-content.tech.fishing-net.name,Рыболовная сеть
mod.extra-paleolithic-content.tech.fishing-net.desc,Позволяет ловить рыбу в стаях.
```

Игра при старте мерджит CSV всех включённых модов поверх ванильной CSV. Конфликты ключей → ошибка валидации.

## UI модов (в MVP — минимальный)

- В главном меню — пункт «Mods».
- Список найденных модов из `<user-data>/mods/` (или `<install-dir>/mods/`).
- Для каждого — включить/выключить + порядок загрузки.
- При запуске новой игры — фиксируем активные моды в сейв (для воспроизводимости).

Steam Workshop — после v0.7+.

## Что разработчик игры (мы) должны делать

- **Каждая новая фича** — спрашивать себя: «А это моддабельно?». Если да — описать через JSON-схему.
- **Никаких хардкод-значений** игровых чисел. Магические константы — в JSON.
- **Документировать схемы** в `docs/modding/`.
- **Тестировать минимальный мод** на каждом релизе: создать мод с одним добавленным биомом, убедиться, что игра его подхватывает.

## Что НЕ разрешено для моддеров (в MVP)

- Изменять цикл симуляции.
- Добавлять новые типы событий (только новые экземпляры существующих типов).
- Перезагружать систему боя/наследования.
- Изменять формат сейва.

Эти возможности появятся после v1.0 с code-моддингом.

## Антипаттерны

- **Хардкод значений в C#-коде.** `const double MaxPopulation = 500;` → нет. В JSON.
- **Циклы между файлами без явной топологической сортировки.** Дерево tech-tree должно быть DAG, валидируется при загрузке.
- **`mod-A` ссылается на `mod-B` без указания dependency.** Манифест должен явно декларировать.
- **«Слияние арбитражных JSON-полей».** Merge только по id. Глубокий merge внутри объектов — опционально и только для редких случаев (например, добавление варианта в `event.choices[]`).
- **Загрузка модов в недетерминированном порядке.** Сортируем по `loadOrder`, затем по `id`.
