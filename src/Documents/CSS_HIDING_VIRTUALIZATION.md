# CSS-hiding режим виртуализации: устранение Scripting spike при pan/zoom

## Проблема

При включённой стандартной виртуализации (`DiagramVirtualizationOptions.Enabled = true`) компонент `NodeRenderer` полностью **удаляет** нод из Blazor render tree когда `Node.Visible = false`:

```csharp
// NodeRenderer.cs — до изменения
if (!Node.Visible)
    return; // Blazor уничтожает весь поддерев
```

Это вызывает катастрофический эффект при pan/zoom в схемах с тяжёлыми нодами (например, `NodeStock` со `StockPureGrid` + `StockCell`):

```
Mouse drag (PanChanged)
  → VirtualizationBehavior.CheckVisibility (все ноды)
    → Visible = false → NodeRenderer удаляет NodeStock из DOM
    → Visible = true  → NodeStock монтируется заново
      → StockPureGrid + N×M StockCell создаются с нуля
        → 725ms Scripting per drag event → UI freeze
```

**Измеренные показатели** при виртуализации включённой стандартным способом:

| Метрика | Значение |
|---|---|
| Средняя задержка drag | ~1700ms (WASM) |
| Scripting per drag | ~725ms |
| FPS во время пана | <10 |

При выключенной виртуализации (`Enabled = false`): ~150ms drag, ~100ms (InteractiveServer).

## Причина

Blazor component lifecycle при `BuildRenderTree → return` без output-а уничтожает все дочерние компоненты. Для `NodeStock` это означает:
- `StockPureGrid` (1 компонент)
- `StockCell` (N строк × M столбцов, например 4×20 = 80 компонентов)
- Все `CellTitle` компоненты при Full render level

Каждый вход нода в viewport = монтирование 80+ компонентов с нуля = DOM insertion spike.

## Решение: CSS Hiding Mode

Вместо удаления из DOM — скрываем нод через `display:none`. Компонент **остаётся смонтированным** в памяти, DOM-элемент существует, но скрыт из layout и недоступен для взаимодействия.

```
Mouse drag (PanChanged)
  → VirtualizationBehavior.CheckVisibility (все ноды)
    → Visible = false → NodeRenderer применяет display:none (NO destroy)
    → Visible = true  → NodeRenderer снимает display:none (NO mount)
      → 0ms Scripting mount overhead → нет freeze
```

### Ключевой факт: data freshness бесплатна

При `display:none` компонент **живёт в памяти** и реагирует на `RaiseStateChanged()` как обычно:

```
SignalR → ApplyBatchFromProcessorAsync → node.RaiseStateChanged()
  → NodeAdvancedComponentBase.ShouldRender() = true
  → StateHasChanged()
  → Blazor обновляет DOM нода (даже display:none)
  → При снятии display:none браузер показывает уже актуальный контент
```

Никакого отдельного кэша нет — данные в модели + живой компонент.

**Tradeoff:** все ноды всегда в DOM-памяти (аналогично `VirtualizationEnabled = false`), но без пересоздания при каждом pan.

## Изменения в форке

### 1. `DiagramVirtualizationOptions.cs` (Core)

Добавлено новое свойство:

```csharp
/// <summary>
/// Режим CSS-скрытия: при Visible=false нод скрывается через display:none,
/// но НЕ удаляется из Blazor render tree (компонент остаётся смонтированным).
/// Устраняет mount/unmount overhead при pan/zoom: StockPureGrid и StockCell
/// не пересоздаются при повторном входе в viewport — нет Scripting spike.
/// SignalR обновления работают нормально — компонент живёт в памяти и реагирует
/// на RaiseStateChanged() независимо от CSS visibility. Data freshness бесплатна.
/// Tradeoff: все ноды всегда в DOM-памяти (аналогично VirtualizationEnabled=false).
/// </summary>
public bool CssHiding { get; set; } = false;
```

`BlazorDiagramVirtualizationOptions` наследуется от `DiagramVirtualizationOptions` и получает свойство автоматически.

### 2. `NodeRenderer.cs` (Blazor)

#### BuildRenderTree

Было:
```csharp
if (!Node.Visible)
    return;
```

Стало:
```csharp
// При CssHiding=true нод всегда остаётся в render tree, скрывается через display:none.
// При CssHiding=false (стандартное поведение) — убираем из DOM при Visible=false.
bool cssHiding = BlazorDiagram.Options.Virtualization.CssHiding;
if (!Node.Visible && !cssHiding)
    return;
```

Для HTML-нодов (`_isSvg = false`) в блоке построения `style`:
```csharp
// При CssHiding и невидимом ноде: display:none скрывает из layout,
// pointer-events:none блокирует клики. Компонент остаётся смонтированным.
if (!Node.Visible && cssHiding)
    style.Append("; display:none; pointer-events:none");
```

