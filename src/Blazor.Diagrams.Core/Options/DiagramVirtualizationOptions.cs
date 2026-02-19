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
}