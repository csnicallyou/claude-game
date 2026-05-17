---
name: game-visual-style
description: Use when implementing or modifying visual rendering, sprite generation, color palette, hex tile rendering, paint-overlay strategic map, climate-driven palette shifts, or anything in `render/*` and `assets/palettes/*`. Also use when designing UI panels for paradox-density layout.
---

# game-visual-style

Визуальная дисциплина Epochs of Humanity. Бюджет $0, художника нет. Я (Claude) генерирую placeholder-визуал процедурно через Godot `Image`/`DrawNode`, потом ассеты заменяются на CC0/самодельные.

## Базовые параметры

| Параметр | Значение | Источник |
|---|---|---|
| Тактический спрайт | 16×16 px | Блок 5.2 интервью |
| Гекс-тайл | 64×64 px | Блок 5.3 интервью |
| Тип проекции | top-down 2D | Блок 5.1 |
| Стиль стратегической карты | пиксельные хексы + paint-overlay (пергамент, рукописные границы) | Блок 5.3 |
| Палитра | «земли палеолита» + климатический сдвиг | Блок 5.4 |
| Целевое разрешение | 1080p, поддержка 1440p и 4K через интегерный scaling | — |

## Палитра «земли палеолита»

Базовая палитра — 28 цветов. Имена основаны на материалах эпохи, не на абстракциях.

Файл — `assets/palettes/paleolithic-base.json`:

```json
{
  "id": "paleolithic-base",
  "name": "Земли Палеолита",
  "colors": {
    "ochre-light":      "#D9A066",
    "ochre-dark":       "#A05A2C",
    "ochre-red":        "#8E3B2D",
    "charcoal":         "#1F1A14",
    "ash-grey":         "#3F3A33",
    "bone-cream":       "#E8DAC2",
    "ivory-white":      "#F4ECDD",
    "flint-grey":       "#5C5852",
    "river-blue":       "#3B5C6E",
    "glacier-azure":    "#7AA6B8",
    "deep-water":       "#1E3640",
    "moss-green":       "#5E6E3F",
    "spring-green":     "#7E8E4A",
    "dry-grass":        "#B0A35B",
    "autumn-rust":      "#7E4B26",
    "winter-pale":      "#C9CBC4",
    "snow-blue":        "#D8E1E6",
    "blood-red":        "#7B1A1A",
    "mammoth-brown":    "#5A3A1F",
    "fur-tan":          "#9C7048",
    "hearth-orange":    "#D86B2E",
    "fire-yellow":      "#E8A93C",
    "smoke-grey":       "#6E6A63",
    "skin-light":       "#D4A887",
    "skin-medium":      "#B07E58",
    "skin-dark":        "#7A4F30",
    "shadow":           "#1A1612",
    "highlight":        "#FFF6E1"
  }
}
```

Все цвета — в `core/visual/PaletteRegistry.cs` (хотя `core/` обычно не render, исключение для палитры как чистых данных). Любой код, который рисует — берёт цвет по имени, **не литералом**.

```csharp
// плохо
var color = new Color(0.85f, 0.63f, 0.4f);

// хорошо
var color = palette["ochre-light"];
```

## Климатический сдвиг

Это **уникальная фишка**, которой нет у референсов. При смене сезона/эпохи палитра динамически сдвигается:

| Состояние | Сдвиг |
|---|---|
| Лето | базовая палитра |
| Осень | +30% rust, +20% autumn-orange, −10% green |
| Зима (без ледника) | +50% pale, +30% snow-blue, −40% green, −20% saturation |
| Ледниковый максимум (post-MVP) | +80% pale/blue, −90% green, +глобальная desaturation |
| Засуха | +20% dry-grass, +30% ochre-dark, −50% river-blue, −30% green |

Реализация через `Godot.Shader` (один пост-обработочный шейдер на весь экран) с параметрами `seasonShift`, `aridity`, `glaciation`. Sim публикует `ClimateState` каждый стратегический тик, render-слой читает и применяет.

```csharp
// render/effects/ClimateShiftShader.cs (только в render-слое, можно using Godot)
public partial class ClimateShiftShader : ColorRect
{
    public override void _Ready()
    {
        Material = ShaderMaterial; // загружен из assets/shaders/climate-shift.gdshader
    }

    public void Apply(ClimateState climate)
    {
        var mat = (ShaderMaterial)Material;
        mat.SetShaderParameter("season_shift", climate.SeasonalIndex); // -1..+1
        mat.SetShaderParameter("aridity", climate.Aridity);            // 0..1
        mat.SetShaderParameter("glaciation", climate.Glaciation);      // 0..1
    }
}
```

## Гекс-тайлы

64×64 px, ориентация **pointy-top** (классика для стратегий, упрощает соседство).

```
   ___
  /   \
 /     \
 \     /
  \___/
```

Каждый тайл — это **слоистая композиция**:

1. Базовый цвет биома (1 цвет из палитры).
2. Текстурный паттерн биома (шумный фон, 2-3 цвета — например, лес = тёмно-зелёный с пятнами moss-green).
3. Pictogram-наложение (если значимый объект — гора, пещера, река) — отдельный спрайт 32×32 в центре тайла.
4. Paint-overlay границы (рукописная штриховка между соседними биомами разного типа).

Биом конфигурируется в `data/biomes/<biome>.json`:

