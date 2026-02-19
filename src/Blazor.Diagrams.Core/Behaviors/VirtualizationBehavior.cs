using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;

namespace Blazor.Diagrams.Core.Behaviors;

public class VirtualizationBehavior : Behavior
{
    public VirtualizationBehavior(Diagram diagram) : base(diagram)
    {
        Diagram.ZoomChanged += CheckVisibility;
        Diagram.PanChanged += CheckVisibility;
        Diagram.ContainerChanged += CheckVisibility;

        // Подписываемся на изменение размера существующих нодов:
        // RecalculateNodeSize() обновляет Node.Size ПОСЛЕ монтирования компонента,
        // но VirtualizationBehavior слушает только Pan/Zoom/Container.
        // Без этой подписки длинный нод скрывается по устаревшему (дефолтному 60×25) размеру
        // и не пересчитывает видимость, пока пользователь не покрутит диаграмму.
        Diagram.Nodes.Added += OnNodeAdded;
        Diagram.Nodes.Removed += OnNodeRemoved;
        foreach (var node in Diagram.Nodes)
            SubscribeToNodeSize(node);
    }

    private void OnNodeAdded(NodeModel node) => SubscribeToNodeSize(node);
    private void OnNodeRemoved(NodeModel node) => UnsubscribeFromNodeSize(node);

    private void SubscribeToNodeSize(NodeModel node)
        => node.SizeChanged += OnNodeSizeChanged;

    private void UnsubscribeFromNodeSize(NodeModel node)
        => node.SizeChanged -= OnNodeSizeChanged;

    /// <summary>
    /// Пересчёт видимости конкретного нода при изменении его размера.
    /// Вызывается из RecalculateNodeSize() через Node.Size setter → SizeChanged.
    /// Без этого: нод скрывается/показывается по устаревшему размеру до следующего Pan/Zoom события.
    /// </summary>
    private void OnNodeSizeChanged(NodeModel node)
    {
        if (!Diagram.Options.Virtualization.Enabled || !Diagram.Options.Virtualization.OnNodes)
            return;
        if (Diagram.Container == null)
            return;
        CheckVisibility(node);
    }

    private void CheckVisibility()
    {
        if (!Diagram.Options.Virtualization.Enabled)
            return;
        
        if (Diagram.Container == null)
            return;

        if (Diagram.Options.Virtualization.OnNodes)
        {
            foreach (var node in Diagram.Nodes)
            {
                CheckVisibility(node);
            }
        }

        if (Diagram.Options.Virtualization.OnGroups)
        {
            foreach (var group in Diagram.Groups)
            {
                CheckVisibility(group);
            }
        }

        if (Diagram.Options.Virtualization.OnLinks)
        {
            foreach (var link in Diagram.Links)
            {
                CheckVisibility(link);
            }
        }
    }

    private void CheckVisibility(Model model)
    {
        if (model is not IHasBounds ihb)
            return;
        
        var bounds = ihb.GetBounds();
        if (bounds == null)
            return;
        
        var left = bounds.Left * Diagram.Zoom + Diagram.Pan.X;
        var top = bounds.Top * Diagram.Zoom + Diagram.Pan.Y;
        var right = left + bounds.Width * Diagram.Zoom;
        var bottom = top + bounds.Height * Diagram.Zoom;

        // Padding расширяет зону видимости — нод остаётся Visible пока он не выйдет
        // за расширенный прямоугольник. Это устраняет артефакт мгновенного пропадания
        // длинных/широких нодов при panning, когда часть нода ещё в экране.
        var padding = Diagram.Options.Virtualization.Padding;
        model.Visible = right > -padding
                     && left < Diagram.Container!.Width + padding
                     && bottom > -padding
                     && top < Diagram.Container.Height + padding;
    }

    public override void Dispose()
    {
        Diagram.ZoomChanged -= CheckVisibility;
        Diagram.PanChanged -= CheckVisibility;
        Diagram.ContainerChanged -= CheckVisibility;

        Diagram.Nodes.Added -= OnNodeAdded;
        Diagram.Nodes.Removed -= OnNodeRemoved;
        foreach (var node in Diagram.Nodes)
            UnsubscribeFromNodeSize(node);
    }
}