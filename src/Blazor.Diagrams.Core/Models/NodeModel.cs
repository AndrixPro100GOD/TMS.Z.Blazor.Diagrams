using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Blazor.Diagrams.Core.Models;

public class NodeModel : MovableModel, IHasBounds, IHasShape, ILinkable
{
    private readonly List<PortModel> _ports = new();
    private readonly List<BaseLinkModel> _links = new();
    private Size? _size;
    private double _rotation; // Хранит угол поворота нода в градусах (по часовой стрелке)
    private double _rotationPivotX; // Нормализованная X-точка опоры [0..1] (0=Left, 1=Right)
    private double _rotationPivotY; // Нормализованная Y-точка опоры [0..1] (0=Top, 1=Bottom)

    public event Action<NodeModel>? SizeChanged;
    public event Action<NodeModel>? Moving;
    public event Action<NodeModel>? RotationChanged; // Событие изменения угла поворота
    public event Action<NodeModel>? RotationPivotChanged; // Событие изменения точки опоры

    public NodeModel(Point? position = null) : base(position)
    {
        _rotationPivotX = 0.5;
        _rotationPivotY = 0.5;
    }

    public NodeModel(string id, Point? position = null) : base(id, position)
    {
        _rotationPivotX = 0.5;
        _rotationPivotY = 0.5;
    }

    public Size? Size
    {
        get => _size;
        set
        {
            if (value?.Equals(_size) == true)
                return;

            _size = value;
            SizeChanged?.Invoke(this);
        }
    }
    public bool ControlledSize { get; init; }

    public GroupModel? Group { get; internal set; }
    public string? Title { get; set; }

    /// <summary>
    /// Когда false — NodeRenderer добавляет CSS-класс из DiagramOptions.NonInteractableNodeCssClass
    /// к wrapper-div diagram-node, визуально блокируя pointer-events для этого нода.
    /// Управляется из модели без JS interop — изменение вступает в силу при следующем рендере.
    /// </summary>
    public bool Interactable { get; set; } = true;

