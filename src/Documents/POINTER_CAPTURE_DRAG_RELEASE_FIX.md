# Исправление «залипания» перетаскивания при отпускании мыши вне DiagramCanvas

## Проблема

При перетаскивании диаграммы (пан) пользователь удерживает левую кнопку мыши и перемещает курсор. Если курсор выходит за границы компонента DiagramCanvas и кнопка отпускается снаружи, браузер не доставляет событие `pointerup` на канвас. В результате:

- Диаграмма остаётся в режиме перетаскивания (`_initialPan != null` в `PanBehavior`);
- При возврате курсора на диаграмму она продолжает «перетаскиваться» без удержания кнопки;
- Пользователю приходится выполнять лишний клик для сброса состояния.

## Причина

События `pointerup` и `pointercancel` доставляются элементу, над которым отпущена кнопка. Если кнопка отпущена вне элемента `DiagramCanvas`, его обработчики `@onpointerup` не вызываются.

## Решения

### 1. Изменения в TMS.Z.Blazor.Diagrams (библиотека)

#### `script.js` — pointer capture и document-level fallback

- **Pointer capture** — при `pointerdown` на `.diagram-canvas` вызывается `element.setPointerCapture(pointerId)`, чтобы последующие события указателя доставлялись канвасу, даже если кнопка отпущена снаружи.
- **Обработчик в фазе capture** — `addEventListener('pointerdown', captureHandler, true)` обеспечивает выполнение до Blazor.
- **Document-level fallback** — при `pointerup`/`pointercancel` на `document` с целью вне канваса вызывается `c.ref.invokeMethodAsync('OnPointerUpOutside', ...)` для прямого вызова .NET (в Blazor Server `dispatchEvent` может не срабатывать).

#### `DiagramCanvas.razor.cs` — метод OnPointerUpOutside

```csharp
[JSInvokable]
public void OnPointerUpOutside(double clientX, double clientY, long button, long buttons, long pointerId)
{
    var e = new Core.Events.PointerEventArgs(...);
    BlazorDiagram.TriggerPointerUp(null, e);
}
```

Метод вызывается из JS при отпускании кнопки вне канваса и завершает перетаскивание через `TriggerPointerUp`.

### 2. Изменения в потребляющем приложении (SchemeDiagram)

Так как библиотечный скрипт может не подключаться или не выполнять логику в нужный момент (например, в Blazor Server), добавлен запасной вариант в самом приложении.

#### `SetupDiagramPointerCaptureAsync` (SchemeDiagram.razor.cs)

Регистрирует JS-логику через `eval`:

- **tryAttach** — находит `.diagram-canvas` внутри `#scheme-diagram-layers` с повторными попытками (150 мс), чтобы дождаться появления DOM.
- **pointerdown** — `setPointerCapture` и сохранение `activePointerId`.
- **document pointerup/pointercancel** — при отпускании вне канваса вызывается `OnDiagramPointerUpOutside` через DotNetObjectReference.
- **document pointermove** — при `buttons === 0` (кнопка отпущена) и наличии активного указателя вызывается завершение перетаскивания. Это fallback, если `pointerup` не доходит до канваса.

#### Вызов setup

- В `OnAfterRenderAsync` при первом рендере (`firstRender`).
- При появлении `Diagram.Container` (fallback для Interactive Server).

#### Очистка

- `RemoveDiagramPointerCaptureAsync` вызывается в `DisposeAsync` для снятия обработчиков и освобождения `DotNetObjectReference`.

## Результат

Пользователь может отпускать левую кнопку мыши в любом месте экрана — перетаскивание корректно завершается. Поведение одинаково при использовании:

- Blazor WebAssembly;
- Blazor Server (Interactive);
- локального форка TMS.Z.Blazor.Diagrams;
- NuGet-пакета TMS.Z.Blazor.Diagrams.

## Файлы с изменениями

| Расположение | Файл | Описание |
|--------------|------|----------|
| TMS.Z.Blazor.Diagrams | `src/Blazor.Diagrams/wwwroot/script.js` | Pointer capture, document handlers, invokeMethodAsync |
| TMS.Z.Blazor.Diagrams | `src/Blazor.Diagrams/Components/DiagramCanvas.razor.cs` | OnPointerUpOutside JSInvokable |
| TMS.Web.SharedLibrary | `SchemeDiagram.razor.cs` | SetupDiagramPointerCaptureAsync, OnDiagramPointerUpOutside, teardown |
