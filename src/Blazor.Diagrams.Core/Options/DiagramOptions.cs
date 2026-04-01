namespace Blazor.Diagrams.Core.Options;

public class DiagramOptions
{
    public int? GridSize { get; set; }
    public bool GridSnapToCenter { get; set; }
    public bool AllowMultiSelection { get; set; } = true;
    public bool AllowPanning { get; set; } = true;

    /// <summary>
    /// Разрешить перемещение (pan) диаграммы при удержании средней кнопки мыши (колёсико).
    /// В отличие от <see cref="AllowPanning"/> (левая кнопка работает только по пустому фону),
    /// средняя кнопка запускает пан по всей диаграмме — в том числе при клике поверх нода.
    /// По умолчанию <c>true</c>.
    /// </summary>
    public bool AllowMiddleButtonPan { get; set; } = true;

    /// <summary>
    /// Разрешить режим пана при удержании пробела + левая кнопка мыши (как в Figma, draw.io, Miro).
    /// При зажатом Space курсор меняется на <c>grab</c>, при перетаскивании — <c>grabbing</c>.
    /// Пан активируется по всей диаграмме, в том числе поверх нодов — пробел временно отключает
    /// выделение и перемещение нодов через левую кнопку.
    /// По умолчанию <c>true</c>.
    /// </summary>
    public bool AllowSpacePan { get; set; } = true;

    public virtual DiagramZoomOptions Zoom { get; } = new();
    public virtual DiagramLinkOptions Links { get; } = new();
    public virtual DiagramGroupOptions Groups { get; } = new();
    public virtual DiagramConstraintsOptions Constraints { get; } = new();
    public virtual DiagramVirtualizationOptions Virtualization { get; } = new();

    /// <summary>
    /// CSS-класс, применяемый к wrapper-div diagram-node когда NodeModel.Interactable = false.
    /// По умолчанию "non-interactable". Переопределите под проектный CSS при необходимости.
    /// Пример: options.NonInteractableNodeCssClass = "myapp-node-blocked";
    /// </summary>
    public string NonInteractableNodeCssClass { get; set; } = "non-interactable";
}