---
name: game-state
description: Use when implementing or modifying save/load, serialization, save-format versioning, save migrations, or anything in `core/save/*`. Also use when changing the shape of simulation state (components, world structure) — that has direct impact on save compatibility.
---

# game-state

Сейвы, сериализация, версионирование, миграции. Стек: System.Text.Json + System.IO.Compression (gzip).

## Главное правило

**Если ты меняешь форму состояния — ты ломаешь старые сейвы.** Варианты:

1. До v0.5 — **ломаем свободно**. Тестеры пересоздают миры при каждом обновлении. Никаких миграций пока что не пишем (см. CLAUDE.md §3 и блок 7.1.1 интервью).
2. С v0.5 — пишем миграцию или bump major-версии формата.

Не «авось загрузится» — это создаст странные баги через час игры.

## Формат сейва

```csharp
// core/save/SaveFile.cs
public sealed record SaveFile(
    int FormatVersion,              // версия формата, инкрементим при изменении схемы
    string SaveId,                  // GUID
    string SavedAtIso,              // ISO-8601, метаданные (не игровое время)
    long StrategicTick,
    long TacticalTick,
    int YearBP,
    string Seed,                    // сид PRNG
    WorldSnapshot World,            // снапшот состояния
    string[] ActiveEraIds,          // ID активных эпох
    EraSnapshot[] EraSnapshots);    // данные эпох

public sealed record WorldSnapshot(
    EntitySnapshot[] Entities,
    Dictionary<string, JsonElement> Singletons); // глобальное состояние, не привязанное к entity

public sealed record EntitySnapshot(
    int Id,
    Dictionary<string, JsonElement> Components); // имя компонента → данные
```

`FormatVersion` инкрементируется при каждом изменении формы `WorldSnapshot` или связанных структур.

## Сериализация

```csharp
// core/save/SaveSerializer.cs
public static class SaveSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,         // компактно
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IncludeFields = false,         // только properties
        // Никаких converter'ов с локалью — strict invariant culture
    };

    public static byte[] Serialize(SaveFile save)
    {
        var json = JsonSerializer.Serialize(save, Options);
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            gz.Write(bytes, 0, bytes.Length);
        }
        return ms.ToArray();
    }

    public static SaveFile Deserialize(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gz, Encoding.UTF8);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<SaveFile>(json, Options)
            ?? throw new InvalidDataException("Save file empty");
    }
}
```

## Что можно сериализовать

- **Можно:** `record struct` компоненты, примитивы, `string`, `DateTime` (как ISO), массивы.
- **Можно:** Enum'ы (по числовому значению, не имени, для стабильности при переименовании).
- **Нельзя:** `Dictionary` без явной сортировки ключей — сериализация может быть не стабильна между .NET-рантаймами.
- **Нельзя:** `Func`/`Action`/`delegate` — не сериализуется в JSON.
- **Нельзя:** циклические ссылки между entities — используй `EntityId` (просто int), не объект.
- **Нельзя:** `Godot.Vector2`, `Godot.Color` — нарушение Закона 2. Используй `System.Numerics.Vector2` или свой `record struct`.

## Хранилище

Сейвы кладутся в user-папку Godot:

```csharp
// render/save/SaveStorage.cs (тут можно Godot, это render-слой)
using Godot;

public static class SaveStorage
{
    public static string SaveDir => Path.Combine(
        OS.GetUserDataDir(), "saves");

    public static void Write(string fileName, byte[] data)
    {
        Directory.CreateDirectory(SaveDir);
        File.WriteAllBytes(Path.Combine(SaveDir, fileName), data);
    }

    public static byte[] Read(string fileName)
        => File.ReadAllBytes(Path.Combine(SaveDir, fileName));
}
```

Файлы: `<saveId>.save` (бинарь, gzipped JSON), `<saveId>.meta.json` (метаданные для UI — превью, имя вождя, год, без полной загрузки).

Ротация автосейвов: храним последние 3, перезаписываем по кругу.

## Миграции (с v0.5)

До v0.5 миграций нет; при инкременте `FormatVersion` старые сейвы просто отказываются грузиться с понятным сообщением.

С v0.5:

```csharp
// core/save/Migrations.cs
public delegate JsonNode Migration(JsonNode save);

public static class Migrations
{
    private static readonly Dictionary<int, Migration> _migrations = new()
    {
        // от версии 1 к 2
        [2] = save =>
        {
            // пример: переименовали поле
            save["World"]!["Pops"] = save["World"]!["Population"];
            save["World"]!.AsObject().Remove("Population");
            return save;
        },
    };

    public static JsonNode Migrate(JsonNode save)
    {
        var version = save["FormatVersion"]!.GetValue<int>();
        while (version < CurrentVersion)
        {
            var next = version + 1;
            if (!_migrations.TryGetValue(next, out var mig))
                throw new InvalidOperationException($"No migration to v{next}");
            save = mig(save);
            save["FormatVersion"] = next;
            version = next;
        }
        return save;
    }
}
```

**Правила миграций:**

- Работают на сырых `JsonNode`, не на типизированных `record`. Старые поля могут отсутствовать.
- Никогда не удаляй уже зарелиженную миграцию. Только добавляй новые.
- Каждая миграция покрыта тестом: фикстура старого сейва → миграция → проверка инвариантов.

## Автосохранение

- Каждые N стратегических тиков (например, 4 тика = 1 игровой год). Не каждый тик — File IO стоит миллисекунды.
- Перед записью — клонируем снапшот (через сериализацию в `byte[]` пустой) и пишем в background `Task`. Иначе зафризит main thread.
- Имена: `auto1.save`, `auto2.save`, `auto3.save` — ротация.

## Проверка целостности при загрузке

1. Парсим `JsonNode`. Если не парсится → reject, не пытайся восстанавливать.
2. Читаем `FormatVersion`. Если > current → отказ («сейв из новой версии игры»).
3. Прогоняем `Migrations.Migrate`.
4. Десериализуем в `SaveFile`. Если не получается → reject.
5. Валидируем инварианты (ссылки entity существуют, время вперёд, тики неотрицательны).
6. Восстанавливаем `World`. **`IEraModule.Init` НЕ вызывается** — только `IEraModule.Rehydrate(snapshot)`.

Если на любом шаге fail — пробуем последний backup (отдельный store `*.bak.save`). Если и он fail — отказ с сообщением игроку.

## Что НЕ хранить в сейве

- Кеши, индексы, derived state — пересчитываем при загрузке.
- Состояние UI (открытые панели, скролл) — отдельный store `<saveId>.ui.json`, не часть `WorldSnapshot`.
- Контент эпох (технологии, события из конфигов) — данные **игры**, не **мира**. При `Rehydrate` эпоха сама подцепит свежий контент из `data/`.
- Текстуры, спрайты, аудио — это ассеты, не состояние.
- Активные звуки, эффекты, частицы — рендер-слой, не состояние.

## Антипаттерны

- «Я просто добавлю поле, старые сейвы загрузятся с `default(T)`». Загрузятся, но через час игры будет crash. Bump version.
- `JsonSerializer.Deserialize<SaveFile>(...)` без try/catch и валидации.
- Сейв-формат на основе class instances с поведением — не сериализуется, ломается на рефакторах.
- `using Godot;` в `core/save/*` — нарушение Закона 2. Godot-API только в `render/save/SaveStorage.cs`.
- Запись на main thread синхронно — будет тормозить геймплей.
- Хранение `EntityId` как объекта вместо int — циклы и проблемы сериализации.