Для SVG-нодов (`_isSvg = true`) в блоке `transform`:
```csharp
// SVG не поддерживает display:none без wrapper; scale(0) скрывает визуально.
if (!Node.Visible && cssHiding)
    transform += " scale(0)";
```

#### OnAfterRenderAsync

Обновлено условие раннего return:
```csharp
// При CssHiding=true: нод отрендерен (display:none), но обработку firstRender
// выполняем в обычном порядке — нод смонтирован, _element валиден.
bool cssHiding = BlazorDiagram.Options.Virtualization.CssHiding;
if (firstRender && !Node.Visible && !cssHiding)
    return;
```

## Изменения в потребляющем приложении

### `SchemeVirtualizationOptions.cs`

```csharp
/// <summary>
/// CSS-hiding режим: при Visible=false нод скрывается через display:none вместо удаления из DOM.
/// При включённом CssHidingEnabled рекомендуется отключить ProgressiveOnViewport=false.
/// </summary>
public bool CssHidingEnabled { get; set; } = false;
```

### `SchemeDiagram.razor.cs`

Поле состояния:
```csharp
/// <summary>
/// При true — виртуализация работает в CSS-hiding режиме: ноды скрываются через display:none,
/// не удаляются из render tree. Нет mount/unmount при pan/zoom, нет Scripting spike.
/// </summary>
private bool _cssHidingEnabled;
```

Инициализация в `OnInitializedAsync`:
```csharp
_cssHidingEnabled = VirtualizationOptions?.Value?.CssHidingEnabled ?? false;

Virtualization =
{
    Enabled = libraryVirtualizationEnabled,
    OnNodes = true,
    // ...
    CssHiding = _cssHidingEnabled  // ← передаём в форк
},
```

Регистрация `ProgressiveVirtualizationBridgeBehavior` при CssHiding:
```csharp
// При CssHiding=true и ProgressiveOnViewport=false — мост не регистрируется:
// ноды всегда смонтированы, отслеживать VisibilityChanged незачем.
bool progressiveOnViewport = VirtualizationOptions?.Value?.ProgressiveOnViewport ?? true;
bool needsBridge = libraryVirtualizationEnabled && (progressiveOnViewport || _cssHidingEnabled);
```

### `SchemeDiagram.Viewport.cs`

`OnNodeBecameVisible` при `_cssHidingEnabled`:
```csharp
if (_cssHidingEnabled)
{
    // Компонент уже смонтирован, IsViewportNear уже true.
    // CSS display:none снимается NodeRenderer через Visible=true.
    return;
}
```

`OnNodeBecameHidden` при `_cssHidingEnabled`:
```csharp
if (_cssHidingEnabled)
{
    // Компонент остаётся смонтированным — не сбрасываем IsViewportNear и RenderLevel.
    // NodeRenderer применяет display:none, компонент продолжает жить в памяти.
    return;
}
```

### `appsettings.json`

```json
"Virtualization": {
  "VirtualizationEnabled": true,
  "CssHidingEnabled": true,
  "VirtualizationPaddingPx": 200,
  "ProgressiveOnViewport": false
}
```

## Ожидаемый эффект

| Метрика | VirtualizationEnabled=false | VirtualizationEnabled=true (стандарт) | VirtualizationEnabled=true + CssHiding |
|---|---|---|---|
| Drag latency | ~150ms | ~1700ms | ~150ms |
| Scripting / drag | низкий | ~725ms | ~0 (нет mount) |
| Память (DOM) | все ноды | только viewport | все ноды |
| Data freshness (SignalR) | + | + | + (компонент живёт) |
| Апгрейд-pipeline | нет | Placeholder→Partial→Full | нет (всегда Full) |

## Файлы с изменениями

| Расположение | Файл | Описание |
|---|---|---|
| TMS.Z.Blazor.Diagrams | `src/Blazor.Diagrams.Core/Options/DiagramVirtualizationOptions.cs` | Новое поле `CssHiding` |
| TMS.Z.Blazor.Diagrams | `src/Blazor.Diagrams/Components/Renderers/NodeRenderer.cs` | `BuildRenderTree` + `OnAfterRenderAsync` |
| TMS.Web.SharedLibrary | `Services/Scheme/SchemeVirtualizationOptions.cs` | Новое поле `CssHidingEnabled` |
| TMS.Web.SharedLibrary | `SchemeDiagram.razor.cs` | `_cssHidingEnabled`, инициализация, bridge регистрация |
| TMS.Web.SharedLibrary | `SchemeDiagram.Viewport.cs` | `OnNodeBecameVisible`, `OnNodeBecameHidden` |
| TMS.Web.Client | `wwwroot/appsettings.json` | `CssHidingEnabled: true`, `ProgressiveOnViewport: false` |
