using System.Text;
using Blazor.Diagrams.Core.Extensions;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;
using Blazor.Diagrams.Extensions;
using Blazor.Diagrams.Models;
using Blazor.Diagrams.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazor.Diagrams.Components.Renderers;

public class NodeRenderer : ComponentBase, IDisposable
{
    private bool _becameVisible;
    private ElementReference _element;
    private bool _isSvg;
    private DotNetObjectReference<NodeRenderer>? _reference;
    private bool _shouldRender;

    [CascadingParameter] public BlazorDiagram BlazorDiagram { get; set; } = null!;

    [Parameter] public NodeModel Node { get; set; } = null!;

    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;

    public void Dispose()
    {
        Node.Changed -= OnNodeChanged;
        Node.VisibilityChanged -= OnVisibilityChanged;

        if (_element.Id != null && !Node.ControlledSize)
        {
            _ = JsRuntime.UnobserveResizes(_element);
        }

        _reference?.Dispose();
    }

    [JSInvokable]
    public void OnResize(Size size)
    {
        // When the node becomes invisible (a.k.a unrendered), the size is zero
        if (Size.Zero.Equals(size))
            return;

        size = new Size(size.Width / BlazorDiagram.Zoom, size.Height / BlazorDiagram.Zoom);
        if (Node.Size != null && Node.Size.Width.AlmostEqualTo(size.Width) &&
            Node.Size.Height.AlmostEqualTo(size.Height))
        {
            return;
        }

        Node.Size = size;
        Node.Refresh();
        Node.RefreshLinks();
        Node.ReinitializePorts();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _reference = DotNetObjectReference.Create(this);
        Node.Changed += OnNodeChanged;
        Node.VisibilityChanged += OnVisibilityChanged;
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        _isSvg = Node is SvgNodeModel;
    }

