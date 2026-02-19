namespace Blazor.Diagrams.Core.Options;

public class DiagramVirtualizationOptions
{
    public bool Enabled { get; set; }
    public bool OnNodes { get; set; } = true;
    public bool OnGroups { get; set; }
    public bool OnLinks { get; set; }

    /// <summary>
    /// Отступ (в пикселях) вокруг viewport при проверке видимости нода.
    /// Узел считается видимым, если его bounds пересекают viewport, расширенный на Padding.
    /// Устраняет визуальное пропадание широких/высоких нодов при пanning — нод исчезает
    /// только когда его BOUNDS полностью выходят за пределы расширенного viewport.
    /// По умолчанию 0 (без расширения, обратная совместимость).
    /// </summary>
    public double Padding { get; set; } = 0;

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
}