using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Dialogs;

public sealed class XmapGraphCanvas : Canvas
{
    public static readonly DependencyProperty EdgesSourceProperty = DependencyProperty.Register(
        nameof(EdgesSource),
        typeof(IEnumerable<XmapGraphEdgeViewModel>),
        typeof(XmapGraphCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnEdgesSourceChanged));

    public IEnumerable<XmapGraphEdgeViewModel>? EdgesSource
    {
        get => (IEnumerable<XmapGraphEdgeViewModel>?)GetValue(EdgesSourceProperty);
        set => SetValue(EdgesSourceProperty, value);
    }

    private static readonly Pen s_NormalPen;
    private static readonly Pen s_NpcPen;
    private static readonly Pen s_RoutePen;
    private static readonly Pen s_RouteNpcPen;
    private static readonly Brush s_NormalDotBrush;
    private static readonly Brush s_RouteDotBrush;

    static XmapGraphCanvas()
    {
        var borderHighlightBrush = new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x3A));
        borderHighlightBrush.Freeze();

        var npcBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x0E, 0x4F));
        npcBrush.Freeze();

        var routeBrush = new SolidColorBrush(Color.FromRgb(0xF7, 0xC9, 0x34));
        routeBrush.Freeze();

        s_NormalDotBrush = borderHighlightBrush;
        s_RouteDotBrush = routeBrush;

        s_NormalPen = new Pen(borderHighlightBrush, 2.0)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        s_NormalPen.Freeze();

        s_NpcPen = new Pen(npcBrush, 2.0)
        {
            DashStyle = DashStyles.Dash,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        s_NpcPen.Freeze();

        s_RoutePen = new Pen(routeBrush, 4.0)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        s_RoutePen.Freeze();

        s_RouteNpcPen = new Pen(routeBrush, 4.0)
        {
            DashStyle = DashStyles.Dash,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        s_RouteNpcPen.Freeze();
    }

    private static void OnEdgesSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not XmapGraphCanvas canvas) return;

        if (e.OldValue is INotifyCollectionChanged oldColl)
        {
            oldColl.CollectionChanged -= canvas.OnEdgesCollectionChanged;
        }

        if (e.OldValue is IEnumerable<XmapGraphEdgeViewModel> oldEdges)
        {
            foreach (var edge in oldEdges)
            {
                edge.PropertyChanged -= canvas.OnEdgePropertyChanged;
            }
        }

        if (e.NewValue is INotifyCollectionChanged newColl)
        {
            newColl.CollectionChanged += canvas.OnEdgesCollectionChanged;
        }

        if (e.NewValue is IEnumerable<XmapGraphEdgeViewModel> newEdges)
        {
            foreach (var edge in newEdges)
            {
                edge.PropertyChanged += canvas.OnEdgePropertyChanged;
            }
        }

        canvas.InvalidateVisual();
    }

    private void OnEdgesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (XmapGraphEdgeViewModel edge in e.OldItems)
            {
                edge.PropertyChanged -= OnEdgePropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (XmapGraphEdgeViewModel edge in e.NewItems)
            {
                edge.PropertyChanged += OnEdgePropertyChanged;
            }
        }

        InvalidateVisual();
    }

    private void OnEdgePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(XmapGraphEdgeViewModel.IsOnRoute))
        {
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (EdgesSource == null) return;

        var normalEdges = new List<XmapGraphEdgeViewModel>();
        var routeEdges = new List<XmapGraphEdgeViewModel>();

        foreach (var edge in EdgesSource)
        {
            if (edge.IsOnRoute)
            {
                routeEdges.Add(edge);
            }
            else
            {
                normalEdges.Add(edge);
            }
        }

        foreach (var edge in normalEdges)
        {
            var pen = edge.IsNpc ? s_NpcPen : s_NormalPen;
            var p1 = new Point(edge.X1, edge.Y1);
            var p2 = new Point(edge.X2, edge.Y2);
            dc.DrawLine(pen, p1, p2);
            dc.DrawEllipse(s_NormalDotBrush, null, p1, 2.5, 2.5);
            dc.DrawEllipse(s_NormalDotBrush, null, p2, 2.5, 2.5);
        }

        foreach (var edge in routeEdges)
        {
            var pen = edge.IsNpc ? s_RouteNpcPen : s_RoutePen;
            var p1 = new Point(edge.X1, edge.Y1);
            var p2 = new Point(edge.X2, edge.Y2);
            dc.DrawLine(pen, p1, p2);
            dc.DrawEllipse(s_RouteDotBrush, null, p1, 4.0, 4.0);
            dc.DrawEllipse(s_RouteDotBrush, null, p2, 4.0, 4.0);
        }
    }
}