    protected override bool ShouldRender()
    {
        if (!_shouldRender)
            return false;

        _shouldRender = false;
        return true;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // При CssHiding=true нод всегда остаётся в render tree, скрывается через display:none.
        // Это устраняет mount/unmount StockPureGrid + StockCell при pan/zoom — нет Scripting spike.
        // При CssHiding=false (стандартное поведение) — убираем из DOM при Visible=false.
        bool cssHiding = BlazorDiagram.Options.Virtualization.CssHiding;
        if (!Node.Visible && !cssHiding)
            return;

        var componentType = BlazorDiagram.GetComponent(Node) ??
                            (_isSvg ? typeof(SvgNodeWidget) : typeof(NodeWidget));
        // Имя класса non-interactable берётся из настроек диаграммы — проект может переопределить.
        var nonInteractableClass = BlazorDiagram.Options.NonInteractableNodeCssClass;

        var classes = new StringBuilder("diagram-node")
            .AppendIf(" locked", Node.Locked)
            .AppendIf(" selected", Node.Selected)
            .AppendIf(" grouped", Node.Group != null)
            .AppendIf($" {nonInteractableClass}", !Node.Interactable); // класс блокировки взаимодействия

        builder.OpenElement(0, _isSvg ? "g" : "div");
        builder.AddAttribute(1, "class", classes.ToString());
        builder.AddAttribute(2, "data-node-id", Node.Id);

        if (_isSvg)
        {
            // SVG: сначала переносим в позицию нода, затем вращаем относительно верхнего-левого угла
            var transform = $"translate({Node.Position.X.ToInvariantString()} {Node.Position.Y.ToInvariantString()})";
            if (!Node.Rotation.AlmostEqualTo(0))
            {
                // Вращаем вокруг указанной точки опоры (в координатах локального прямоугольника)
                var originX = (Node.Size?.Width ?? 0) * Node.RotationPivotX;
                var originY = (Node.Size?.Height ?? 0) * Node.RotationPivotY;
                transform += $" translate({originX.ToInvariantString()} {originY.ToInvariantString()}) rotate({Node.Rotation.ToInvariantString()}) translate({(-originX).ToInvariantString()} {(-originY).ToInvariantString()})";
            }
            // При CssHiding и невидимом SVG-ноде — скрываем через visibility
            if (!Node.Visible && cssHiding)
                transform += " scale(0)"; // SVG не поддерживает display:none без wrapper; scale(0) скрывает
            builder.AddAttribute(3, "transform", transform);
        }
        else
        {
            // HTML: позиционируем и вращаем контейнер нода вокруг верхнего-левого угла,
            // чтобы его позиция оставалась якорем
            var style = new StringBuilder()
                .Append($"top: {Node.Position.Y.ToInvariantString()}px; left: {Node.Position.X.ToInvariantString()}px");
            if (!Node.Rotation.AlmostEqualTo(0))
            {
                // CSS transform-origin в px от верхнего-левого угла
                var originX = (Node.Size?.Width ?? 0) * Node.RotationPivotX;
                var originY = (Node.Size?.Height ?? 0) * Node.RotationPivotY;
                style.Append($"; transform-origin: {originX.ToInvariantString()}px {originY.ToInvariantString()}px; transform: rotate({Node.Rotation.ToInvariantString()}deg)");
            }
            // При CssHiding и невидимом ноде: display:none скрывает из layout, pointer-events:none
            // блокирует клики. Компонент остаётся смонтированным — нет lifecycle overhead при pan/zoom.
            if (!Node.Visible && cssHiding)
                style.Append("; display:none; pointer-events:none");
            builder.AddAttribute(3, "style", style.ToString());
        }

        builder.AddAttribute(4, "onpointerdown", EventCallback.Factory.Create<PointerEventArgs>(this, OnPointerDown));
        builder.AddEventStopPropagationAttribute(5, "onpointerdown", true);
        builder.AddAttribute(6, "onpointerup", EventCallback.Factory.Create<PointerEventArgs>(this, OnPointerUp));
        builder.AddEventStopPropagationAttribute(7, "onpointerup", true);
        builder.AddAttribute(8, "onmouseenter", EventCallback.Factory.Create<MouseEventArgs>(this, OnMouseEnter));
        builder.AddAttribute(9, "onmouseleave", EventCallback.Factory.Create<MouseEventArgs>(this, OnMouseLeave));
        builder.AddElementReferenceCapture(10, value => _element = value);
        builder.OpenComponent(11, componentType);
        builder.AddAttribute(12, "Node", Node);
        builder.CloseComponent();

        builder.CloseElement();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // При стандартной виртуализации (CssHiding=false): если нод невидим на firstRender,
        // компонент не был отрендерен (BuildRenderTree вернул пусто) — нечего регистрировать.
        // При CssHiding=true: нод отрендерен (display:none), но ElementReference не имеет
        // реального layout — регистрируем ResizeObserver только когда нод станет видимым
        // (_becameVisible=true) чтобы получить корректный размер.
        bool cssHiding = BlazorDiagram.Options.Virtualization.CssHiding;
        if (firstRender && !Node.Visible && !cssHiding)
            return;

        if (firstRender || _becameVisible)
        {
            _becameVisible = false;

            // При CssHiding и невидимом ноде на firstRender откладываем регистрацию ResizeObserver:
            // display:none даёт size=0 → OnResize(zero) пропустим, но зарегистрируем впрок.
            // Когда нод станет видимым (OnVisibilityChanged → ReRender → _becameVisible=true),
            // ResizeObserver уже зарегистрирован и получит актуальный размер.
            if (!Node.ControlledSize)
            {
                await JsRuntime.ObserveResizes(_element, _reference!);
            }
        }
    }

    private void OnNodeChanged(Model _)
    {
        ReRender();
    }

    private void OnVisibilityChanged(Model _)
    {
        _becameVisible = Node.Visible;
        ReRender();
    }

    private void ReRender()
    {
        _shouldRender = true;
        InvokeAsync(StateHasChanged);
    }

    private void OnPointerDown(PointerEventArgs e)
    {
        BlazorDiagram.TriggerPointerDown(Node, e.ToCore());
    }

    private void OnPointerUp(PointerEventArgs e)
    {
        BlazorDiagram.TriggerPointerUp(Node, e.ToCore());
    }

    private void OnMouseEnter(MouseEventArgs e)
    {
        BlazorDiagram.TriggerPointerEnter(Node, e.ToCore());
    }

    private void OnMouseLeave(MouseEventArgs e)
    {
        BlazorDiagram.TriggerPointerLeave(Node, e.ToCore());
    }
}