using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models.Base;
using Blazor.Diagrams.Core.Events;

namespace Blazor.Diagrams.Core.Behaviors;

/// <summary>
/// Управляет перемещением (pan) диаграммы. Поддерживает три режима:
/// <list type="bullet">
///   <item>Левая кнопка мыши по пустому фону диаграммы (<see cref="DiagramOptions.AllowPanning"/>)</item>
///   <item>Средняя кнопка мыши по любой области, включая ноды (<see cref="DiagramOptions.AllowMiddleButtonPan"/>)</item>
///   <item>Пробел + левая кнопка мыши по любой области — Figma/draw.io стиль (<see cref="DiagramOptions.AllowSpacePan"/>)</item>
/// </list>
/// Управляет CSS-курсором через <see cref="Diagram.SetPointerCursor"/>:
/// <c>grab</c> в Space-режиме ожидания, <c>grabbing</c> во время активного пана.
/// </summary>
public class PanBehavior : Behavior
{
    private Point? _initialPan;
    private double _lastClientX;
    private double _lastClientY;

    /// <summary>
    /// Флаг удержания пробела. Может десинхронизироваться при потере фокуса канваса;
    /// сбрасывается также на PointerDown для защиты от «залипания».
    /// </summary>
    private bool _spaceHeld;

    public PanBehavior(Diagram diagram) : base(diagram)
    {
        Diagram.PointerDown += OnPointerDown;
        Diagram.PointerMove += OnPointerMove;
        Diagram.PointerUp += OnPointerUp;
        Diagram.KeyDown += OnKeyDown;
        Diagram.KeyUp += OnKeyUp;
    }

    private void OnPointerDown(Model? model, PointerEventArgs e)
    {
        if (e.Button == (int)MouseEventButton.Wheel)
        {
            // Средняя кнопка мыши: пан по всей диаграмме, включая клики поверх нодов.
            // Стандартное поведение Figma, draw.io, Google Maps, Miro.
            StartMiddleButtonPan(e.ClientX, e.ClientY);
            return;
        }

        if (e.Button != (int)MouseEventButton.Left)
            return;

        Start(model, e.ClientX, e.ClientY, e.ShiftKey);
    }

    private void OnPointerMove(Model? model, PointerEventArgs e) => Move(e.ClientX, e.ClientY);

    private void OnPointerUp(Model? model, PointerEventArgs e) => End();

    private void OnKeyDown(KeyboardEventArgs e)
    {
        // Используем Code (физическая клавиша), а не Key (символ " "), для совместимости с раскладками
        if (e.Code != "Space")
            return;

        if (_spaceHeld)
            return; // защита от повторяющихся keydown-событий при удержании

        _spaceHeld = true;

        // Показываем курсор "grab" как визуальный сигнал готовности к пану
        if (Diagram.Options.AllowPanning && Diagram.Options.AllowSpacePan && _initialPan == null)
            Diagram.SetPointerCursor("grab");
    }

    private void OnKeyUp(KeyboardEventArgs e)
    {
        if (e.Code != "Space")
            return;

        _spaceHeld = false;

        // Если не в процессе активного пана — восстанавливаем курсор
        if (_initialPan == null)
            Diagram.SetPointerCursor("default");
    }

    private void Start(Model? model, double clientX, double clientY, bool shiftKey)
    {
        if (!Diagram.Options.AllowPanning || shiftKey)
            return;

        // Space+левая кнопка: пан везде, независимо от нода (Figma-режим)
        if (_spaceHeld && Diagram.Options.AllowSpacePan)
        {
            BeginPan(clientX, clientY);
            return;
        }

        // Обычный пан левой кнопкой: только по пустому фону диаграммы
        if (model != null)
            return;

        BeginPan(clientX, clientY);
    }

    /// <summary>
    /// Запускает пан по средней кнопке мыши. Активируется независимо от того,
    /// был ли клик по ноду или по пустому фону диаграммы.
    /// </summary>
    private void StartMiddleButtonPan(double clientX, double clientY)
    {
        if (!Diagram.Options.AllowPanning || !Diagram.Options.AllowMiddleButtonPan)
            return;

        // Защита от двойного старта: JS capture-фаза вызывает OnMiddleButtonPointerDownCapture
        // (model=null), а затем для нодов без stopPropagation (напр. платформ) дополнительно
        // приходит стандартный bubble-фазовый TriggerPointerDown(node, e). Оба ведут сюда —
        // если пан уже начат, второй вызов игнорируем.
        if (_initialPan != null)
            return;

        BeginPan(clientX, clientY);
    }

    /// <summary>
    /// Общая точка входа в активный пан: сохраняет начальное состояние и задаёт курсор.
    /// </summary>
    private void BeginPan(double clientX, double clientY)
    {
        _initialPan = Diagram.Pan;
        _lastClientX = clientX;
        _lastClientY = clientY;
        Diagram.SetPointerCursor("grabbing");
    }

    private void Move(double clientX, double clientY)
    {
        if (!Diagram.Options.AllowPanning || _initialPan == null)
            return;

        // Инкрементальный подход (frame-by-frame): вычисляем дельту только
        // относительно ПРЕДЫДУЩЕГО вызова Move, а не от начала пана.
        // Прежняя «абсолютная» формула (mouseTotal - panTotal) ломалась
        // при одновременном зуме: Diagram.Pan менялся зумом → _initialPan
        // устаревал → огромный false-delta → телепортация.
        var deltaX = clientX - _lastClientX;
        var deltaY = clientY - _lastClientY;
        _lastClientX = clientX;
        _lastClientY = clientY;
        Diagram.UpdatePan(deltaX, deltaY);
    }

    private void End()
    {
        if (!Diagram.Options.AllowPanning)
            return;

        _initialPan = null;

        // После завершения пана: если Space всё ещё удерживается — курсор "grab",
        // иначе возвращаем стандартный "default"
        var targetCursor = _spaceHeld && Diagram.Options.AllowSpacePan ? "grab" : "default";
        Diagram.SetPointerCursor(targetCursor);
    }

    public override void Dispose()
    {
        Diagram.PointerDown -= OnPointerDown;
        Diagram.PointerMove -= OnPointerMove;
        Diagram.PointerUp -= OnPointerUp;
        Diagram.KeyDown -= OnKeyDown;
        Diagram.KeyUp -= OnKeyUp;
    }
}