    /// <summary>
    /// Угол поворота нода в градусах (по часовой стрелке). Изменение приводит к Refresh/RefreshLinks
    /// и пересчёту габаритов (AABB) для корректного хит-тестинга.
    /// </summary>
    public double Rotation
    {
        get => _rotation;
        set
        {
            var normalized = NormalizeAngle(value);
            if (Math.Abs(normalized - _rotation) < 0.0001)
                return;

            _rotation = normalized;

            // Обновляем визуал и линки, чтобы они учитывали изменение ориентации
            ReinitializePorts();
            Refresh();
            RefreshLinks();
            RotationChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Нормализованная X-точка опоры поворота [0..1]. 0 — левый край, 0.5 — центр, 1 — правый край.
    /// </summary>
    public double RotationPivotX
    {
        get => _rotationPivotX;
        set
        {
            var v = Clamp01(value);
            if (Math.Abs(v - _rotationPivotX) < 0.0001)
                return;
            _rotationPivotX = v;
            Refresh();
            RotationPivotChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Нормализованная Y-точка опоры поворота [0..1]. 0 — верхний край, 0.5 — центр, 1 — нижний край.
    /// </summary>
    public double RotationPivotY
    {
        get => _rotationPivotY;
        set
        {
            var v = Clamp01(value);
            if (Math.Abs(v - _rotationPivotY) < 0.0001)
                return;
            _rotationPivotY = v;
            Refresh();
            RotationPivotChanged?.Invoke(this);
        }
    }

    public IReadOnlyList<PortModel> Ports => _ports;
    public IReadOnlyList<BaseLinkModel> Links => _links;
    public IEnumerable<BaseLinkModel> PortLinks => Ports.SelectMany(p => p.Links);

    #region Ports

    public PortModel AddPort(PortModel port)
    {
        _ports.Add(port);
        return port;
    }

    public PortModel AddPort(PortAlignment alignment = PortAlignment.Bottom)
        => AddPort(new PortModel(this, alignment, Position));

    public PortModel? GetPort(PortAlignment alignment) => Ports.FirstOrDefault(p => p.Alignment == alignment);

    public T? GetPort<T>(PortAlignment alignment) where T : PortModel => (T?)GetPort(alignment);

    public bool RemovePort(PortModel port) => _ports.Remove(port);

    #endregion

    #region Refreshing

    public void RefreshAll()
    {
        Refresh();
        _ports.ForEach(p => p.RefreshAll());
    }

    public void RefreshLinks()
    {
        foreach (var link in Links)
        {
            link.Refresh();
            link.RefreshLinks();
        }
    }

    public void ReinitializePorts()
    {
        foreach (var port in Ports)
        {
            port.Initialized = false;
            port.Refresh();
        }
    }

    #endregion

    public override void SetPosition(double x, double y)
    {
        var deltaX = x - Position.X;
        var deltaY = y - Position.Y;
        base.SetPosition(x, y);

        UpdatePortPositions(deltaX, deltaY);
        Refresh();
        RefreshLinks();
        Moving?.Invoke(this);
    }

    public virtual void UpdatePositionSilently(double deltaX, double deltaY)
    {
        base.SetPosition(Position.X + deltaX, Position.Y + deltaY);
        UpdatePortPositions(deltaX, deltaY);
        Refresh();
    }

    public Rectangle? GetBounds() => GetBounds(false);

    public Rectangle? GetBounds(bool includePorts)
    {
        if (Size == null)
            return null;

        // Базовый прямоугольник нода (до поворота)
        var baseRect = new Rectangle(Position, Size);

        // Если угол 0 — оставляем старую быструю ветку
        if (Math.Abs(Rotation % 360) < 0.0001)
        {
            if (!includePorts)
                return baseRect;

            var leftPort0 = GetPort(PortAlignment.Left);
            var topPort0 = GetPort(PortAlignment.Top);
            var rightPort0 = GetPort(PortAlignment.Right);
            var bottomPort0 = GetPort(PortAlignment.Bottom);

            var left0 = leftPort0 == null ? baseRect.Left : Math.Min(baseRect.Left, leftPort0.Position.X);
            var top0 = topPort0 == null ? baseRect.Top : Math.Min(baseRect.Top, topPort0.Position.Y);
            var right0 = rightPort0 == null
                ? baseRect.Right
                : Math.Max(rightPort0.Position.X + rightPort0.Size.Width, baseRect.Right);
            var bottom0 = bottomPort0 == null
                ? baseRect.Bottom
                : Math.Max(bottomPort0.Position.Y + bottomPort0.Size.Height, baseRect.Bottom);

            return new Rectangle(left0, top0, right0, bottom0);
        }

        // Вычисляем AABB для повернутого прямоугольника
        var pivotPoint = new Point(
            baseRect.Left + (_size!.Width * RotationPivotX),
            baseRect.Top + (_size!.Height * RotationPivotY));
        var rotatedAabb = GetRotatedAxisAlignedBounds(baseRect, Rotation, pivotPoint);

        if (!includePorts)
            return rotatedAabb;

        // Учитываем порты как есть (их позиции абсолютные) объединением с AABB нода
        var leftPort = GetPort(PortAlignment.Left);
        var topPort = GetPort(PortAlignment.Top);
        var rightPort = GetPort(PortAlignment.Right);
        var bottomPort = GetPort(PortAlignment.Bottom);

        var left = leftPort == null ? rotatedAabb.Left : Math.Min(rotatedAabb.Left, leftPort.Position.X);
        var top = topPort == null ? rotatedAabb.Top : Math.Min(rotatedAabb.Top, topPort.Position.Y);
        var right = rightPort == null
            ? rotatedAabb.Right
            : Math.Max(rightPort.Position.X + rightPort.Size.Width, rotatedAabb.Right);
        var bottom = bottomPort == null
            ? rotatedAabb.Bottom
            : Math.Max(bottomPort.Position.Y + bottomPort.Size.Height, rotatedAabb.Bottom);

        return new Rectangle(left, top, right, bottom);
    }

    public virtual IShape GetShape()
    {
        if (Size == null)
            return Shapes.Rectangle(this);

        if (Math.Abs(Rotation % 360) < 0.0001)
            return Shapes.Rectangle(this);

        // Возвращаем форму повернутого прямоугольника для корректной работы анчоров/углов
        var rect = new Rectangle(Position, Size);
        var pivot = new Point(
            rect.Left + (_size!.Width * RotationPivotX),
            rect.Top + (_size!.Height * RotationPivotY));
        return new RotatedRectangleShape(rect, Rotation, pivot);
    }

    public virtual bool CanAttachTo(ILinkable other) => other is not PortModel && other is not BaseLinkModel;

    private void UpdatePortPositions(double deltaX, double deltaY)
    {
        // Save some JS calls and update ports directly here
        foreach (var port in _ports)
        {
            port.Position = new Point(port.Position.X + deltaX, port.Position.Y + deltaY);
            port.RefreshLinks();
        }
    }

    protected void TriggerMoving()
    {
        Moving?.Invoke(this);
    }

    void ILinkable.AddLink(BaseLinkModel link) => _links.Add(link);

    void ILinkable.RemoveLink(BaseLinkModel link) => _links.Remove(link);

    #region Rotation helpers
    private static double NormalizeAngle(double angle)
    {
        var r = angle % 360.0;
        return r < 0 ? r + 360.0 : r;
    }

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

    private static Rectangle GetRotatedAxisAlignedBounds(Rectangle rect, double angleDeg, Point pivot)
    {
        var p1 = RotateAround(rect.NorthWest, pivot, angleDeg);
        var p2 = RotateAround(rect.NorthEast, pivot, angleDeg);
        var p3 = RotateAround(rect.SouthEast, pivot, angleDeg);
        var p4 = RotateAround(rect.SouthWest, pivot, angleDeg);

        var minX = Math.Min(Math.Min(p1.X, p2.X), Math.Min(p3.X, p4.X));
        var maxX = Math.Max(Math.Max(p1.X, p2.X), Math.Max(p3.X, p4.X));
        var minY = Math.Min(Math.Min(p1.Y, p2.Y), Math.Min(p3.Y, p4.Y));
        var maxY = Math.Max(Math.Max(p1.Y, p2.Y), Math.Max(p3.Y, p4.Y));

        return new Rectangle(minX, minY, maxX, maxY);
    }

    private static Point RotateAround(Point p, Point center, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var dx = p.X - center.X;
        var dy = p.Y - center.Y;
        var x = center.X + dx * cos - dy * sin;
        var y = center.Y + dx * sin + dy * cos;
        return new Point(x, y);
    }

    private sealed class RotatedRectangleShape : IShape
    {
        private readonly Rectangle rect;
        private readonly double angleDeg;
        private readonly Point pivot;

        public RotatedRectangleShape(Rectangle rect, double angleDeg, Point pivot)
        {
            this.rect = rect;
            this.angleDeg = angleDeg;
            this.pivot = pivot;
        }

        public IEnumerable<Point> GetIntersectionsWithLine(Line line)
        {
            if (Math.Abs(angleDeg % 360) < 0.0001)
                return rect.GetIntersectionsWithLine(line);

            var p1 = RotateAround(line.Start, pivot, -angleDeg);
            var p2 = RotateAround(line.End, pivot, -angleDeg);
            var unrotated = new Line(p1, p2);

            var intersections = rect.GetIntersectionsWithLine(unrotated);
            return intersections.Select(p => RotateAround(p, pivot, angleDeg));
        }

        public Point? GetPointAtAngle(double a)
        {
            var localPoint = rect.GetPointAtAngle(a - angleDeg);
            if (localPoint == null)
                return null;
            return RotateAround(localPoint, pivot, angleDeg);
        }
    }
    #endregion
}