```json
{
  "id": "carmel-foothills",
  "nameKey": "biome.carmel_foothills.name",
  "baseColor": "moss-green",
  "patternColors": ["spring-green", "dry-grass"],
  "pictograms": [
    { "key": "tree-conifer", "weight": 0.4 },
    { "key": "rock-small", "weight": 0.1 }
  ],
  "habitability": 0.7,
  "huntingDensity": 0.6
}
```

Биомы Леванта 45k BCE (для `data/biomes/`):

- `levantine-coast` — побережье Средиземного
- `carmel-foothills` — предгорья Кармеля (наш стартовый регион)
- `jordan-valley` — долина Иордана
- `dead-sea-basin` — район Мёртвого моря (тогда выше уровня)
- `sinai-desert` — Синайская пустыня
- `negev-arid` — Негев
- `golan-volcanic` — Голанские базальты
- `lebanon-cedars` — Кедровые леса Ливана
- `zagros-foothills` — предгорья Загроса
- `mesopotamia-marsh` — Месопотамские болота
- `arabian-savannah` — Аравийская саванна (45k была влажнее)

## Стратегическая карта: paint-overlay

Идея — пиксельные гексы под пергаментной поверхностью.

```
[ Слой 0 ] Пергаментный фон — текстура 256×256, повторяющаяся
[ Слой 1 ] Пиксельные хексы (биомы) — рендерятся как TileMap
[ Слой 2 ] Реки — рукописные линии river-blue, рисованные через Line2D
[ Слой 3 ] Границы провинций — штрихованные, charcoal цвет, толщина 2px
[ Слой 4 ] Pictograms значимых мест — пещеры, стоянки, горы — статические Sprite2D
[ Слой 5 ] Юниты и события — анимированные спрайты
[ Слой 6 ] UI overlay — Control nodes
```

«Перо» эстетика создаётся **сдвигом цвета** (всё чуть приглушено через `ColorRect` с modulate 0.92), плюс лёгким noise-эффектом через ColorOverlay-шейдер.

## Тактическая карта (поселение)

Top-down 2D, чистый. 16×16 спрайты людей и животных, 32×32 для зданий (шалаши, костры). Сетка 1 тайл = 16 px = 1 «шаг» жителя.

Цвета — та же палитра, но без paint-overlay. Здесь эстетика — «чистый Songs of Syx».

## Спрайт человека (16×16)

```
   .##.        # = тёмная обводка (charcoal)
  .####.       . = цвет волос/головного убора
  ######       o = цвет лица (skin-*)
  .oooo.
   .oo.
  .####.
 ##....##      одежда: 1 основной цвет + 1 акцент
 #.::::.#
 #.::::.#
  ######
  #.  .#
  #.  .#
  #.  .#
```

(Это приблизительная схема, фактический пиксель — рисую в коде через `Image.SetPixel` в Godot.)

Палитра одежды зависит от культуры (см. `data/cultures/*.json`) и сезона.

## UI: Paradox-density

Игрок выбрал плотные панели (блок 6.2 a). Это означает:

- Внутриигровой UI — пиксельные панели (Godot Control + кастомный тема).
- Шрифт: моноширинный пиксельный (например, `m6x11` или собственный 8x8/10x10).
- Размер базового UI-элемента — кратен 4px (для DPI scaling без размытия).
- Цвета панелей — `charcoal` (фон), `bone-cream` (текст), `ochre-dark` (акцент), `flint-grey` (border).
- Иконки — 16×16 в той же палитре.

Меню/настройки/главное меню — стандартные Godot Control в нейтральном стиле, без пиксельной нагрузки (это блок 6.2 c).

## Что я рисую процедурно в коде (MVP)

| Объект | Подход |
|---|---|
| Гекс-тайл биома | Заливка цветом + шумный паттерн (Perlin/simplex через .NET) + случайные пиксели акцента |
| Спрайт человека | Композиция: голова (3×3) + торс (3×4) + цвет одежды по культуре |
| Спрайт мегафауны | Прямоугольный силуэт по размерам животного (мамонт — 16×12, олень — 12×8) с цветом fur-* |
| Pictogram гор/пещер | Скетч-стиль чёрной линией на ochre-light фоне |
| Реки | Bézier-кривые между точками гекс-центров |
| Дым костра | Partycle система Godot с цветами smoke-grey → дёргать в shader |

Всё это — placeholder. Финальные ассеты — позже (CC0 / художник / я-сам-нарисую).

## Что НЕ делать

- **Литеральные `Color(r, g, b)` в коде рендера.** Только через `palette["color-name"]`.
- **Импортировать render-код в `core/sim/eras/`.** Нарушение Закона 2.
- **Произвольный размер спрайтов.** 16×16 или 32×32 для людей/зданий, 64×64 для гексов. Промежуточные размеры путают пиксельную сетку.
- **Anti-aliasing на пиксельных ассетах.** Текстуры импортятся в Godot с `Filter: Nearest`.
- **«Дорогие» эффекты в MVP.** Никаких полноэкранных нормал-мап теней, никаких light-баков. Один climate-shader на весь экран — потолок.

## Антипаттерны

- Прямое использование `Godot.Color.SOMETHING` — нет palette discipline.
- Размер UI в пикселях не кратен 4 — при scaling выглядит размыто.
- Перемешивание шрифтов (пиксельный для одних панелей, sans-serif для других) — нет единого языка.
- «Шейдер на каждый кейс» — производительность и поддержка. Лучше один универсальный climate-shader.
